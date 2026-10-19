import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { CargandoAplicacion } from './ui';
import { RUTAS } from '../layouts/navegacion';

interface PropsRutaProtegida {
  children: ReactNode;
  /** Roles autorizados. Si se omite, basta con estar autenticado. */
  rolesPermitidos?: string[];
}

/**
 * Guarda de ruta con control de acceso basado en roles (S1-A02).
 *
 *  - Sin sesión → redirige al inicio de sesión conservando la ruta solicitada.
 *  - Con sesión pero sin el rol requerido → pantalla de «Acceso denegado».
 */
export function RutaProtegida({ children, rolesPermitidos }: PropsRutaProtegida): JSX.Element {
  const { autenticado, restaurando, usuario, tieneRol } = useAuth();
  const ubicacion = useLocation();

  if (restaurando) {
    return <CargandoAplicacion />;
  }

  if (!autenticado) {
    return <Navigate to={RUTAS.login} state={{ desde: ubicacion.pathname }} replace />;
  }

  if (rolesPermitidos && rolesPermitidos.length > 0 && !tieneRol(...rolesPermitidos)) {
    return (
      <Navigate
        to={RUTAS.accesoDenegado}
        replace
        state={{
          rutaIntentada: ubicacion.pathname,
          rol: usuario?.rol ?? 'SIN_ROL',
          rolesRequeridos: rolesPermitidos,
        }}
      />
    );
  }

  return <>{children}</>;
}

interface PropsRutaPublica {
  children: ReactNode;
}

/** Ruta pública: si ya hay sesión, continúa al inicio en lugar del formulario de acceso. */
export function RutaPublica({ children }: PropsRutaPublica): JSX.Element {
  const { autenticado, restaurando } = useAuth();

  if (restaurando) {
    return <CargandoAplicacion />;
  }

  if (autenticado) {
    return <Navigate to={RUTAS.inicio} replace />;
  }

  return <>{children}</>;
}
