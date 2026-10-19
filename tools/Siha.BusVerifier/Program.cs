using System.Net.Sockets;
using MassTransit;
using Siha.BusVerifier;
using Siha.Contracts.Events;

// ============================================================================
//  Siha.BusVerifier · Suscriptor de prueba del bus (Sprint 1, tarea S1-P04)
//  Se suscribe a SolicitudCreadaEvent en RabbitMQ e imprime el mensaje completo.
//  Ver tools/Siha.BusVerifier/README.md para el detalle de uso.
// ============================================================================

try
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
}
catch
{
    // La consola actual no admite UTF-8 (por ejemplo salida redirigida): se continúa igualmente.
}

var (opciones, errorConfiguracion) = OpcionesBus.Parsear(args);

if (errorConfiguracion is not null)
{
    Console.Error.WriteLine($"✘ Configuración inválida: {errorConfiguracion}");
    Console.Error.WriteLine();
    OpcionesBus.ImprimirAyuda();
    return 1;
}

if (opciones.MostrarAyuda)
{
    OpcionesBus.ImprimirAyuda();
    return 0;
}

ImprimirEncabezado(opciones);

Console.WriteLine($"Comprobando disponibilidad de RabbitMQ en {opciones.Uri} ...");
if (!EsperarBroker(opciones))
{
    ImprimirDiagnosticoSinBus(opciones);
    return 0;
}

Console.WriteLine("RabbitMQ disponible. Iniciando el bus de MassTransit ...");

using var cierre = new CancellationTokenSource();
Console.CancelKeyPress += (_, evento) =>
{
    evento.Cancel = true;
    Console.WriteLine();
    Console.WriteLine("Ctrl+C detectado: cerrando el verificador ...");
    cierre.Cancel();
};

var recepcion = new TaskCompletionSource<SolicitudCreadaEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
NotificadorRecepcion.Recibido += evento => recepcion.TrySetResult(evento);

IBusControl bus;
try
{
    bus = CrearBus(opciones);
    await bus.StartAsync(TimeSpan.FromSeconds(Math.Max(opciones.TimeoutConexionSegundos, 5)));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"✘ No se pudo iniciar el bus: {ex.Message}");
    return 2;
}

Console.WriteLine("✔ Bus iniciado. Suscripción activa:");
Console.WriteLine($"    Host     : {opciones.Uri}");
Console.WriteLine($"    Cola     : {opciones.Cola}");
Console.WriteLine($"    Usuario  : {opciones.Usuario}");
Console.WriteLine($"    Mensaje  : {typeof(SolicitudCreadaEvent).FullName}");
Console.WriteLine();

if (opciones.Prueba)
{
    var eventoPrueba = CrearEventoDePrueba();
    Console.WriteLine($"Modo --prueba: publicando un evento sintético con CorrelationId {eventoPrueba.CorrelationId} ...");

    try
    {
        await bus.Publish(eventoPrueba, contexto => contexto.CorrelationId = eventoPrueba.CorrelationId, cierre.Token);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"✘ No se pudo publicar el evento sintético: {ex.Message}");
        await bus.StopAsync();
        return 2;
    }

    var completada = await Task.WhenAny(recepcion.Task, Task.Delay(TimeSpan.FromSeconds(30), cierre.Token));
    if (completada == recepcion.Task)
    {
        Console.WriteLine("✔ Autoprueba completada: el contrato viaja íntegro por el bus (id de ida = id de vuelta).");
        await bus.StopAsync();
        return 0;
    }

    Console.Error.WriteLine("✘ No se recibió el evento sintético en 30 s. Revise la conexión con RabbitMQ.");
    await bus.StopAsync();
    return 2;
}

Console.WriteLine("Esperando mensajes del Microservicio B. Presione Ctrl+C para salir.");
try
{
    await Task.Delay(Timeout.Infinite, cierre.Token);
}
catch (OperationCanceledException)
{
    // Salida solicitada por el operador.
}

await bus.StopAsync();
Console.WriteLine("Verificador finalizado.");
return 0;

// ----------------------------------------------------------------------------
//  Funciones locales
// ----------------------------------------------------------------------------

