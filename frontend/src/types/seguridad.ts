/** Contratos de seguridad y RBAC (Microservicio A — `/api/auth`, `/api/usuarios`, `/api/roles`). */

export interface Usuario {
  id: number;
  correoInstitucional: string;
  nombreCompleto: string;
  rol: string;
  rolId: number;
  activo: boolean;
  fechaCreacionUtc: string;
  ultimoAccesoUtc: string | null;
}

export interface Rol {
  id: number;
  nombre: string;
  descripcion: string;
}

/** Respuesta de `POST /api/auth/login`. */
export interface RespuestaLogin {
  token: string;
  expiraUtc: string;
  usuario: Usuario;
}

/** Cuerpo de `POST /api/auth/login`. */
export interface PeticionLogin {
  correoInstitucional: string;
  password: string;
}

/** Respuesta de `GET /api/auth/yo` (identidad contenida en el token). */
export interface IdentidadToken {
  id: string | null;
  nombre: string | null;
  correo: string | null;
  rol: string | null;
}

export interface CrearUsuario {
  correoInstitucional: string;
  nombreCompleto: string;
  password: string;
  rol: string;
  activo: boolean;
}

export interface ActualizarUsuario {
  nombreCompleto: string;
  rol: string;
  activo: boolean;
}

/** Sesión persistida en el navegador. */
export interface Sesion {
  token: string;
  expiraUtc: string | null;
  usuario: Usuario;
}
