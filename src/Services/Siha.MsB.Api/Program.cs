using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Siha.MsB.Api.Consumers;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Events;
using Siha.MsB.Api.Options;
using Siha.MsB.Api.Security;
using Siha.MsB.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SihaDbB")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'SihaDbB'.");

builder.Services.AddDbContext<SihaDbContextB>(options => options.UseSqlServer(connectionString));

// --- Archivos: validación de PDF y almacenamiento seguro en disco (S1-S05 / S1-S06) ---
var archivosSection = builder.Configuration.GetSection(ArchivosOptions.SectionName);
builder.Services.Configure<ArchivosOptions>(archivosSection);

var archivos = archivosSection.Get<ArchivosOptions>() ?? new ArchivosOptions();

// Se deja un margen sobre el máximo permitido para que el rechazo por tamaño lo produzca la
// validación de la aplicación (400) y no el límite del servidor en peticiones apenas mayores.
var limitePeticion = archivos.TamanoMaximoBytes + (1024 * 1024);
builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = limitePeticion);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = limitePeticion);

builder.Services.AddSingleton<IArchivoStorageService, ArchivoStorageService>();

// --- Seguridad: validación de los JWT emitidos por el Microservicio A ---
var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
builder.Services.Configure<JwtSettings>(jwtSection);
var jwt = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
{
    throw new InvalidOperationException("La clave JWT (Jwt:Key) debe tener al menos 32 bytes para HMAC-SHA256.");
}

builder.Services.AddScoped<IExpedienteEventPublisher, ExpedienteEventPublisher>();

// --- Flujo borrador -> documentos verificados -> publicación, y reintento automático tras caídas del bus ---
builder.Services.AddScoped<ExpedienteEnvioService>();
builder.Services.Configure<RepublicacionOptions>(builder.Configuration.GetSection(RepublicacionOptions.SectionName));
builder.Services.AddSingleton<RepublicacionAutomaticaWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RepublicacionAutomaticaWorker>());

// --- Bus de eventos: MassTransit + RabbitMQ (US-06, tarea S1-J07) ---
var rabbitUri = builder.Configuration["RabbitMq:Uri"] ?? "rabbitmq://localhost";
var rabbitUsuario = builder.Configuration["RabbitMq:Usuario"] ?? "guest";
var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";

builder.Services.AddMassTransit(x =>
{
    // Suscriptor de verificación del contrato (evidencia del flujo extremo a extremo de S1-J09).
    x.AddConsumer<VerificacionSolicitudConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(new Uri(rabbitUri), host =>
        {
            host.Username(rabbitUsuario);
            host.Password(rabbitPassword);
        });

        // Reintentos a nivel de mensaje para no perder el CorrelationId ante fallos transitorios.
        cfg.UseMessageRetry(retry => retry.Exponential(
            5,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(2)));

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SIHA-IA · Microservicio B",
        Version = "v1",
        Description = "Registro de expedientes (US-05) y publicación asíncrona en RabbitMQ (US-06). Sprint 1."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Token JWT emitido por el Microservicio A (/api/auth/login)."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// Fallo temprano y visible si el volumen de documentos no es escribible (p. ej. permisos del contenedor).
VerificarAlmacenamiento(app, archivos);

await MigrarAsync(app);

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "SIHA-IA Microservicio B v1"));

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    servicio = "Siha.MsB.Api",
    estado = "OK",
    sprint = "Sprint 1",
    bus = "MassTransit + RabbitMQ",
    utc = DateTimeOffset.UtcNow
}));

app.Run();

static void VerificarAlmacenamiento(WebApplication app, ArchivosOptions archivos)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var raiz = Path.GetFullPath(archivos.RutaAlmacenamiento);

    try
    {
        Directory.CreateDirectory(raiz);
        var sonda = Path.Combine(raiz, $".sonda-{Guid.NewGuid():N}");
        File.WriteAllText(sonda, "ok");
        File.Delete(sonda);
        logger.LogInformation("Almacenamiento de documentos listo en {Raiz}.", raiz);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        // No se detiene el servicio: las cargas responderán 503 hasta que el volumen esté disponible.
        logger.LogError(ex, "El almacenamiento de documentos en {Raiz} no es escribible.", raiz);
    }
}

static async Task MigrarAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<SihaDbContextB>();

    // Un proveedor no relacional (p. ej. EF Core InMemory en pruebas automatizadas) no admite
    // migraciones: el esquema se crea en memoria sin tocar SQL Server.
    if (!db.Database.IsRelational())
    {
        logger.LogInformation("Proveedor de base de datos no relacional: se omite la migración de DB B.");
        return;
    }

    var intentos = 0;
    while (true)
    {
        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Microservicio B listo. DB B migrada. Swagger en /swagger, salud en /health.");
            break;
        }
        catch (Exception ex) when (intentos < 24)
        {
            intentos++;
            logger.LogWarning(
                "SQL Server DB B no disponible (intento {Intento}/24). Reintento en 5 s. Detalle: {Mensaje}",
                intentos, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

/// <summary>Expuesto para las pruebas de integración con <c>WebApplicationFactory</c>.</summary>
public partial class Program
{
}
