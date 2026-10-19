using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Siha.Contracts;
using Siha.Contracts.Events;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace Siha.Integracion.Tests.Soporte;

/// <summary>
/// Levanta UNA vez por ejecución SQL Server 2022 y RabbitMQ 3.13 reales en Docker (las mismas
/// imágenes que docker-compose.yml). Cada prueba crea su propia base de datos y su propia cola de
/// escucha para no interferir con las demás.
/// </summary>
public sealed class InfraestructuraReal : IAsyncLifetime
{
    public const string ImagenSqlServer = "mcr.microsoft.com/mssql/server:2022-latest";
    public const string ImagenRabbitMq = "rabbitmq:3.13-management";
    public const string UsuarioRabbit = "siha";
    public const string PasswordRabbit = "siha-rabbit-2026";
    public const string ClaveJwt = "SIHA-IA-clave-de-firma-JWT-para-el-Sprint-1-2026-256bits";
    public const string EmisorJwt = "SIHA-IA";
    public const string AudienciaJwt = "SIHA-IA-Clients";

    public MsSqlContainer SqlServer { get; } = new MsSqlBuilder(ImagenSqlServer)
        .WithPassword("SihaAdmin2026!")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder(ImagenRabbitMq)
        .WithUsername(UsuarioRabbit)
        .WithPassword(PasswordRabbit)
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(SqlServer.StartAsync(), RabbitMq.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await SqlServer.DisposeAsync();
    }

    /// <summary>Cadena de conexión a una base de datos nueva (aislada) dentro del SQL Server real.</summary>
    public string CadenaConexion(string baseDatos)
    {
        var builder = new SqlConnectionStringBuilder(SqlServer.GetConnectionString())
        {
            InitialCatalog = baseDatos,
            TrustServerCertificate = true,
            Encrypt = false
        };
        return builder.ConnectionString;
    }

    public Uri UriRabbit => new($"rabbitmq://{RabbitMq.Hostname}:{RabbitMq.GetMappedPublicPort(5672)}");

    /// <summary>JWT firmado con la misma clave que usa el Microservicio A en docker-compose.</summary>
    public static string TokenOperador()
    {
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ClaveJwt)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            EmisorJwt,
            AudienciaJwt,
            new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, RolesSistema.Operador)
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionInfraestructura : ICollectionFixture<InfraestructuraReal>
{
    public const string Nombre = "Infraestructura real (SQL Server + RabbitMQ)";
}

/// <summary>
/// Microservicio B real (Program.cs completo: EF Core SqlServer, migraciones, MassTransit RabbitMQ,
/// JWT y reintento automático) apuntando a los contenedores de <see cref="InfraestructuraReal"/>.
/// </summary>
public sealed class MsBReal : WebApplicationFactory<Program>
{
    private readonly InfraestructuraReal _infra;

    public MsBReal(InfraestructuraReal infra, string? rutaAlmacenamiento = null)
    {
        _infra = infra;
        BaseDatos = $"SihaDbB_it_{Guid.NewGuid():N}"[..30];
        RutaAlmacenamiento = rutaAlmacenamiento
            ?? Path.Combine(Path.GetTempPath(), "siha-integracion", Guid.NewGuid().ToString("N"));
    }

    public string BaseDatos { get; }

    public string RutaAlmacenamiento { get; }

    public string CadenaConexion => _infra.CadenaConexion(BaseDatos);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        // UseSetting (y no ConfigureAppConfiguration) porque Program.cs lee la configuración antes
        // de builder.Build(); los valores de host sí están disponibles en ese momento.
        foreach (var (clave, valor) in Configuracion())
        {
            builder.UseSetting(clave, valor);
        }
    }

    private Dictionary<string, string?> Configuracion() => new()
    {
                ["ConnectionStrings:SihaDbB"] = CadenaConexion,
                ["Jwt:Key"] = InfraestructuraReal.ClaveJwt,
                ["Jwt:Issuer"] = InfraestructuraReal.EmisorJwt,
                ["Jwt:Audience"] = InfraestructuraReal.AudienciaJwt,
                ["RabbitMq:Uri"] = _infra.UriRabbit.ToString(),
                ["RabbitMq:Usuario"] = InfraestructuraReal.UsuarioRabbit,
                ["RabbitMq:Password"] = InfraestructuraReal.PasswordRabbit,
                ["Archivos:RutaAlmacenamiento"] = RutaAlmacenamiento,
                ["Archivos:TamanoMaximoBytes"] = (10 * 1024 * 1024).ToString(),
                ["Republicacion:Habilitada"] = "true",
                ["Republicacion:IntervaloSegundos"] = "2",
                ["Republicacion:EnCursoAbandonadoSegundos"] = "60"
    };

    public HttpClient ClienteOperador()
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", InfraestructuraReal.TokenOperador());
        return cliente;
    }
}

/// <summary>Mensaje recibido por el suscriptor independiente, con sus cabeceras de transporte.</summary>
public sealed record MensajeRecibido(SolicitudCreadaEvent Evento, Guid? CorrelationId, Guid? MessageId, DateTimeOffset RecibidoUtc);

/// <summary>
/// Suscriptor RabbitMQ independiente del Microservicio B (su propio bus y su propia cola durable),
/// equivalente al futuro Microservicio C. Sirve para comprobar la recepción real del mensaje.
/// </summary>
public sealed class SuscriptorEspia : IAsyncDisposable
{
    private readonly IBusControl _bus;

    public ConcurrentQueue<MensajeRecibido> Recibidos { get; } = new();

    private SuscriptorEspia(IBusControl bus) => _bus = bus;

    public static async Task<SuscriptorEspia> IniciarAsync(InfraestructuraReal infra)
    {
        SuscriptorEspia? espia = null;
        var cola = $"siha-integracion-espia-{Guid.NewGuid():N}";

        var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(infra.UriRabbit, h =>
            {
                h.Username(InfraestructuraReal.UsuarioRabbit);
                h.Password(InfraestructuraReal.PasswordRabbit);
            });

            cfg.ReceiveEndpoint(cola, e =>
            {
                e.Durable = true;
                e.AutoDelete = false;
                e.Handler<SolicitudCreadaEvent>(contexto =>
                {
                    espia!.Recibidos.Enqueue(new MensajeRecibido(
                        contexto.Message, contexto.CorrelationId, contexto.MessageId, DateTimeOffset.UtcNow));
                    return Task.CompletedTask;
                });
            });
        });

        espia = new SuscriptorEspia(bus);
        await bus.StartAsync(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);
        return espia;
    }

    public async Task<IReadOnlyList<MensajeRecibido>> EsperarAsync(Guid expedienteId, TimeSpan limite, int cantidad = 1)
    {
        var fin = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < fin)
        {
            var encontrados = Recibidos.Where(m => m.Evento.ExpedienteId == expedienteId).ToList();
            if (encontrados.Count >= cantidad)
            {
                return encontrados;
            }

            await Task.Delay(200);
        }

        return Recibidos.Where(m => m.Evento.ExpedienteId == expedienteId).ToList();
    }

    public async ValueTask DisposeAsync() => await _bus.StopAsync();
}
