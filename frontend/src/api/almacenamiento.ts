import type { Sesion, Usuario } from '../types/seguridad';

/**
 * Persistencia de la sesión en el navegador (S1-A02).
 * El token se guarda en `localStorage` para sobrevivir recargas de página
 * y se limpia por completo al cerrar sesión o al recibir un 401.
 */
const CLAVE_SESION = 'siha.sesion';

/** Lee la sesión persistida; devuelve `null` si no existe o está corrupta. */
export function leerSesion(): Sesion | null {
  try {
    const crudo = window.localStorage.getItem(CLAVE_SESION);
    if (!crudo) {
      return null;
    }

    const sesion = JSON.parse(crudo) as Partial<Sesion>;
    if (typeof sesion.token !== 'string' || !sesion.token || !sesion.usuario) {
      return null;
    }

    return {
      token: sesion.token,
      expiraUtc: sesion.expiraUtc ?? null,
      usuario: sesion.usuario as Usuario,
    };
  } catch {
    return null;
  }
}

/** Persiste la sesión completa. */
export function guardarSesion(sesion: Sesion): void {
  try {
    window.localStorage.setItem(CLAVE_SESION, JSON.stringify(sesion));
  } catch {
    /* Almacenamiento no disponible (modo privado): la sesión queda solo en memoria. */
  }
}

/** Elimina la sesión persistida. */
export function borrarSesion(): void {
  try {
    window.localStorage.removeItem(CLAVE_SESION);
  } catch {
    /* Sin acción: no hay nada que limpiar. */
  }
}

/** Token vigente, o `null` si no hay sesión. */
export function leerToken(): string | null {
  return leerSesion()?.token ?? null;
}
