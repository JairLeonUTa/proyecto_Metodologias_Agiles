using System.Text;
using Grpc.Core;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Siha.MsA.Api.Grpc;
using Siha.MsC.Api.Consumers;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Grpc;
using Siha.MsC.Api.Options;
using Siha.MsC.Api.Security;
using Siha.MsC.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SihaDbC")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'SihaDbC'.");

builder.Services.AddDbContext<SihaDbContextC>(options => options.UseSqlServer(connectionString));

// Los PDF viven en el volumen compartido que escribe el Microservicio B. El consumidor
// solo acepta rutas relativas dentro de esta raíz y revalida tamaño y SHA-256.
builder.Services.Configure<AlmacenOptions>(builder.Configuration.GetSection(AlmacenOptions.SectionName));
builder.Services.AddSingleton<IAlmacenDocumentos, AlmacenDocumentos>();
builder.Services.AddScoped<ProcesarSolicitudCreadaService>();
builder.Services.AddScoped<ExtraccionService>();

// --- Estrategias consolidadas (Strategy Registry S2-J10) ---
builder.Services.AddStrategies();

// --- Cliente gRPC hacia Microservicio A (ReglasGrpc) ---
builder.Services.Configure<ReglasGrpcOptions>(builder.Configuration.GetSection(ReglasGrpcOptions.SectionName));

// --- Agente IA local (Ollama, S2-S01): solo configuración; lo consumen las estrategias de US-10 ---
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));

builder.Services.AddHttpClient<IOllamaEmbeddingService, OllamaEmbeddingService>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(cfg.Url.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(cfg.TimeoutSegundos);
});

builder.Services.AddGrpcClient<ReglasGrpc.ReglasGrpcClient>((sp, options) =>
{
    var cfg = sp.GetRequiredService<IOptions<ReglasGrpcOptions>>().Value;
    options.Address = new Uri(cfg.Url);
})
.ConfigureChannel(o =>
{
    // h2c (HTTP/2 sin TLS) en red Docker interna.
    o.Credentials = ChannelCredentials.Insecure;
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    return handler;
});

builder.Services.AddScoped<IReglasGrpcClient, ReglasGrpcClient>();

// El agente consulta la malla vigente de la carrera de destino en MS A (mismo puerto gRPC de reglas).
builder.Services.AddGrpcClient<CatalogoGrpc.CatalogoGrpcClient>((sp, options) =>
{
    var cfg = sp.GetRequiredService<IOptions<ReglasGrpcOptions>>().Value;
    options.Address = new Uri(cfg.Url);
})
.ConfigureChannel(o => o.Credentials = ChannelCredentials.Insecure);
builder.Services.AddScoped<ICatalogoGrpcCliente, CatalogoGrpcCliente>();
builder.Services.AddScoped<SolicitudesAnalisisService>();

// --- Seguridad: los endpoints de confirmación validan los JWT emitidos por el Microservicio A ---
var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
builder.Services.Configure<JwtSettings>(jwtSection);
var jwt = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("La clave JWT (Jwt:Key) debe tener al menos 32 bytes para HMAC-SHA256.");

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
builder.Services.AddControllers();

var rabbitUri = builder.Configuration["RabbitMq:Uri"] ?? "rabbitmq://localhost";
var rabbitUsuario = builder.Configuration["RabbitMq:Usuario"] ?? "guest";
var rabbitPassword = builder.Configuration["RabbitMq:Password"];
if (string.IsNullOrWhiteSpace(rabbitPassword))
    throw new InvalidOperationException("Configura RabbitMq__Password para conectar al bus.");

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SolicitudCreadaConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(new Uri(rabbitUri), host =>
        {
            host.Username(rabbitUsuario);
            host.Password(rabbitPassword);
        });

        // at-least-once: si la descarga/verificación falla de forma transitoria, MassTransit
        // reintenta el mismo mensaje. La tabla PROCESSED_EVENTS evita repetir el trabajo.
        cfg.UseMessageRetry(retry => retry.Exponential(
            5,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(2)));

        // Nombre configurable para aislar las pruebas de integración; en Docker queda el valor por defecto.
        var cola = builder.Configuration["RabbitMq:ColaSolicitudCreada"] ?? "solicitud-creada-ms-c";
        cfg.ReceiveEndpoint(cola, endpoint =>
        {
            endpoint.ConfigureConsumer<SolicitudCreadaConsumer>(context);
        });
    });
});

var app = builder.Build();

await MigrarAsync(app);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    servicio = "Siha.MsC.Api",
    estado = "OK",
    sprint = "Sprint 2",
    consumidor = "solicitud-creada-ms-c",
    endpoints = new[]
    {
        "GET /api/extracciones/{id}",
        "PUT /api/extracciones/{id}/confirmar",
        "POST /api/extracciones/calcular-creditos",
        "POST /api/extracciones/calcular-contenido",
        "POST /api/extracciones/precedentes/buscar",
        "POST /api/extracciones/{id}/analizar"
    },
    utc = DateTimeOffset.UtcNow
}));

app.Run();

static async Task MigrarAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<SihaDbContextC>();

    if (!db.Database.IsRelational())
    {
        logger.LogInformation("Proveedor no relacional: se omite la migración de DB C.");
        return;
    }

    var intentos = 0;
    while (true)
    {
        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Microservicio C listo. DB C migrada. Salud en /health.");
            break;
        }
        catch (Exception ex) when (intentos < 24)
        {
            intentos++;
            logger.LogWarning(
                "SQL Server DB C no disponible (intento {Intento}/24). Reintento en 5 s. Detalle: {Mensaje}",
                intentos, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

/// <summary>Expuesto para las pruebas de integración con <c>WebApplicationFactory</c>.</summary>
public partial class Program
{
}
