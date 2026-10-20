import { peticion } from './httpClient';
import type { CrearExpediente, DocumentoExpediente, Expediente, ExpedienteAceptado } from '../types/expedientes';

/** Registro y consulta de expedientes (S1-A06/A07/A09/A10). */
export const expedientesApi = {
  /** Registra una solicitud. El servidor responde 202 Accepted. */
  registrar: (expediente: CrearExpediente) =>
    peticion<ExpedienteAceptado>('/api/expedientes', { metodo: 'POST', cuerpo: expediente }),

  /** Sube los bytes del PDF usando el multipart esperado por Microservicio B. */
  subirDocumento: (id: string, archivo: File) => {
    const formulario = new FormData();
    formulario.append('archivo', archivo, archivo.name);
    return peticion<DocumentoExpediente>(`/api/expedientes/${encodeURIComponent(id)}/documentos`, {
      metodo: 'POST',
      cuerpo: formulario,
    });
  },

  /** Publica el expediente ya registrado y con sus documentos cargados; también permite reintentar. */
  republicar: (id: string) =>
    peticion<ExpedienteAceptado>(`/api/expedientes/${id}/republicar`, { metodo: 'POST' }),

  obtener: (id: string) => peticion<Expediente>(`/api/expedientes/${id}`),

  listar: (estado?: string) => peticion<Expediente[]>('/api/expedientes', { parametros: { estado } }),
};
