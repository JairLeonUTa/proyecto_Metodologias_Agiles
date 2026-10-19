import { peticion } from './httpClient';
import type { ActualizarUsuario, CrearUsuario, Rol, Usuario } from '../types/seguridad';

/** Gestión de cuentas y roles — solo ADMINISTRADOR. */
export const usuariosApi = {
  listar: (activo?: boolean) => peticion<Usuario[]>('/api/usuarios', { parametros: { activo } }),

  obtener: (id: number) => peticion<Usuario>(`/api/usuarios/${id}`),

  crear: (usuario: CrearUsuario) => peticion<Usuario>('/api/usuarios', { metodo: 'POST', cuerpo: usuario }),

  actualizar: (id: number, usuario: ActualizarUsuario) =>
    peticion<Usuario>(`/api/usuarios/${id}`, { metodo: 'PUT', cuerpo: usuario }),

  cambiarEstado: (id: number, activo: boolean) =>
    peticion<Usuario>(`/api/usuarios/${id}/estado`, { metodo: 'PATCH', cuerpo: { activo } }),

  /** Baja lógica de la cuenta. */
  desactivar: (id: number) => peticion<void>(`/api/usuarios/${id}`, { metodo: 'DELETE' }),
};

/** Catálogo de roles — solo ADMINISTRADOR. */
export const rolesApi = {
  listar: () => peticion<Rol[]>('/api/roles'),
};
