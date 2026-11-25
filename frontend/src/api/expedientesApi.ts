import { peticion } from './httpClient';
import type {
  AfinidadManual,
  CertificadoLeido,
  ConsultaAfinidad,
  CrearExpediente,
  DocumentoCargado,
  DocumentosExpediente,
  Expediente,
  ExpedienteAceptado,
  OpcionesRegistroManual,
  RegistroManual,
  SilaboAdjunto,
  SolicitudCertificado,
} from '../types/expedientes';

function formularioPdf(archivo: File, silabos: SilaboAdjunto[] = []): FormData {
  const formulario = new FormData();
  formulario.append('archivo', archivo, archivo.name);
  for (const silabo of silabos) {
    formulario.append('silabos', silabo.archivo, silabo.archivo.name);
    formulario.append('silabosMaterias', silabo.materia);
  }
  return formulario;
}

/**
 * Registro, carga de documentos, envío y consulta de expedientes (S1-A06/A07/A09/A10).
 *
 * Flujo acordado con el Microservicio B:
 *  1. `registrar` crea el expediente como borrador (PENDIENTE_DOCUMENTOS); no publica nada.
 *  2. `subirDocumento` envía cada PDF real (multipart); el servidor lo valida, lo almacena y calcula su hash.
 *  3. `enviar` pide al servidor verificar los PDF en disco y publicar el evento si se cumplen los requisitos.
 */
export const expedientesApi = {
  /**
   * Lee el certificado SIHA sin registrar nada. 422 con `detalles` si no cumple la plantilla.
   * En movilidad externa también lee los sílabos de la otra universidad que se adjunten.
   */
  validarCertificado: (archivo: File, silabos: SilaboAdjunto[] = []) =>
    peticion<CertificadoLeido>('/api/expedientes/certificado/validar', {
      metodo: 'POST',
      cuerpo: formularioPdf(archivo, silabos),
    }),

  /** Registra y envía la solicitud a partir del certificado SIHA (más los sílabos externos adjuntos). */
  registrarCertificado: (archivo: File, silabos: SilaboAdjunto[] = []) =>
    peticion<SolicitudCertificado>('/api/expedientes/certificado', {
      metodo: 'POST',
      cuerpo: formularioPdf(archivo, silabos),
    }),

  /** Vuelve a mandar al agente una solicitud que nunca le llegó (por ejemplo, si estaba apagado). */
  reenviarAgente: (expedienteId: string) =>
    peticion<unknown>(`/api/expedientes/${expedienteId}/reenviar-agente`, { metodo: 'POST' }),

  /** Docentes revisores y materias de las carreras, para el registro manual. */
  opcionesManual: (carreraOrigen = '', carreraDestino = '') =>
    peticion<OpcionesRegistroManual>(
      `/api/expedientes/manual/opciones?carreraOrigen=${encodeURIComponent(carreraOrigen)}&carreraDestino=${encodeURIComponent(carreraDestino)}`,
    ),

  /** Sugiere con qué materia homologar cada aprobada y avisa si lo pedido no se relaciona (no registra nada). */
  afinidadManual: (consulta: ConsultaAfinidad) =>
    peticion<AfinidadManual>('/api/expedientes/manual/afinidad', { metodo: 'POST', cuerpo: consulta }),

  /** Revisa el registro manual con las mismas reglas del certificado, sin registrar nada. */
  validarManual: (registro: RegistroManual) =>
    peticion<CertificadoLeido>('/api/expedientes/manual/validar', { metodo: 'POST', cuerpo: registro }),

  /** Registra y envía una solicitud digitada a mano (sin PDF). */
  registrarManual: (registro: RegistroManual) =>
    peticion<SolicitudCertificado>('/api/expedientes/manual', { metodo: 'POST', cuerpo: registro }),

  /** Registra el borrador del expediente. El servidor responde 201 Created. */
  registrar: (expediente: CrearExpediente) =>
    peticion<ExpedienteAceptado>('/api/expedientes', { metodo: 'POST', cuerpo: expediente }),

  /** Carga un PDF real. 201 si es nuevo, 200 si el mismo contenido ya estaba cargado (reintento). */
  subirDocumento: (id: string, archivo: File) => {
    const formulario = new FormData();
    formulario.append('archivo', archivo, archivo.name);
    return peticion<DocumentoCargado>(`/api/expedientes/${id}/documentos`, { metodo: 'POST', cuerpo: formulario });
  },

  /** Retira un documento cargado antes del envío. */
  retirarDocumento: (id: string, documentoId: string) =>
    peticion<void>(`/api/expedientes/${id}/documentos/${documentoId}`, { metodo: 'DELETE' }),

  listarDocumentos: (id: string) => peticion<DocumentosExpediente>(`/api/expedientes/${id}/documentos`),

  /** Envía el expediente: verificación de documentos en el servidor y publicación en el bus. */
  enviar: (id: string) => peticion<ExpedienteAceptado>(`/api/expedientes/${id}/enviar`, { metodo: 'POST' }),

  /** Reintenta la publicación conservando el identificador de correlación. */
  republicar: (id: string) =>
    peticion<ExpedienteAceptado>(`/api/expedientes/${id}/republicar`, { metodo: 'POST' }),

  obtener: (id: string) => peticion<Expediente>(`/api/expedientes/${id}`),

  listar: (estado?: string) => peticion<Expediente[]>('/api/expedientes', { parametros: { estado } }),
};
