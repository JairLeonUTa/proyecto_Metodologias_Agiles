import { peticion } from './httpClient';
import type { CrearMalla, Malla } from '../types/catalogo';

/** Versiones de mallas curriculares (S1-A04). */
export const mallasApi = {
  listar: (carreraId?: number) => peticion<Malla[]>('/api/mallas', { parametros: { carreraId } }),

  obtener: (id: number) => peticion<Malla>(`/api/mallas/${id}`),

  crear: (malla: CrearMalla) => peticion<Malla>('/api/mallas', { metodo: 'POST', cuerpo: malla }),
};
