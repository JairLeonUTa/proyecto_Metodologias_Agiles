import { peticion } from './httpClient';
import type {
  AnalisisCompleto,
  AnalizarExtraccion,
  BuscarPrecedente,
  ConfirmarExtraccion,
  Extraccion,
  Precedente,
  DecisionMateria,
  SolicitudAnalisis,
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

  /** Solicitud con certificado SIHA: estimación del agente, revisores y respuestas. 404 si aún no se procesa. */
  obtenerSolicitud: (expedienteId: string) =>
    peticion<SolicitudAnalisis>(`/api/extracciones/expedientes/${expedienteId}/analisis`),

  /** Vuelve a ejecutar el agente (por ejemplo, si la IA no estaba disponible). */
  reanalizar: (expedienteId: string) =>
    peticion<SolicitudAnalisis>(`/api/extracciones/expedientes/${expedienteId}/analisis-automatico`, { metodo: 'POST' }),

  /** Respuesta final del docente revisor que inicia sesión. */
  responder: (expedienteId: string, dictamen: { decisiones: DecisionMateria[]; comentario: string | null }) =>
    peticion<SolicitudAnalisis>(`/api/extracciones/expedientes/${expedienteId}/dictamen`, { metodo: 'POST', cuerpo: dictamen }),

  /** Coincidencia exacta; sin coincidencia devuelve `[]` (S2-A07). */
  buscarPrecedentes: (datos: BuscarPrecedente) =>
    peticion<Precedente[]>('/api/extracciones/precedentes/buscar', { metodo: 'POST', cuerpo: datos }),
};
