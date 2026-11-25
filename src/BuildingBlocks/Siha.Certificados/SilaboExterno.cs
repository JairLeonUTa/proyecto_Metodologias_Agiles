namespace Siha.Certificados;

/// <summary>
/// Sílabo o plan analítico de una materia aprobada en OTRA universidad (movilidad externa).
/// Se adjunta junto al certificado SIHA para que el agente pueda comparar sus unidades y temas
/// contra el sílabo oficial de la UTA. Sin este documento la materia queda "Sin sílabo para comparar".
/// </summary>
public sealed record SilaboExterno(
    /// <summary>Materia aprobada del certificado a la que corresponde este sílabo.</summary>
    string Materia,
    /// <summary>Nombre de la asignatura tal como aparece en el sílabo (puede diferir del certificado).</summary>
    string? NombreEnSilabo,
    int? Creditos,
    /// <summary>Horas totales de la materia (suma de sus unidades).</summary>
    int HorasTotales,
    IReadOnlyList<UnidadSilabo> Unidades);

/// <summary>Unidad temática del sílabo externo con sus temas y horas.</summary>
public sealed record UnidadSilabo(int Numero, string Nombre, int Horas, string Contenidos);

/// <summary>
/// Resultado de leer un sílabo externo. Si <see cref="EsValido"/> es falso, <see cref="Errores"/>
/// explica en lenguaje sencillo qué le falta al documento para poder compararlo.
/// </summary>
public sealed record ResultadoLecturaSilabo(
    bool EsValido,
    SilaboExterno? Silabo,
    IReadOnlyList<string> Errores)
{
    public static ResultadoLecturaSilabo Invalido(IEnumerable<string> errores)
        => new(false, null, errores.ToList());

    public static ResultadoLecturaSilabo Valido(SilaboExterno silabo)
        => new(true, silabo, Array.Empty<string>());
}
