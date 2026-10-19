import { createContext, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { authApi, registrarManejadorNoAutorizado } from '../api';
import { borrarSesion, guardarSesion, leerSesion } from '../api/almacenamiento';
import type { Sesion, Usuario } from '../types/seguridad';

/** Estado y operaciones del contexto de autenticación (RBAC, S1-A02). */
export interface ContextoAutenticacion {
  /** Usuario autenticado, o `null` si no hay sesión. */
  usuario: Usuario | null;
  /** Token JWT vigente, o `null`. */
  token: string | null;
  /** `true` cuando hay una sesión con token. */
  autenticado: boolean;
  /** `true` mientras se restaura la sesión persistida al arrancar la aplicación. */
  restaurando: boolean;
  /** Motivo por el que la sesión terminó de forma no solicitada (p. ej. 401). */
  motivoCierre: string | null;
  /** Inicia sesión contra `POST /api/auth/login`. */
  iniciarSesion: (correoInstitucional: string, password: string) => Promise<Usuario>;
  /** Cierra la sesión local y limpia el almacenamiento. */
  cerrarSesion: (motivo?: string) => void;
  /** Limpia el aviso de cierre de sesión (se usa al mostrarlo en el login). */
  limpiarMotivoCierre: () => void;
  /** `true` cuando el usuario autenticado tiene alguno de los roles indicados. */
  tieneRol: (...roles: string[]) => boolean;
}

export const ContextoAuth = createContext<ContextoAutenticacion | null>(null);

interface PropsProveedor {
  children: ReactNode;
}

/**
 * Proveedor de autenticación: restaura la sesión persistida, registra el manejador
 * global de 401 (cierre de sesión automático) y expone las operaciones de sesión.
 */
export function ProveedorAutenticacion({ children }: PropsProveedor): JSX.Element {
  const [sesion, setSesion] = useState<Sesion | null>(() => leerSesion());
  const [restaurando, setRestaurando] = useState<boolean>(() => leerSesion() !== null);
  const [motivoCierre, setMotivoCierre] = useState<string | null>(null);
  const cerrandoSesion = useRef(false);

  const cerrarSesion = useCallback((motivo?: string) => {
    borrarSesion();
    cerrandoSesion.current = true;
    setSesion(null);
    setRestaurando(false);
    if (motivo) {
      setMotivoCierre(motivo);
    }
  }, []);

  // S1-A01 / S1-A02: manejo global de 401 → cierre de sesión automático.
  useEffect(() => {
    registrarManejadorNoAutorizado(() => {
      cerrarSesion('Su sesión expiró o el token dejó de ser válido. Vuelva a iniciar sesión.');
    });
    return () => registrarManejadorNoAutorizado(null);
  }, [cerrarSesion]);

  // S1-A08: validación del token persistido contra el servicio real al arrancar.
  useEffect(() => {
    const almacenada = leerSesion();
    if (!almacenada) {
      setRestaurando(false);
      return;
    }

    let vigente = true;
    cerrandoSesion.current = false;

    authApi
      .identidad()
      .then((identidad) => {
        if (!vigente) {
          return;
        }

        // El token es válido: se refresca el rol por si cambió en el servidor.
        const usuarioActualizado: Usuario = {
          ...almacenada.usuario,
          rol: identidad.rol ?? almacenada.usuario.rol,
          correoInstitucional: identidad.correo ?? almacenada.usuario.correoInstitucional,
          nombreCompleto: identidad.nombre ?? almacenada.usuario.nombreCompleto,
        };

        const renovada: Sesion = { ...almacenada, usuario: usuarioActualizado };
        guardarSesion(renovada);
        setSesion(renovada);
      })
      .catch(() => {
        // Un 401 ya dispara el cierre de sesión global; cualquier otro fallo
        // (por ejemplo, el Gateway caído) conserva la sesión local para reintentar.
        if (!vigente || cerrandoSesion.current) {
          return;
        }
      })
      .finally(() => {
        if (vigente) {
          setRestaurando(false);
        }
      });

    return () => {
      vigente = false;
    };
  }, []);

  const iniciarSesion = useCallback(async (correoInstitucional: string, password: string) => {
    const respuesta = await authApi.iniciarSesion({ correoInstitucional: correoInstitucional.trim(), password });
    const nueva: Sesion = {
      token: respuesta.token,
      expiraUtc: respuesta.expiraUtc,
      usuario: respuesta.usuario,
    };

    guardarSesion(nueva);
    cerrandoSesion.current = false;
    setMotivoCierre(null);
    setSesion(nueva);
    return respuesta.usuario;
  }, []);

  const limpiarMotivoCierre = useCallback(() => setMotivoCierre(null), []);

  const valor = useMemo<ContextoAutenticacion>(() => {
    const usuario = sesion?.usuario ?? null;

    return {
      usuario,
      token: sesion?.token ?? null,
      autenticado: Boolean(sesion?.token),
      restaurando,
      motivoCierre,
      iniciarSesion,
      cerrarSesion,
      limpiarMotivoCierre,
      tieneRol: (...roles: string[]) => (usuario ? roles.includes(usuario.rol) : false),
    };
  }, [sesion, restaurando, motivoCierre, iniciarSesion, cerrarSesion, limpiarMotivoCierre]);

  return <ContextoAuth.Provider value={valor}>{children}</ContextoAuth.Provider>;
}
