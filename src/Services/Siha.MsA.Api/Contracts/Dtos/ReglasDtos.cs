using System.ComponentModel.DataAnnotations;

namespace Siha.MsA.Api.Contracts.Dtos;

/// <summary>Parámetros heurísticos θ, δ, τ vigentes para una ruta normativa.</summary>
public record ParametroEvaluacionDto(
    int Id,
    string RutaNormativa,
    double Theta,
    double Delta,
    double Tau,
    int Version,
    DateTimeOffset ActualizadoUtc,
    string ActualizadoPor);

/// <summary>
/// Petición de actualización de umbrales. La validación matemática (rangos 0-100 y δ ≤ θ)
/// se ejecuta en el servicio y produce 400 Bad Request sin modificar la configuración previa.
/// </summary>
public record ActualizarParametrosRequest(
    [param: Range(0d, 100d)] double Theta,
    [param: Range(0d, 100d)] double Delta,
    [param: Range(0d, 100d)] double Tau);

public record ReglaDto(
    int Id,
    string Codigo,
    string RutaNormativa,
    string TipoTramite,
    string Descripcion,
    string? Expresion,
    string Version,
    string Fuente,
    bool EsNormativa,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    bool Activa);

/// <summary>
/// Cuerpo completo para POST y PUT /api/reglas/{id}. PUT exige nueva versión e inicio posterior;
/// código, ruta, tipo de trámite y EsNormativa deben coincidir con la fila identificada.
/// Vigencias inclusivas; la versión es una etiqueta opaca, no un número ordenable.
/// </summary>
public record CrearReglaRequest(
    [param: Required, MaxLength(20)] string Codigo,
    [param: Required, MaxLength(20)] string RutaNormativa,
    [param: Required, MaxLength(20)] string TipoTramite,
    [param: Required, MaxLength(400)] string Descripcion,
    [param: MaxLength(400)] string? Expresion,
    [param: Required, MaxLength(20)] string Version,
    [param: Required, MaxLength(200)] string Fuente,
    bool EsNormativa,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta);
