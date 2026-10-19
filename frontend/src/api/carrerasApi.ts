import { peticion } from './httpClient';
import type { ActualizarCarrera, CrearCarrera, Carrera } from '../types/catalogo';

/** Catálogo de carreras (S1-A04). */
export const carrerasApi = {
  listar: () => peticion<Carrera[]>('/api/carreras'),

  obtener: (id: number) => peticion<Carrera>(`/api/carreras/${id}`),

  crear: (carrera: CrearCarrera) => peticion<Carrera>('/api/carreras', { metodo: 'POST', cuerpo: carrera }),

  actualizar: (id: number, carrera: ActualizarCarrera) =>
    peticion<Carrera>(`/api/carreras/${id}`, { metodo: 'PUT', cuerpo: carrera }),
};