static void ImprimirEncabezado(OpcionesBus opciones)
{
    Console.WriteLine(new string('=', 86));
    Console.WriteLine("  SIHA-IA · Subscriptor de prueba del bus (Sprint 1 · tarea S1-P04)");
    Console.WriteLine("  Universidad Técnica de Ambato · FISEI · Metodologías Ágiles");
    Console.WriteLine(new string('=', 86));
    Console.WriteLine($"  Contrato  : SolicitudCreadaEvent (Siha.Contracts)");
    Console.WriteLine($"  Host bus  : {opciones.Uri}");
    Console.WriteLine($"  Usuario   : {opciones.Usuario}");
    Console.WriteLine($"  Cola      : {opciones.Cola}");
    Console.WriteLine($"  Modo      : {(opciones.Prueba ? "autoprueba (--prueba)" : "suscripción continua")}");
    Console.WriteLine(new string('-', 86));
    Console.WriteLine();
}

static bool EsperarBroker(OpcionesBus opciones)
{
    var (host, puerto) = DescomponerUri(opciones.Uri);
    var limite = DateTimeOffset.UtcNow.AddSeconds(opciones.TimeoutConexionSegundos);
    var intento = 0;

    while (true)
    {
        intento++;

        try
        {
            using var cliente = new TcpClient();
            using var intentoCancelable = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            cliente.ConnectAsync(host, puerto, intentoCancelable.Token).AsTask().GetAwaiter().GetResult();
            Console.WriteLine($"    · intento {intento}: conexión TCP establecida con {host}:{puerto}.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    · intento {intento}: {host}:{puerto} no responde ({ex.GetType().Name}).");
        }

        if (DateTimeOffset.UtcNow >= limite)
        {
            return false;
        }

        Thread.Sleep(TimeSpan.FromSeconds(2));
    }
}

static void ImprimirDiagnosticoSinBus(OpcionesBus opciones)
{
    var (host, puerto) = DescomponerUri(opciones.Uri);

    Console.WriteLine();
    Console.WriteLine($"✘ RabbitMQ no está disponible en {host}:{puerto} tras {opciones.TimeoutConexionSegundos} s.");
    Console.WriteLine("  El verificador quedó listo pero sin bus al que suscribirse (no es un fallo del contrato).");
    Console.WriteLine();
    Console.WriteLine("  Levante el bus y vuelva a ejecutar el verificador:");
    Console.WriteLine("    docker compose up -d rabbitmq");
    Console.WriteLine("    docker compose ps rabbitmq          # debe verse como healthy");
    Console.WriteLine();
    Console.WriteLine("  Panel de administración: http://localhost:15672  (siha / siha-rabbit-2026)");
    Console.WriteLine();
    Console.WriteLine("  Si RabbitMQ corre en otro host o puerto:");
    Console.WriteLine("    dotnet run --project tools/Siha.BusVerifier -- --uri rabbitmq://mi-host:5672");
}

static IBusControl CrearBus(OpcionesBus opciones) =>
    Bus.Factory.CreateUsingRabbitMq(cfg =>
    {
        cfg.Host(new Uri(opciones.Uri), host =>
        {
            host.Username(opciones.Usuario);
            host.Password(opciones.Password);
        });

        cfg.ReceiveEndpoint(opciones.Cola, endpoint =>
        {
            endpoint.PrefetchCount = opciones.Prefetch;
            endpoint.Consumer<VerificadorSolicitudCreadaConsumer>();
        });
    });

static SolicitudCreadaEvent CrearEventoDePrueba() => new()
{
    ExpedienteId = Guid.NewGuid(),
    CorrelationId = Guid.NewGuid(),
    CodigoExpediente = $"EXP-PRUEBA-{DateTime.UtcNow:yyyyMMddHHmmss}",
    TipoTramite = "INTERNO",
    RutaNormativa = "CAU-P-011",
    CarreraOrigenCodigo = "SW",
    CarreraDestinoCodigo = "TI",
    DocumentoIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
    AsignaturasOrigen = new[] { "SW101", "SW201" },
    Estado = "PENDIENTE_CONTENIDOS",
    FechaCreacionUtc = DateTimeOffset.UtcNow,
    Version = "1.0"
};

static (string Host, int Puerto) DescomponerUri(string uri)
{
    var analizada = new Uri(uri);
    var puerto = analizada.Port <= 0
        ? (analizada.Scheme.Contains("amqps", StringComparison.OrdinalIgnoreCase) ? 5671 : 5672)
        : analizada.Port;

    return (analizada.Host, puerto);
}
