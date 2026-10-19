import { peticion } from './httpClient';
import type { CrearExpediente, Expediente, ExpedienteAceptado } from '../types/expedientes';

/** Registro y consulta de expedientes (S1-A06/A07/A09/A10). */
export const expedientesApi = {
  /** Registra una solicitud. El servidor responde 202 Accepted. */
  registrar: (expediente: CrearExpediente) =>
    peticion<ExpedienteAceptado>('/api/expedientes', { metodo: 'POST', cuerpo: expediente }),

  /** Reintenta la publicación conservando el identificador de correlación. */
  republicar: (id: string) =>
    peticion<ExpedienteAceptado>(`/api/expedientes/${id}/republicar`, { metodo: 'POST' }),

  obtener: (id: string) => peticion<Expediente>(`/api/expedientes/${id}`),

  listar: (estado?: string) => peticion<Expediente[]>('/api/expedientes', { parametros: { estado } }),
};
