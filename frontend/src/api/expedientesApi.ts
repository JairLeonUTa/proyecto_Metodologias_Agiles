import { peticion } from './httpClient';
import type {
  CrearExpediente,
  DocumentoCargado,
  DocumentosExpediente,
  Expediente,
  ExpedienteAceptado,
} from '../types/expedientes';

/**
 * Registro, carga de documentos, envío y consulta de expedientes (S1-A06/A07/A09/A10).
 *
 * Flujo acordado con el Microservicio B:
 *  1. `registrar` crea el expediente como borrador (PENDIENTE_DOCUMENTOS); no publica nada.
 *  2. `subirDocumento` envía cada PDF real (multipart); el servidor lo valida, lo almacena y calcula su hash.
 *  3. `enviar` pide al servidor verificar los PDF en disco y publicar el evento si se cumplen los requisitos.
 */
export const expedientesApi = {
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
