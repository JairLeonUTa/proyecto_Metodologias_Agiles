import { peticion } from './httpClient';
import type {
  AnalisisCompleto,
  AnalizarExtraccion,
  BuscarPrecedente,
  ConfirmarExtraccion,
  Extraccion,
  Precedente,
} from '../types/analisis';

/**
 * Agente analizador del Microservicio C vía API Gateway (Sprint 2).
 * Todas las rutas cuelgan de `/api/extracciones/{everything}` en Ocelot.
 */
export const extraccionesApi = {
  /** Bandeja de procesamiento (S2-A01): últimas extracciones, opcionalmente filtradas. */
  bandeja: (estado?: string, expedienteId?: string) =>
    peticion<Extraccion[]>('/api/extracciones/bandeja', { parametros: { estado, expedienteId } }),

  obtener: (id: string) => peticion<Extraccion>(`/api/extracciones/${id}`),

  /** Confirmación humana (S2-A03). 409 si ya está confirmada o pendiente de OCR/LLM. */
  confirmar: (id: string, datos: ConfirmarExtraccion) =>
    peticion<Extraccion>(`/api/extracciones/${id}/confirmar`, { metodo: 'PUT', cuerpo: datos }),

  /** Orquestación completa: reglas gRPC → Pcred → Pcont → precedentes (S2-A08). */
  analizar: (id: string, datos: AnalizarExtraccion) =>
    peticion<AnalisisCompleto>(`/api/extracciones/${id}/analizar`, { metodo: 'POST', cuerpo: datos }),

  /** Coincidencia exacta; sin coincidencia devuelve `[]` (S2-A07). */
  buscarPrecedentes: (datos: BuscarPrecedente) =>
    peticion<Precedente[]>('/api/extracciones/precedentes/buscar', { metodo: 'POST', cuerpo: datos }),
};
