namespace Siha.MsC.Api.Domain;

/// <summary>Resultado de la evaluación de similitud semántica de contenidos (US-10, S2-J06/J07).</summary>
public sealed record EvaluacionContenido(
    /// <summary>Porcentaje de cobertura temática ponderado por horas: Pcont = Σ(h_j * covered_j) / Σ(h_j) * 100.</summary>
    decimal Pcont,

    /// <summary>Unidades de destino cubiertas (similitud ≥ τ).</summary>
    IReadOnlyCollection<UnidadCobertura> UnidadesCubiertas,

    /// <summary>Unidades de destino NO cubiertas (similitud < τ).</summary>
    IReadOnlyCollection<UnidadCobertura> UnidadesFaltantes,

    /// <summary>Umbral τ (tau) usado para decidir cobertura (viene de gRPC ReglasGrpc).</summary>
    double TauUsado,

    /// <summary>Detalle de trazabilidad del cálculo.</summary>
    string DetalleCalculo,

    /// <summary>Validación RN-19: true si alguna unidad de origen se usa en más de una destino cubierto.</summary>
    bool ViolacionRN19,

    /// <summary>Unidades de origen que violan RN-19 (aparecen en múltiples destinos cubiertos).</summary>
    IReadOnlyCollection<int> UnidadesOrigenDuplicadas
);

/// <summary>Cobertura de una unidad de destino por las unidades de origen.</summary>
public sealed record UnidadCobertura(
    int Numero,
    string Nombre,
    int Horas,
    /// <summary>Mejor similitud coseno encontrada entre esta unidad de destino y cualquier unidad de origen.</summary>
    double SimilitudMaxima,
    /// <summary>True si SimilitudMaxima ≥ TauUsado.</summary>
    bool Cubierta,
    /// <summary>Números de las unidades de origen que contribuyen (para N:1).</summary>
    IReadOnlyCollection<int> UnidadesOrigenContribuyentes
);

/// <summary>Parámetros de entrada para la evaluación de contenidos.</summary>
public sealed record ParametrosContenido(
    /// <summary>Unidades de origen (extraídas del PDF y confirmadas por operador).</summary>
    IReadOnlyCollection<UnidadOrigenEmbedding> UnidadesOrigen,

    /// <summary>Unidades de destino (del plan analítico en catálogo, con embeddings precalculados).</summary>
    IReadOnlyCollection<UnidadDestinoEmbedding> UnidadesDestino,

    /// <summary>Umbral τ (tau) vigente para considerar una unidad como cubierta (0-100, ej. 75).</summary>
    double Tau,

    /// <summary>Ruta normativa para trazabilidad.</summary>
    string RutaNormativa,

    /// <summary>Si true, valida RN-19 (no reutilizar origen en múltiples destinos). Default true.</summary>
    bool ValidarRN19 = true
);

/// <summary>Unidad de origen con su embedding vectorial.</summary>
public sealed record UnidadOrigenEmbedding(
    int Numero,
    string Nombre,
    int Horas,
    string Contenidos,
    float[] Embedding
);

/// <summary>Unidad de destino con su embedding vectorial precalculado.</summary>
public sealed record UnidadDestinoEmbedding(
    int Numero,
    string Nombre,
    int Horas,
    string Contenidos,
    float[] Embedding
);