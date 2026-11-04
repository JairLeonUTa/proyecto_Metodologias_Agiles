namespace Siha.Certificados;

/// <summary>
/// Certificado de homologación SIHA (formato oficial <c>SIHA-CH-01</c>).
///
/// Es el único documento que se acepta para registrar una solicitud: llega preaprobado por la
/// secretaría y trae todo lo necesario para homologar (estudiante, trámite, carreras y cada
/// asignatura aprobada con semestre, créditos, horas, fecha, nota, matrícula y unidades temáticas).
/// Nadie vuelve a digitar esos datos en el sistema.
/// </summary>
public sealed record CertificadoSiha(
    string NumeroCertificado,
    DateOnly FechaEmision,
    string EmitidoPor,
    string EstudianteNombre,
    string EstudianteIdentificacion,
    string TipoTramite,
    string RutaNormativa,
    string InstitucionOrigen,
    string CarreraOrigenCodigo,
    string CarreraOrigenNombre,
    string CarreraDestinoCodigo,
    string CarreraDestinoNombre,
    IReadOnlyList<AsignaturaCertificada> Asignaturas,
    IReadOnlyList<RevisorCertificado> Revisores);

/// <summary>
/// Docente revisor asignado en el certificado. Debe existir en MS A como usuario activo con rol DOCENTE
/// (lo verifica MS B por gRPC al registrar).
/// </summary>
public sealed record RevisorCertificado(string Nombre, string Correo);

/// <summary>Asignatura aprobada que consta en el certificado, con su plan analítico resumido.</summary>
public sealed record AsignaturaCertificada(
    string Codigo,
    string Nombre,
    int Semestre,
    int Creditos,
    int HorasTotales,
    DateOnly FechaAprobacion,
    decimal Nota,
    IReadOnlyList<UnidadCertificada> Unidades);

/// <summary>Unidad temática de una asignatura aprobada (nombre, horas y contenidos).</summary>
public sealed record UnidadCertificada(int Numero, string Nombre, int Horas, string Contenidos);

/// <summary>
/// Resultado de leer un PDF con el formato SIHA. Si <see cref="EsValido"/> es falso,
/// <see cref="Errores"/> explica cada incumplimiento y el documento debe rechazarse.
/// </summary>
public sealed record ResultadoLecturaCertificado(
    bool EsValido,
    CertificadoSiha? Certificado,
    IReadOnlyList<string> Errores)
{
    public static ResultadoLecturaCertificado Invalido(IEnumerable<string> errores)
        => new(false, null, errores.ToList());

    public static ResultadoLecturaCertificado Valido(CertificadoSiha certificado)
        => new(true, certificado, Array.Empty<string>());
}
