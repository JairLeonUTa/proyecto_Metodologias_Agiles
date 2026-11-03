using Siha.MsC.Api.Contracts.Dtos;

namespace Siha.MsC.Api.Services;

/// <summary>Servicio de búsqueda de precedentes homologatorios (US-11, S2-J08).</summary>
public interface IPrecedenteService
{
    /// <summary>
    /// Busca precedentes por coincidencia EXACTA de:
    /// - Todas las asignaturas de origen (códigos, conjunto igual)
    /// - Asignatura de destino
    /// - Institución de origen
    /// </summary>
    /// <param name="request">Criterios de búsqueda.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Lista de precedentes coincidentes (vacía si no hay match exacto).</returns>
    Task<IReadOnlyCollection<PrecedenteResponse>> BuscarAsync(
        BuscarPrecedenteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra un nuevo precedente (para uso futuro, Sprint 3 US-15.4).
    /// </summary>
    Task<Guid> RegistrarAsync(
        string resolucion,
        IReadOnlyCollection<(string Codigo, string Nombre)> asignaturasOrigen,
        string asignaturaDestino,
        string institucionOrigen,
        DateOnly fecha,
        CancellationToken cancellationToken = default);
}