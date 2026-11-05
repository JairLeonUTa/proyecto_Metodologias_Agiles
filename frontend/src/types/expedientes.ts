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
}

/** Respuesta 202 de `POST /api/expedientes/certificado`. */
export interface SolicitudCertificado {
  expediente: Expediente;
  certificado: CertificadoLeido;
  publicado: boolean;
  mensaje: string;
}
