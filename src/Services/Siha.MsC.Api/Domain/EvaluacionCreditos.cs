namespace Siha.MsC.Api.Domain;

/// <summary>Resultado del cálculo de proporción de créditos (Pcred) y controles normativos (US-09).</summary>
public sealed record EvaluacionCreditos(
    /// <summary>Proporción de créditos: min(100, (credOrigen / credDestino) * 100).</summary>
    decimal Pcred,

    /// <summary>Categoría normativa asignada (ej. "VALIDACION_CONOCIMIENTOS", "BLOQUEO_3RA_MATRICULA", "NORMAL").</summary>
    string Categoria,

    /// <summary>Alertas normativas para el docente (solo informativas, no bloquean el cálculo salvo bloqueos duros).</summary>
    IReadOnlyCollection<string> Alertas,

    /// <summary>Indica si la evaluación tiene un bloqueo duro que impide continuar (RN-16: 3ra matrícula).</summary>
    bool BloqueoDuro,

    /// <summary>Detalle de los parámetros usados para trazabilidad.</summary>
    string DetalleCalculo
);

/// <summary>Parámetros de entrada para el cálculo de Pcred y controles normativos.</summary>
public sealed record ParametrosCreditos(
    /// <summary>Créditos de la asignatura de origen (extraídos del PDF y confirmados por operador).</summary>
    int CreditosOrigen,

    /// <summary>Créditos de la asignatura de destino (del catálogo vigente).</summary>
    int CreditosDestino,

    /// <summary>Años transcurridos desde que se cursó la asignatura de origen.</summary>
    int AnosTranscurridos,

    /// <summary>Número de matrícula en la malla destino (1 = primera, 2 = segunda, 3 = tercera...).</summary>
    int NumeroMatriculaDestino,

    /// <summary>Indica si los créditos de origen son externos (otra institución).</summary>
    bool EsCreditoExterno,

    /// <summary>Ruta normativa aplicable (CAU-P-011 = cambio carrera, CAU-P-122 = movilidad).</summary>
    string RutaNormativa
);