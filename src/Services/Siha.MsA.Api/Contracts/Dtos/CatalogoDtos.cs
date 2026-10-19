using System.ComponentModel.DataAnnotations;

namespace Siha.MsA.Api.Contracts.Dtos;

public record CarreraDto(int Id, string Codigo, string Nombre, string Facultad, bool Activa);

public record CrearCarreraRequest(
    [param: Required, MaxLength(20)] string Codigo,
    [param: Required, MaxLength(150)] string Nombre,
    [param: MaxLength(60)] string Facultad = "FISEI",
    bool Activa = true);

public record ActualizarCarreraRequest(
    [param: Required, MaxLength(150)] string Nombre,
    [param: MaxLength(60)] string Facultad,
    bool Activa);

public record MallaDto(
    int Id,
    int CarreraId,
    string CarreraCodigo,
    string Version,
    int Anio,
    string Fuente,
    string? RutaNormativa,
    bool Vigente,
    int TotalAsignaturas);

public record CrearMallaRequest(
    [param: Range(1, int.MaxValue)] int CarreraId,
    [param: Required, MaxLength(20)] string Version,
    [param: Range(1990, 2100)] int Anio,
    [param: Required, MaxLength(200)] string Fuente,
    [param: MaxLength(20)] string? RutaNormativa,
    bool Vigente = true);

public record UnidadTematicaDto(int Numero, string Nombre, int Horas, string Contenidos);

public record AsignaturaDto(
    int Id,
    int MallaId,
    string Codigo,
    string Nombre,
    int Creditos,
    int Horas,
    int Semestre,
    bool PlanCompleto,
    IReadOnlyCollection<UnidadTematicaDto> Unidades);

public record UnidadTematicaRequest(
    [param: Range(1, 50)] int Numero,
    [param: Required, MaxLength(200)] string Nombre,
    [param: Range(0, 1000)] int Horas,
    [param: MaxLength(4000)] string Contenidos = "");

public record CrearAsignaturaRequest(
    [param: Range(1, int.MaxValue)] int MallaId,
    [param: Required, MaxLength(30)] string Codigo,
    [param: Required, MaxLength(200)] string Nombre,
    [param: Range(0, 30)] int Creditos,
    [param: Range(0, 2000)] int Horas,
    [param: Range(1, 20)] int Semestre,
    IReadOnlyCollection<UnidadTematicaRequest>? Unidades = null);

public record ActualizarAsignaturaRequest(
    [param: Required, MaxLength(200)] string Nombre,
    [param: Range(0, 30)] int Creditos,
    [param: Range(0, 2000)] int Horas,
    [param: Range(1, 20)] int Semestre,
    IReadOnlyCollection<UnidadTematicaRequest>? Unidades = null);

/// <summary>Asignatura dentro de la carga masiva; la malla proviene del contenedor de la petición.</summary>
public record AsignaturaImportItemRequest(
    [param: Required, MaxLength(30)] string Codigo,
    [param: Required, MaxLength(200)] string Nombre,
    [param: Range(0, 30)] int Creditos,
    [param: Range(0, 2000)] int Horas,
    [param: Range(1, 20)] int Semestre,
    IReadOnlyCollection<UnidadTematicaRequest>? Unidades = null);

/// <summary>Carga masiva del catálogo estructurado (43 planes analíticos de Software).</summary>
public record ImportarCatalogoRequest(
    [param: Range(1, int.MaxValue)] int MallaId,
    IReadOnlyCollection<AsignaturaImportItemRequest> Asignaturas);

public record ImportacionResultadoDto(
    int Total,
    int Importadas,
    int Rechazadas,
    IReadOnlyCollection<string> Errores);
