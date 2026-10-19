using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.GrpcServices;
using Siha.MsA.Api.Security;
using Siha.MsA.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- gRPC (S1-S04) -------------------------------------------------------------------------
// Kestrel expone DOS listeners cuando no hay TLS:
//   1. API REST + Swagger  -> HTTP/1.1 (puerto de Urls/ASPNETCORE_URLS: 5081 local, 8080 Docker).
//   2. Servicio gRPC       -> HTTP/2 en claro (h2c) en un puerto dedicado (Grpc:Puerto / Grpc__Puerto).
// No es posible negociar HTTP/1.1 y HTTP/2 sobre un mismo puerto sin TLS: la selección de protocolo
// requiere ALPN y, sin certificado, Kestrel deja el endpoint en HTTP/1.1 (el cliente gRPC falla con
// HTTP_1_1_REQUIRED). Por eso gRPC se aísla en su propio listener HTTP/2 (prior knowledge).
//
// Nota: al declarar listeners por código, Kestrel ignora las direcciones derivadas de
// UseUrls/ASPNETCORE_URLS, por lo que la dirección REST se vuelve a registrar explícitamente.
// Se toma de ASPNETCORE_URLS/Urls: en Docker http://+:8080 y en local http://localhost:5081.
var urlsRest = (Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
        ?? builder.Configuration["Urls"]
        ?? "http://localhost:5081")
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var puertoGrpc = builder.Configuration.GetValue<int?>("Grpc:Puerto") ?? 5083;

builder.WebHost.ConfigureKestrel(options =>
{
    // API REST y Swagger en HTTP/1.1.
    foreach (var url in urlsRest)
    {
        Escuchar(options, url, HttpProtocols.Http1);
    }

    // Listener dedicado para gRPC: solo HTTP/2, sin TLS (h2c).
    options.ListenAnyIP(puertoGrpc, listen => listen.Protocols = HttpProtocols.Http2);
});

// Registra un listener de Kestrel a partir de una URL de configuración ("http://+:8080",
// "http://localhost:5081" o "http://ip:puerto"), aplicando el protocolo indicado.
static void Escuchar(KestrelServerOptions options, string url, HttpProtocols protocolos)
{
    var uri = new Uri(url.Replace("+", "0.0.0.0").Replace("*", "0.0.0.0"));
    var puerto = uri.IsDefaultPort ? 80 : uri.Port;

    void Aplicar(ListenOptions listen) => listen.Protocols = protocolos;

    if (uri.Host is "0.0.0.0" or "any")
    {
        options.ListenAnyIP(puerto, Aplicar);
    }
    else if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
    {
        options.ListenLocalhost(puerto, Aplicar);
    }
    else if (IPAddress.TryParse(uri.Host, out var ip))
    {
        options.Listen(ip, puerto, Aplicar);
    }
    else
    {
        // Host no local resoluble como IP (p. ej. un nombre DNS): escuchar en todas las interfaces.
        options.ListenAnyIP(puerto, Aplicar);
    }
}


var connectionString = builder.Configuration.GetConnectionString("SihaDbA")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'SihaDbA'.");

builder.Services.AddDbContext<SihaDbContextA>(options => options.UseSqlServer(connectionString));

// --- Seguridad (US-01) ---
var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
builder.Services.Configure<JwtSettings>(jwtSection);
var jwt = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
{
    throw new InvalidOperationException("La clave JWT (Jwt:Key) debe tener al menos 32 bytes para HMAC-SHA256.");
}

builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// --- Servicios de dominio ---
builder.Services.AddScoped<IReglasService, ReglasService>();
builder.Services.AddScoped<ICatalogoService, CatalogoService>();

// --- gRPC de reglas y umbrales (S1-S04) ---
builder.Services.AddGrpc();
builder.Services.AddScoped<ReglasGrpcService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SIHA-IA · Microservicio A",
        Version = "v1",
        Description = "Seguridad y RBAC (US-01), reglas y umbrales (US-02) y catálogo académico (US-03). Sprint 1."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Escriba únicamente el token JWT obtenido en /api/auth/login."
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

await MigrarYSembrarAsync(app);

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "SIHA-IA Microservicio A v1"));

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Servicio gRPC (paquete siha.reglas.v1). Se publica en el listener HTTP/2 dedicado (Grpc:Puerto),
// separado del endpoint REST porque gRPC sobre h2c exige un puerto exclusivo sin TLS.
app.MapGrpcService<ReglasGrpcService>();

app.MapGet("/health", () => Results.Ok(new
{
    servicio = "Siha.MsA.Api",
    estado = "OK",
    sprint = "Sprint 1",
    grpc = "siha.reglas.v1.ReglasGrpc",
    grpcPuerto = puertoGrpc,
    utc = DateTimeOffset.UtcNow
}));

app.Run();

static async Task MigrarYSembrarAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<SihaDbContextA>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    var intentos = 0;
    while (true)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (intentos < 24)
        {
            intentos++;
            logger.LogWarning(
                "SQL Server DB A no disponible (intento {Intento}/24). Reintento en 5 s. Detalle: {Mensaje}",
                intentos, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    var correo = configuration["Seed:AdminCorreo"] ?? "admin@uta.edu.ec";
    var password = configuration["Seed:AdminPassword"];
    if (string.IsNullOrWhiteSpace(password))
        throw new InvalidOperationException("Configura Seed__AdminPassword para inicializar la cuenta administradora.");

    await DbSeederA.SembrarAsync(db, hasher, correo, password, logger);
    logger.LogInformation("Microservicio A listo. Swagger en /swagger, salud en /health.");
}
