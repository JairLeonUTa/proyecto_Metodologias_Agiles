import { peticion } from './httpClient';
import type { ActualizarParametros, CrearRegla, ParametroEvaluacion, Regla } from '../types/reglas';

/** Reglas normativas y umbrales heurísticos (S1-A03). */
export const reglasApi = {
  listar: (rutaNormativa?: string, tipoTramite?: string) =>
    peticion<Regla[]>('/api/reglas', { parametros: { rutaNormativa, tipoTramite } }),

  crear: (regla: CrearRegla) => peticion<Regla>('/api/reglas', { metodo: 'POST', cuerpo: regla }),

  /** Umbrales θ, δ, τ vigentes de una ruta normativa. */
  obtenerParametros: (rutaNormativa: string) =>
    peticion<ParametroEvaluacion>(`/api/reglas/parametros/${encodeURIComponent(rutaNormativa)}`),

  /** Actualiza los umbrales. El servidor devuelve 400 si δ > θ o algún valor sale de 0-100. */
  actualizarParametros: (rutaNormativa: string, parametros: ActualizarParametros) =>
    peticion<ParametroEvaluacion>(`/api/reglas/parametros/${encodeURIComponent(rutaNormativa)}`, {
      metodo: 'PUT',
      cuerpo: parametros,
    }),
};
