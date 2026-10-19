using Siha.Contracts.Events;

namespace Siha.BusVerifier;

/// <summary>
/// Punto de enganche para que el programa principal sepa cuándo llegó un evento
/// (se usa con <c>--prueba</c> para terminar automáticamente tras la primera recepción).
/// </summary>
public static class NotificadorRecepcion
{
    public static event Action<SolicitudCreadaEvent>? Recibido;

    public static void Notificar(SolicitudCreadaEvent evento) => Recibido?.Invoke(evento);
}
