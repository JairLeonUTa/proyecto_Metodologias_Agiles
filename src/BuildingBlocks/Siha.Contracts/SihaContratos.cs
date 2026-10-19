namespace Siha.Contracts;

/// <summary>
/// Estados del ciclo de vida del expediente (Sprint 1 - US-05 / US-06).
/// El expediente NUNCA se presenta como evaluado antes de que el Microservicio C produzca un resultado.
/// </summary>
public static class ExpedienteEstados
{
    /// <summary>El expediente fue recibido y validado, pero aún no se publicó en el bus.</summary>
    public const string Registrado = "REGISTRADO";

    /// <summary>El evento fue publicado; el operador puede continuar mientras C procesa.</summary>
    public const string PendienteContenidos = "PENDIENTE_CONTENIDOS";

    /// <summary>El proceso de extracción/análisis está en curso en el Microservicio C.</summary>
    public const string EnProceso = "EN_PROCESO";

    /// <summary>Estado recuperable: la persistencia se completó pero la publicación en RabbitMQ falló (CA3 de US-06).</summary>
    public const string FalloPublicacion = "REGISTRADO_PENDIENTE_PUBLICACION";

    /// <summary>El expediente cuenta con un dictamen preliminar emitido.</summary>
    public const string Evaluado = "EVALUADO";
}

/// <summary>Rutas normativas institucionales soportadas (Resoluciones CAU).</summary>
public static class RutasNormativas
{
    /// <summary>Cambio de carrera dentro de la misma universidad.</summary>
    public const string CambioCarreraOu = "CAU-P-011";

    /// <summary>Movilidad / homologación desde otra universidad.</summary>
    public const string Movilidad = "CAU-P-122";

    public static readonly string[] Todas = { CambioCarreraOu, Movilidad };
}

/// <summary>Tipos de trámite que el operador puede registrar.</summary>
public static class TiposTramite
{
    public const string Interno = "INTERNO";
    public const string Externo = "EXTERNO";

    public static readonly string[] Todos = { Interno, Externo };
}

/// <summary>Roles del sistema (control de acceso basado en roles - RBAC).</summary>
public static class RolesSistema
{
    public const string Administrador = "ADMINISTRADOR";
    public const string Docente = "DOCENTE";
    public const string Operador = "OPERADOR";

    public static readonly string[] Todos = { Administrador, Docente, Operador };
}
