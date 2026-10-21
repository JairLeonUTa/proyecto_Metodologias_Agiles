/** Constantes compartidas del dominio SIHA-IA. */

/** Roles del sistema (RBAC, Microservicio A). */
export const ROLES = {
  ADMINISTRADOR: 'ADMINISTRADOR',
  DOCENTE: 'DOCENTE',
  OPERADOR: 'OPERADOR',
} as const;

export type RolNombre = (typeof ROLES)[keyof typeof ROLES];

export const ROLES_TODOS: RolNombre[] = [ROLES.ADMINISTRADOR, ROLES.DOCENTE, ROLES.OPERADOR];

/** Descripción legible de cada rol, usada en menús e insignias. */
export const ETIQUETA_ROL: Record<string, string> = {
  ADMINISTRADOR: 'Administrador',
  DOCENTE: 'Docente',
  OPERADOR: 'Operador',
};

/** Rutas normativas institucionales (resoluciones CAU). */
export const RUTAS_NORMATIVAS = {
  CAMBIO_CARRERA_OU: 'CAU-P-011',
  MOVILIDAD: 'CAU-P-122',
} as const;

export type RutaNormativa = (typeof RUTAS_NORMATIVAS)[keyof typeof RUTAS_NORMATIVAS];

export const RUTAS_NORMATIVAS_TODAS: RutaNormativa[] = [
  RUTAS_NORMATIVAS.CAMBIO_CARRERA_OU,
  RUTAS_NORMATIVAS.MOVILIDAD,
];

export const DESCRIPCION_RUTA: Record<string, string> = {
  'CAU-P-011': 'Cambio de carrera dentro de la Universidad Técnica de Ambato',
  'CAU-P-122': 'Movilidad / homologación desde otra institución de educación superior',
};

/** Tipos de trámite de una solicitud de homologación. */
export const TIPOS_TRAMITE = {
  INTERNO: 'INTERNO',
  EXTERNO: 'EXTERNO',
} as const;

export type TipoTramite = (typeof TIPOS_TRAMITE)[keyof typeof TIPOS_TRAMITE];

export const TIPOS_TRAMITE_TODOS: TipoTramite[] = [TIPOS_TRAMITE.INTERNO, TIPOS_TRAMITE.EXTERNO];

export const ETIQUETA_TIPO_TRAMITE: Record<string, string> = {
  INTERNO: 'Interna (misma universidad)',
  EXTERNO: 'Externa (otra institución)',
};

/** Estados del ciclo de vida del expediente (coincide con ExpedienteEstados en Siha.Contracts). */
export const ESTADOS_EXPEDIENTE = {
  REGISTRADO: 'REGISTRADO',
  /** Borrador: expediente existe en DB B y espera la carga y validación de sus PDF en el servidor. */
  PENDIENTE_DOCUMENTOS: 'PENDIENTE_DOCUMENTOS',
  /** Transición protegida por concurrencia optimista: un único envío reservó el expediente y está publicando el evento. */
  PUBLICACION_EN_CURSO: 'PUBLICACION_EN_CURSO',
  /** El evento fue publicado; el operador puede continuar mientras C procesa. */
  PENDIENTE_CONTENIDOS: 'PENDIENTE_CONTENIDOS',
  EN_PROCESO: 'EN_PROCESO',
  /** Estado recuperable: la persistencia se completó pero la publicación en RabbitMQ falló (CA3 de US-06). */
  REGISTRADO_PENDIENTE_PUBLICACION: 'REGISTRADO_PENDIENTE_PUBLICACION',
  EVALUADO: 'EVALUADO',
} as const;

export type EstadoExpediente = (typeof ESTADOS_EXPEDIENTE)[keyof typeof ESTADOS_EXPEDIENTE];

export const ESTADOS_EXPEDIENTE_TODOS: EstadoExpediente[] = [
  ESTADOS_EXPEDIENTE.REGISTRADO,
  ESTADOS_EXPEDIENTE.PENDIENTE_DOCUMENTOS,
  ESTADOS_EXPEDIENTE.PUBLICACION_EN_CURSO,
  ESTADOS_EXPEDIENTE.PENDIENTE_CONTENIDOS,
  ESTADOS_EXPEDIENTE.EN_PROCESO,
  ESTADOS_EXPEDIENTE.REGISTRADO_PENDIENTE_PUBLICACION,
  ESTADOS_EXPEDIENTE.EVALUADO,
];

export const ETIQUETA_ESTADO: Record<string, string> = {
  REGISTRADO: 'Registrado',
  PENDIENTE_DOCUMENTOS: 'Borrador — pendiente de documentos',
  PUBLICACION_EN_CURSO: 'Publicación en curso',
  PENDIENTE_CONTENIDOS: 'Pendiente de contenidos',
  EN_PROCESO: 'En proceso',
  REGISTRADO_PENDIENTE_PUBLICACION: 'Registrado — pendiente de publicación',
  EVALUADO: 'Evaluado',
};

/** Estado de recepción de cada documento PDF en el Microservicio B. */
export const ETIQUETA_ESTADO_DOCUMENTO: Record<string, string> = {
  VALIDADO: 'Validado',
  RECHAZADO: 'Rechazado',
  RETIRADO: 'Retirado',
  NO_DISPONIBLE: 'No disponible',
};

/** Reglas de validación de archivos PDF (S1-A07, validación local). */
export const PDF = {
  /** Tipo MIME esperado. */
  MIME: 'application/pdf',
  /** Extensión admitida. */
  EXTENSION: '.pdf',
  /** Tamaño máximo permitido por archivo: 10 MB. */
  TAMANO_MAXIMO_BYTES: 10 * 1024 * 1024,
  /** Cantidad máxima de documentos por expediente. */
  MAXIMO_ARCHIVOS: 10,
} as const;

/** Horas por crédito en la UTA (regla del catálogo académico). */
export const HORAS_POR_CREDITO = 48;

/**
 * Instituciones de educación superior frecuentes como origen de una homologación
 * externa. El selector permite además especificar una institución distinta.
 */
export const INSTITUCIONES_ORIGEN: string[] = [
  'Universidad Técnica de Ambato',
  'Escuela Superior Politécnica de Chimborazo',
  'Escuela Politécnica Nacional',
  'Universidad Central del Ecuador',
  'Universidad Nacional de Chimborazo',
  'Pontificia Universidad Católica del Ecuador',
  'Universidad de las Fuerzas Armadas ESPE',
  'Universidad Técnica del Norte',
  'Universidad Nacional de Loja',
  'Universidad de Cuenca',
  'Universidad Estatal de Milagro',
  'Universidad Técnica de Machala',
];

/** Valor del selector que habilita el ingreso manual del nombre de la institución. */
export const INSTITUCION_OTRA = '__OTRA_INSTITUCION__';

