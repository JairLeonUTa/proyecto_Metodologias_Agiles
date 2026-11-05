namespace Siha.Certificados;

/// <summary>
/// Certificado de homologación SIHA (formato oficial <c>SIHA-CH-01</c>).
///
/// Es el único documento que se acepta para registrar una solicitud. Llega preaprobado por la
/// secretaría y trae: el estudiante, el trámite, las carreras con su nombre completo, el historial
/// académico (materias aprobadas y reprobadas con nivel, período académico y nota), qué materia(s)
/// aprobada(s) quiere homologar por cuál materia de la carrera de destino y los dos docentes revisores.
/// Los contenidos, horas y créditos NO van en el certificado: el agente los toma de las mallas y sílabos
/// oficiales que conoce.
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
    string CarreraOrigen,
    string CarreraDestino,
    IReadOnlyList<MateriaCursada> Materias,
    IReadOnlyList<SolicitudHomologacion> Solicitudes,
    IReadOnlyList<RevisorCertificado> Revisores);

/// <summary>Materia del historial académico (aprobada o reprobada).</summary>
public sealed record MateriaCursada(string Nombre, int Nivel, string PeriodoAcademico, decimal Nota, bool Aprobada);

/// <summary>Una o varias materias aprobadas que el estudiante pide homologar por una materia de destino.</summary>
public sealed record SolicitudHomologacion(int Numero, IReadOnlyList<string> MateriasOrigen, string MateriaDestino);

/// <summary>
/// Docente revisor asignado en el certificado. Debe existir en MS A como usuario activo con rol DOCENTE
/// (lo verifica MS B por gRPC al registrar).
/// </summary>
public sealed record RevisorCertificado(string Nombre, string Correo);

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
