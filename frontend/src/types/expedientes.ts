/** Contratos de expedientes (Microservicio B — `/api/expedientes`). */

export interface DocumentoExpediente {
  id?: string;
  nombreArchivo: string;
  rutaAlmacenamiento: string;
  hashSha256: string | null;
  tamanoBytes: number;
  fechaCargaUtc?: string;
}

export interface AsignaturaExpediente {
  codigoOrigen: string;
  nombreOrigen: string;
  codigoDestino: string | null;
  nota: number | null;
  fechaAprobacionCertificada?: string | null;
  numeroMatriculaDestino?: number | null;
}

/** Cuerpo de `POST /api/expedientes`. */
export interface CrearExpediente {
  tipoTramite: string;
  rutaNormativa: string;
  carreraOrigenCodigo: string;
  carreraDestinoCodigo: string;
  institucionOrigen: string | null;
  asignaturas: AsignaturaExpediente[];
}

/** Metadatos de un documento devueltos por el servidor (hash, ruta y tamaño calculados en B). */
export interface DocumentoCargado {
  id: string;
  nombreArchivo: string;
  nombreAlmacenado: string;
  rutaAlmacenamiento: string;
  hashSha256: string;
  tamanoBytes: number;
  mimeType: string;
  /** VALIDADO, RECHAZADO, RETIRADO o NO_DISPONIBLE. */
  estadoRecepcion: string;
  observacionValidacion: string | null;
  fechaCargaUtc: string;
}

/** Respuesta de `GET /api/expedientes/{id}/documentos`. */
export interface DocumentosExpediente {
  expedienteId: string;
  codigoExpediente: string;
  estadoExpediente: string;
  totalDocumentos: number;
  documentosValidados: number;
  documentosRechazados: number;
  documentos: DocumentoCargado[];
}

/** Respuesta 202 Accepted de `POST /api/expedientes` y `POST /api/expedientes/{id}/republicar`. */
export interface ExpedienteAceptado {
  id: string;
  codigo: string;
  estado: string;
  correlationId: string;
  publicado: boolean;
  mensaje: string;
}

/** Respuesta de `GET /api/expedientes/{id}` y de `GET /api/expedientes`. */
export interface Expediente {
  id: string;
  codigo: string;
  tipoTramite: string;
  rutaNormativa: string;
  carreraOrigenCodigo: string;
  carreraDestinoCodigo: string;
  institucionOrigen?: string | null;
  estado: string;
  correlationId: string;
  eventoPublicado: boolean;
  intentosPublicacion: number;
  ultimoErrorPublicacion: string | null;
  fechaCreacionUtc: string;
  fechaPublicacionUtc: string | null;
  documentos: DocumentoCargado[];
  asignaturas: Array<{
    codigoOrigen: string;
    nombreOrigen: string;
    codigoDestino: string | null;
    nota: number | null;
    fechaAprobacionCertificada?: string | null;
    numeroMatriculaDestino?: number | null;
    nivel?: number | null;
    periodoAcademico?: string | null;
    aprobada?: boolean | null;
    asignaturaDestinoNombre?: string | null;
    solicitudNumero?: number | null;
  }>;
  /** Datos del certificado SIHA del que se creó el expediente (nulos en registros manuales antiguos). */
  numeroCertificado?: string | null;
  estudianteNombre?: string | null;
  estudianteIdentificacion?: string | null;
  revisores?: Array<{ nombre: string; correo: string }> | null;
  carreraOrigenNombre?: string | null;
  carreraDestinoNombre?: string | null;
}

/** Materia del historial académico del certificado (aprobada o reprobada). */
export interface MateriaCursada {
  nombre: string;
  nivel: number;
  periodoAcademico: string;
  nota: number;
  aprobada: boolean;
}

/** Materia(s) aprobada(s) que el estudiante pide homologar por una materia de la carrera de destino. */
export interface SolicitudHomologacion {
  numero: number;
  materiasOrigen: string[];
  materiaDestino: string;
}

/** Respuesta de `POST /api/expedientes/certificado/validar`: lo que el sistema leyó del PDF. */
export interface CertificadoLeido {
  numeroCertificado: string;
  fechaEmision: string;
  emitidoPor: string;
  estudianteNombre: string;
  estudianteIdentificacion: string;
  tipoTramite: string;
  rutaNormativa: string;
  institucionOrigen: string;
  carreraOrigen: string;
  carreraDestino: string;
  materias: MateriaCursada[];
  solicitudes: SolicitudHomologacion[];
  /** Docentes revisores (usuarios DOCENTE verificados en MS A). */
  revisores: Array<{ nombre: string; correo: string }>;
  /** Sílabos de la otra universidad adjuntados y leídos (solo movilidad externa). */
  silabos?: SilaboLeido[] | null;
  /** Materias de destino que no aparecen en la malla vigente, con el nombre oficial sugerido. */
  avisos?: AvisoMateriaDestino[] | null;
}

/** Aviso: la materia de destino no está en la malla vigente (no impide registrar). */
export interface AvisoMateriaDestino {
  solicitud: number;
  materiaDestino: string;
  sugerencia?: string | null;
  mensaje: string;
}

/** Tipo de rechazo del certificado, para resumirlo en lenguaje sencillo. */
export type TipoRechazoCertificado = 'NO_ES_CERTIFICADO' | 'CAMPO_VACIO' | 'REGLA' | 'CAMPOS_Y_REGLAS';

/** Fila de la lista de verificación: la sección del certificado está bien o tiene problemas. */
export interface SeccionVerificada {
  seccion: string;
  ok: boolean;
  problemas: string[];
}

/** Cuerpo del 422 cuando el certificado no se puede registrar. */
export interface RechazoCertificado {
  mensaje: string;
  tipo: TipoRechazoCertificado;
  detalles: string[];
  problemas: Array<{ seccion: string; tipo: string; mensaje: string }>;
  secciones: SeccionVerificada[];
}

/** Sílabo externo que el operador adjunta para una materia aprobada (movilidad externa). */
export interface SilaboAdjunto {
  materia: string;
  archivo: File;
}

/** Resumen de un sílabo externo leído por el servidor: unidades y horas encontradas. */
export interface SilaboLeido {
  materia: string;
  nombreEnSilabo?: string | null;
  creditos?: number | null;
  unidades: number;
  horasTotales: number;
}

/** Respuesta 202 de `POST /api/expedientes/certificado`. */
export interface SolicitudCertificado {
  expediente: Expediente;
  certificado: CertificadoLeido;
  publicado: boolean;
  mensaje: string;
}

/** Materia del historial en el registro manual. */
export interface MateriaManual {
  nombre: string;
  nivel: number | null;
  periodoAcademico: string;
  nota: number | null;
  aprobada: boolean;
}

/** Materia de destino y las aprobadas con las que se pide homologarla. */
export interface SolicitudManual {
  materiasOrigen: string[];
  materiaDestino: string;
}

/** Registro manual de una solicitud (sin PDF): los mismos datos del certificado SIHA. */
export interface RegistroManual {
  estudianteNombre: string;
  cedula: string;
  tipoTramite: string;
  institucionOrigen: string;
  carreraOrigen: string;
  carreraDestino: string;
  revisor1Correo: string;
  revisor2Correo: string;
  materias: MateriaManual[];
  solicitudes: SolicitudManual[];
}

export interface OpcionesRegistroManual {
  docentes: { nombre: string; correo: string }[];
  materiasOrigen: string[];
  materiasDestino: string[];
}
