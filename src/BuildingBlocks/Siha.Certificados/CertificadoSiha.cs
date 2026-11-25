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
/// <see cref="Problemas"/> trae los mismos mensajes clasificados por sección y tipo, para mostrar una
/// lista de verificación al operador.
/// </summary>
public sealed record ResultadoLecturaCertificado(
    bool EsValido,
    CertificadoSiha? Certificado,
    IReadOnlyList<string> Errores)
{
    public IReadOnlyList<ProblemaCertificado> Problemas { get; init; } = Array.Empty<ProblemaCertificado>();

    /// <summary>El archivo no es el certificado SIHA (no es PDF, está escaneado u otra plantilla).</summary>
    public static ResultadoLecturaCertificado NoEsCertificado(string mensaje)
        => Invalido(new[] { new ProblemaCertificado(SeccionesCertificado.Documento, TiposProblema.NoEsCertificado, mensaje) });

    public static ResultadoLecturaCertificado Invalido(IEnumerable<ProblemaCertificado> problemas)
    {
        var lista = problemas.GroupBy(p => p.Mensaje).Select(g => g.First()).ToList();
        return new(false, null, lista.Select(p => p.Mensaje).ToList()) { Problemas = lista };
    }

    public static ResultadoLecturaCertificado Valido(CertificadoSiha certificado)
        => new(true, certificado, Array.Empty<string>());
}

/// <summary>Un problema del certificado: en qué sección está, de qué tipo es y qué hay que corregir.</summary>
public sealed record ProblemaCertificado(string Seccion, string Tipo, string Mensaje);

/// <summary>Secciones del certificado, en el orden en que aparecen en la lista de verificación.</summary>
public static class SeccionesCertificado
{
    /// <summary>El archivo en sí (solo para "no es el certificado SIHA").</summary>
    public const string Documento = "Documento";
    public const string Certificado = "Certificado";
    public const string Estudiante = "Estudiante";
    public const string Tramite = "Trámite";
    public const string Revisores = "Revisores";
    public const string Historial = "Historial";
    public const string Solicitudes = "Solicitudes";

    /// <summary>Secciones que se muestran en la lista de verificación (sin "Documento").</summary>
    public static readonly string[] Verificables = { Certificado, Estudiante, Tramite, Revisores, Historial, Solicitudes };
}

/// <summary>Tipos de problema, para agrupar los mensajes en lenguaje sencillo.</summary>
public static class TiposProblema
{
    /// <summary>"Este PDF no es el certificado SIHA": otra plantilla, escaneado o imagen.</summary>
    public const string NoEsCertificado = "NO_ES_CERTIFICADO";

    /// <summary>"El certificado tiene campos sin llenar": falta la etiqueta, está vacía o tiene el texto de ejemplo.</summary>
    public const string CampoVacio = "CAMPO_VACIO";

    /// <summary>"Datos que no cumplen las reglas": nota incoherente, siglas, revisor que no existe, etc.</summary>
    public const string Regla = "REGLA";
}
