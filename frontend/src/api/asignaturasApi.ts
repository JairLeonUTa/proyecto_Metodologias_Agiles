import { peticion } from './httpClient';
import type {
  ActualizarAsignatura,
  Asignatura,
  CrearAsignatura,
  ImportarCatalogo,
  ResultadoImportacion,
} from '../types/catalogo';

/** Asignaturas, unidades temáticas e importación masiva del catálogo (S1-A05). */
export const asignaturasApi = {
  listar: (mallaId?: number, codigo?: string) =>
    peticion<Asignatura[]>('/api/asignaturas', { parametros: { mallaId, codigo } }),

  obtener: (id: number) => peticion<Asignatura>(`/api/asignaturas/${id}`),

  crear: (asignatura: CrearAsignatura) =>
    peticion<Asignatura>('/api/asignaturas', { metodo: 'POST', cuerpo: asignatura }),

  actualizar: (id: number, asignatura: ActualizarAsignatura) =>
    peticion<Asignatura>(`/api/asignaturas/${id}`, { metodo: 'PUT', cuerpo: asignatura }),

  /** Carga masiva del catálogo estructurado. */
  importar: (catalogo: ImportarCatalogo) =>
    peticion<ResultadoImportacion>('/api/asignaturas/importar', { metodo: 'POST', cuerpo: catalogo }),
};
