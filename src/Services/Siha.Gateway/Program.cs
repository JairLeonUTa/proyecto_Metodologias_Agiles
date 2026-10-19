using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// El archivo de rutas cambia según el entorno: nombres de servicio en Docker, localhost en local.
// Variables de entorno admitidas: SIHA_MSA_URL, SIHA_MSB_URL (p. ej. para despliegues alternos).
var ocelotFile = builder.Environment.IsDevelopment() ? "ocelot.Development.json" : "ocelot.json";
builder.Configuration.AddJsonFile(ocelotFile, optional: false, reloadOnChange: true);

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("Falta Jwt:Key en la configuración del Gateway.");
var jwtIssuer = jwtSection["Issuer"] ?? "SIHA-IA";
var jwtAudience = jwtSection["Audience"] ?? "SIHA-IA-Clients";

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("La clave JWT (Jwt:Key) debe tener al menos 32 bytes para HMAC-SHA256.");
}

// Validación de firma de tokens JWT en el borde (US-04, tarea S1-J06).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddOcelot(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

// Sonda de salud del propio Gateway. Debe atender ANTES del middleware de Ocelot,
// porque Ocelot actúa como middleware terminal y capturaría la petición /health.
app.Use(async (contexto, siguiente) =>
{
    if (contexto.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
    {
        await contexto.Response.WriteAsJsonAsync(new
        {
            servicio = "Siha.Gateway",
            estado = "OK",
            sprint = "Sprint 1",
            rutas = ocelotFile,
            utc = DateTimeOffset.UtcNow
        });
        return;
    }

    await siguiente();
});

// Enrutamiento centralizado (Ocelot). Atiende 401/403/404 en el borde (US-04, CA2/CA3).
await app.UseOcelot();

app.Run();
