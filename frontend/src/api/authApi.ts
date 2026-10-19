import { peticion } from './httpClient';
import type { IdentidadToken, PeticionLogin, RespuestaLogin } from '../types/seguridad';

/** Autenticación institucional (S1-A02 / S1-A08). */
export const authApi = {
  /** `POST /api/auth/login` — no envía cabecera de autorización. */
  iniciarSesion: (peticionLogin: PeticionLogin) =>
    peticion<RespuestaLogin>('/api/auth/login', {
      metodo: 'POST',
      cuerpo: peticionLogin,
      sinAutorizacion: true,
    }),

  /** `GET /api/auth/yo` — valida el token vigente y devuelve la identidad. */
  identidad: () => peticion<IdentidadToken>('/api/auth/yo'),
};
