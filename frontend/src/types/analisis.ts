/**
 * Contratos del Microservicio C — Agente analizador (Sprint 2, US-07 a US-11).
 * Reflejan `Siha.MsC.Api.Contracts.Dtos` tal como los serializa ASP.NET Core (camelCase).
 */

/** Estados de una extracción documental (`EstadosExtraccion` en MS C). */
export const ESTADOS_EXTRACCION = {
  PENDIENTE_CONFIRMACION: 'PENDIENTE_CONFIRMACION',
  CONFIRMADA: 'CONFIRMADA',
  PENDIENTE_OCR_LLM: 'PENDIENTE_OCR_LLM',
  /** PDF sin texto y OCR/LLM no operativo: extracción cerrada sin datos. */
  CANCELADA: 'CANCELADA',
} as const;

export type EstadoExtraccion = (typeof ESTADOS_EXTRACCION)[keyof typeof ESTADOS_EXTRACCION];

/** Umbral de confianza para auto-confirmación (CA2 de US-07). */
export const UMBRAL_CONFIANZA = 0.8;

export interface UnidadExtraccion {
  numero: number;
  nombre: string;
  horas: number;
  contenidos: string;
}

/** `GET /api/extracciones/{id}` y `GET /api/extracciones/bandeja`. */
export interface Extraccion {
  id: string;
  expedienteId: string;
  documentoId: string | null;
  codigoAsignaturaOrigen: string;
  nombreAsignaturaOrigen: string;
  creditos: number | null;
  horas: number | null;
  /** Confianza global en [0, 1]. */
  confianza: number;
  estado: string;
  requiereConfirmacion: boolean;
  unidades: UnidadExtraccion[];
  creadoUtc: string;
  confirmadoUtc: string | null;
  confirmadoPor: string | null;
  observacion: string | null;
}

/** Cuerpo de `PUT /api/extracciones/{id}/confirmar` (S2-J02). */
export interface ConfirmarExtraccion {
  creditos: number;
  horas: number;
  unidades: Array<{ numero: number; nombre: string; horas: number; contenidos?: string }>;
  observacion?: string | null;
}

/** Resultado de Pcred y controles normativos (US-09). */
export interface EvaluacionCreditos {
  /** Porcentaje 0–100. */
  pcred: number;
  /** NORMAL, VALIDACION_CONOCIMIENTOS o BLOQUEO_3RA_MATRICULA. */
  categoria: string;
  alertas: string[];
  bloqueoDuro: boolean;
  detalleCalculo: string;
}

export interface UnidadCobertura {
  numero: number;
  nombre: string;
  horas: number;
  /** Similitud coseno en porcentaje (0–100). */
  similitudMaxima: number;
  cubierta: boolean;
  unidadesOrigenContribuyentes: number[];
}

/** Resultado de Pcont y desglose por unidad (US-10). */
export interface EvaluacionContenido {
  /** Porcentaje 0–100. */
  pcont: number;
  unidadesCubiertas: UnidadCobertura[];
  unidadesFaltantes: UnidadCobertura[];
  /** Umbral τ en porcentaje (0–100). */
  tauUsado: number;
  detalleCalculo: string;
  violacionRN19: boolean;
  unidadesOrigenDuplicadas: number[];
}

export interface PrecedenteAsignaturaOrigen {
  codigo: string;
  nombre: string;
}

/** Precedente con coincidencia exacta (US-11). */
export interface Precedente {
  id: string;
  resolucion: string;
  asignaturasOrigen: PrecedenteAsignaturaOrigen[];
  asignaturaDestino: string;
  institucionOrigen: string;
  /** DateOnly serializado como `yyyy-MM-dd`. */
  fecha: string;
}

export interface BuscarPrecedente {
  codigosAsignaturasOrigen: string[];
  codigoAsignaturaDestino: string;
  institucionOrigen: string;
}

export interface UnidadDestino {
  numero: number;
  nombre: string;
  horas: number;
  contenidos: string;
  /** Vacío: MS C genera el embedding con Ollama (S2-A08). */
  embedding: number[];
}

/** Cuerpo de `POST /api/extracciones/{id}/analizar` (S2-J09). */
export interface AnalizarExtraccion {
  anosTranscurridos: number;
  numeroMatriculaDestino: number;
  esCreditoExterno: boolean;
  rutaNormativa: string;
  codigoAsignaturaDestino: string;
  institucionOrigen: string;
  creditosDestino: number;
  unidadesDestino: UnidadDestino[];
  validarRN19?: boolean;
}

/** `EstadoEvaluacionFinal` (serializado como número). */
export const ESTADO_FINAL = {
  LISTA_PARA_DICTAMEN: 0,
  BLOQUEO_TERCERA_MATRICULA: 1,
  BLOQUEO_RN19: 2,
  REQUIERE_VALIDACION_CONOCIMIENTOS: 3,
  ERROR_REGLAS_GRPC: 4,
  EXTRACCION_NO_CONFIRMADA: 5,
} as const;

export interface AnalisisCompleto {
  extraccion: Extraccion;
  creditos: EvaluacionCreditos;
  contenido: EvaluacionContenido;
  precedentes: Precedente[];
  estadoFinal: number;
  alertasGlobales: string[];
  detalleTrazabilidad: string;
}
