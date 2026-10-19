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
  }>;
}
