extern alias msc;

using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Siha.Contracts.Events;

namespace Siha.Integracion.Tests.Soporte;

/// <summary>
/// Microservicio C real (Sprint 2): Program.cs completo con EF Core SqlServer y migraciones de DB C,
/// MassTransit sobre el RabbitMQ real y el consumidor idempotente de SolicitudCreadaEvent.
/// Cada instancia usa su propia base y su propia cola para no mezclar mensajes entre pruebas.
/// </summary>
public sealed class MsCReal : WebApplicationFactory<msc::Program>
{
    private readonly InfraestructuraReal _infra;

    /// <param name="rutaAlmacenamiento">Volumen compartido con el Microservicio B (mismos PDF).</param>
    public MsCReal(InfraestructuraReal infra, string rutaAlmacenamiento, string? urlReglasGrpc = null)
    {
        _infra = infra;
        RutaAlmacenamiento = rutaAlmacenamiento;
        UrlReglasGrpc = urlReglasGrpc ?? "http://localhost:1";
        BaseDatos = $"SihaDbC_it_{Guid.NewGuid():N}"[..30];
        Cola = $"solicitud-creada-ms-c-it-{Guid.NewGuid():N}";
    }

    public string BaseDatos { get; }
    public string Cola { get; }
    public string RutaAlmacenamiento { get; }
    public string UrlReglasGrpc { get; }
    public string CadenaConexion => _infra.CadenaConexion(BaseDatos);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:SihaDbC", CadenaConexion);
        builder.UseSetting("Jwt:Key", InfraestructuraReal.ClaveJwt);
        builder.UseSetting("Jwt:Issuer", InfraestructuraReal.EmisorJwt);
        builder.UseSetting("Jwt:Audience", InfraestructuraReal.AudienciaJwt);
        builder.UseSetting("RabbitMq:Uri", _infra.UriRabbit.ToString());
        builder.UseSetting("RabbitMq:Usuario", InfraestructuraReal.UsuarioRabbit);
        builder.UseSetting("RabbitMq:Password", InfraestructuraReal.PasswordRabbit);
        builder.UseSetting("RabbitMq:ColaSolicitudCreada", Cola);
        builder.UseSetting("Archivos:RutaAlmacenamiento", RutaAlmacenamiento);
        builder.UseSetting("ReglasGrpc:Url", UrlReglasGrpc);
    }

    /// <summary>Arranca el host: migra DB C y conecta el consumidor al bus.</summary>
    public async Task IniciarAsync()
    {
        CreateClient();
        var bus = Services.GetRequiredService<IBusControl>();
        await bus.WaitForHealthStatus(BusHealthStatus.Healthy, TimeSpan.FromSeconds(60));
    }

    public async Task<int> ContarAsync(string sql)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        return Convert.ToInt32(await comando.ExecuteScalarAsync());
    }

    public async Task<T?> ValorAsync<T>(string sql)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        var valor = await comando.ExecuteScalarAsync();
        return valor is null or DBNull ? default : (T)Convert.ChangeType(valor, typeof(T));
    }

    /// <summary>Espera hasta que la consulta devuelva al menos <paramref name="minimo"/> filas.</summary>
    public async Task<int> EsperarFilasAsync(string sql, int minimo, TimeSpan limite)
    {
        var fin = DateTime.UtcNow + limite;
        var filas = 0;
        while (DateTime.UtcNow < fin)
        {
            filas = await ContarAsync(sql);
            if (filas >= minimo)
            {
                return filas;
            }

            await Task.Delay(300);
        }

        return filas;
    }
}

/// <summary>Publicador independiente para enviar SolicitudCreadaEvent al bus real como lo hace el Microservicio B.</summary>
public sealed class PublicadorPrueba : IAsyncDisposable
{
    private readonly IBusControl _bus;

    private PublicadorPrueba(IBusControl bus) => _bus = bus;

    public static async Task<PublicadorPrueba> IniciarAsync(InfraestructuraReal infra)
    {
        var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(infra.UriRabbit, h =>
            {
                h.Username(InfraestructuraReal.UsuarioRabbit);
                h.Password(InfraestructuraReal.PasswordRabbit);
            });
        });

        await bus.StartAsync(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);
        return new PublicadorPrueba(bus);
    }

    /// <summary>Publica con MessageId = ExpedienteId y la cabecera CorrelationId, igual que el Microservicio B.</summary>
    public Task PublicarAsync(SolicitudCreadaEvent evento) =>
        _bus.Publish(evento, contexto =>
        {
            contexto.MessageId = evento.ExpedienteId;
            contexto.CorrelationId = evento.CorrelationId;
        });

    public async ValueTask DisposeAsync() => await _bus.StopAsync();
}
