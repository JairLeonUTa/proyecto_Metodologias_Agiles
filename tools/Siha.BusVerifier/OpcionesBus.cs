using System.Globalization;

namespace Siha.BusVerifier;

/// <summary>
/// Opciones de ejecución del verificador del bus. Se resuelven en este orden de prioridad:
/// argumentos de línea de comandos → variables de entorno → valores por defecto del Sprint 1.
/// </summary>
public sealed class OpcionesBus
{
    public const string UriPorDefecto = "rabbitmq://localhost";
    public const string UsuarioPorDefecto = "siha";
    public const string PasswordPorDefecto = "";
    public const string ColaPorDefecto = "siha-verificador-solicitud-creada";

    public string Uri { get; private init; } = UriPorDefecto;

    public string Usuario { get; private init; } = UsuarioPorDefecto;

    public string Password { get; private init; } = PasswordPorDefecto;

    public string Cola { get; private init; } = ColaPorDefecto;

    public ushort Prefetch { get; private init; } = 16;

    /// <summary>Segundos que se espera a que RabbitMQ acepte la conexión TCP antes de rendirse.</summary>
    public int TimeoutConexionSegundos { get; private init; } = 15;

    /// <summary>Publica un <c>SolicitudCreadaEvent</c> sintético y termina al recibirlo (autoprueba).</summary>
    public bool Prueba { get; private init; }

    public bool MostrarAyuda { get; private init; }

    public static (OpcionesBus Opciones, string? Error) Parsear(string[] args)
    {
        string? uri = null;
        string? usuario = null;
        string? password = null;
        string? cola = null;
        ushort? prefetch = null;
        var timeout = 15;
        var prueba = false;
        var ayuda = false;

        for (var i = 0; i < args.Length; i++)
        {
            var argumento = args[i];

            switch (argumento)
            {
                case "-h" or "--help" or "--ayuda":
                    ayuda = true;
                    break;

                case "--prueba":
                    prueba = true;
                    break;

                case "--uri":
                    if (!TryTomarValor(args, ref i, out uri)) return Error("--uri requiere un valor (por ejemplo rabbitmq://localhost).");
                    break;

                case "--usuario":
                    if (!TryTomarValor(args, ref i, out usuario)) return Error("--usuario requiere un valor.");
                    break;

                case "--password":
                    if (!TryTomarValor(args, ref i, out password)) return Error("--password requiere un valor.");
                    break;

                case "--cola":
                    if (!TryTomarValor(args, ref i, out cola)) return Error("--cola requiere un valor.");
                    break;

                case "--prefetch":
                    if (!TryTomarValor(args, ref i, out var prefetchTexto)) return Error("--prefetch requiere un valor numérico.");
                    if (!ushort.TryParse(prefetchTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefetchValor) || prefetchValor == 0)
                    {
                        return Error("--prefetch debe ser un entero mayor que cero.");
                    }

                    prefetch = prefetchValor;
                    break;

                case "--timeout-conexion":
                    if (!TryTomarValor(args, ref i, out var timeoutTexto)) return Error("--timeout-conexion requiere un valor en segundos.");
                    if (!int.TryParse(timeoutTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out timeout) || timeout < 0)
                    {
                        return Error("--timeout-conexion debe ser un entero mayor o igual que cero.");
                    }

                    break;

                default:
                    return Error($"Argumento no reconocido: '{argumento}'.");
            }
        }

        uri ??= Ambiente("SIHA_RABBITMQ_URI") ?? UriPorDefecto;
        usuario ??= Ambiente("SIHA_RABBITMQ_USUARIO") ?? UsuarioPorDefecto;
        password ??= Ambiente("SIHA_RABBITMQ_PASSWORD") ?? PasswordPorDefecto;
        cola ??= Ambiente("SIHA_RABBITMQ_COLA") ?? ColaPorDefecto;

        if (!ayuda && string.IsNullOrWhiteSpace(password))
            return Error("Configura SIHA_RABBITMQ_PASSWORD o utiliza --password.");

        if (!System.Uri.TryCreate(uri, UriKind.Absolute, out _))
        {
            return Error($"La URI del bus '{uri}' no es válida. Ejemplo: rabbitmq://localhost");
        }

        if (string.IsNullOrWhiteSpace(cola))
        {
            return Error("El nombre de la cola no puede estar vacío.");
        }

        return (new OpcionesBus
        {
            Uri = uri,
            Usuario = usuario,
            Password = password,
            Cola = cola,
            Prefetch = prefetch ?? 16,
            TimeoutConexionSegundos = timeout,
            Prueba = prueba,
            MostrarAyuda = ayuda
        }, null);

        static (OpcionesBus, string?) Error(string mensaje) => (new OpcionesBus(), mensaje);
    }

    public static void ImprimirAyuda()
    {
        Console.WriteLine("""
        Siha.BusVerifier · suscriptor de prueba del bus del SIHA-IA (Sprint 1, tarea S1-P04)

        Uso:
          dotnet run --project tools/Siha.BusVerifier [opciones]

        Opciones:
          --uri <uri>                 URI del host RabbitMQ.    Por defecto: rabbitmq://localhost
                                      Variables: SIHA_RABBITMQ_URI
          --usuario <usuario>         Usuario del bus.           Por defecto: siha
                                      Variables: SIHA_RABBITMQ_USUARIO
          --password <clave>          Contraseña del bus.        Obligatoria (sin valor por defecto)
                                      Variables: SIHA_RABBITMQ_PASSWORD
          --cola <nombre>             Cola de suscripción.       Por defecto: siha-verificador-solicitud-creada
                                      Variables: SIHA_RABBITMQ_COLA
          --prefetch <n>              Mensajes en vuelo por consumidor. Por defecto: 16
          --timeout-conexion <seg>    Segundos de espera a que RabbitMQ acepte la conexión. Por defecto: 15
          --prueba                    Publica un SolicitudCreadaEvent sintético y termina al recibirlo.
          -h | --help | --ayuda       Muestra esta ayuda.

        El verificador se suscribe a SolicitudCreadaEvent e imprime el mensaje completo (cuerpo JSON y
        metadatos MassTransit, incluido el CorrelationId). Presione Ctrl+C para salir.

        Si el bus no está disponible, el verificador muestra el diagnóstico y las instrucciones para
        levantar RabbitMQ con Docker (docker compose up -d rabbitmq).
        """);
    }

    private static bool TryTomarValor(string[] args, ref int indice, out string valor)
    {
        if (indice + 1 >= args.Length)
        {
            valor = string.Empty;
            return false;
        }

        valor = args[++indice];
        return true;
    }

    private static string? Ambiente(string nombre)
        => Environment.GetEnvironmentVariable(nombre) is { Length: > 0 } valor ? valor : null;
}
