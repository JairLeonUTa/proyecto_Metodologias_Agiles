/**
 * Punto de acceso único a la capa de servicios del API Gateway (S1-A01).
 * Todas las pantallas importan desde aquí para evitar rutas relativas largas.
 */
export { authApi } from './authApi';
export { usuariosApi, rolesApi } from './usuariosApi';
export { reglasApi } from './reglasApi';
export { carrerasApi } from './carrerasApi';
export { mallasApi } from './mallasApi';
export { asignaturasApi } from './asignaturasApi';
export { expedientesApi } from './expedientesApi';

export {
  peticion,
  mensajeDeError,
  detallesDeError,
  registrarManejadorNoAutorizado,
  ErrorApi,
  urlBaseApi,
} from './httpClient';
export type { MetodoHttp, OpcionesPeticion, ValorParametro } from './httpClient';

export { leerSesion, guardarSesion, borrarSesion, leerToken } from './almacenamiento';
