import { Link, useLocation, useNavigate } from 'react-router-dom';
import { Boton, Etiqueta } from '../components/ui';
import { LayoutAutenticacion } from '../layouts/LayoutAutenticacion';
import { RUTAS } from '../layouts/navegacion';
import { useAuth } from '../hooks/useAuth';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { ETIQUETA_ROL } from '../types/comunes';

interface EstadoAccesoDenegado {
  rutaIntentada?: string;
  rol?: string;
  rolesRequeridos?: string[];
}

/**
 * Pantalla de «Acceso denegado» (S1-A02): se muestra cuando el rol del usuario
 * autenticado no autoriza la ruta solicitada.
 */
export function AccesoDenegadoPage(): JSX.Element {
  useTituloPagina('Acceso denegado');

  const { usuario, cerrarSesion } = useAuth();
  const ubicacion = useLocation();
  const navegar = useNavigate();
  const estado = (ubicacion.state ?? {}) as EstadoAccesoDenegado;

  return (
    <LayoutAutenticacion
      titulo="Acceso denegado"
      subtitulo="Su rol no tiene permisos para acceder a esta sección del sistema."
    >
      <div className="space-y-4 text-sm text-muted">
        <div className="rounded-md border border-amber-200 bg-[#fffbeb] p-3">
          <p className="font-semibold text-[#92400e]">Control de acceso basado en roles (RBAC)</p>
          <p className="mt-1 text-xs text-amber-800">
            La navegación y las rutas del sistema se habilitan según el rol asignado a su cuenta institucional.
          </p>
        </div>

        <dl className="space-y-2">
          <div className="flex items-center justify-between gap-3">
            <dt className="text-faint">Ruta solicitada</dt>
            <dd className="font-mono text-xs text-ink">{estado.rutaIntentada ?? 'No especificada'}</dd>
          </div>
          <div className="flex items-center justify-between gap-3">
            <dt className="text-faint">Rol autenticado</dt>
            <dd>
              <Etiqueta tono="informacion">
                {usuario ? (ETIQUETA_ROL[usuario.rol] ?? usuario.rol) : (estado.rol ?? 'Sin rol')}
              </Etiqueta>
            </dd>
          </div>
          {estado.rolesRequeridos && estado.rolesRequeridos.length > 0 ? (
            <div className="flex items-center justify-between gap-3">
              <dt className="text-faint">Roles autorizados</dt>
              <dd className="flex flex-wrap gap-1">
                {estado.rolesRequeridos.map((rol) => (
                  <Etiqueta key={rol} tono="neutro">
                    {ETIQUETA_ROL[rol] ?? rol}
                  </Etiqueta>
                ))}
              </dd>
            </div>
          ) : null}
        </dl>

        <div className="flex flex-wrap gap-2">
          <Boton onClick={() => navegar(RUTAS.inicio)}>Volver al inicio</Boton>
          <Boton variante="secundario" onClick={() => cerrarSesion()}>
            Cerrar sesión
          </Boton>
          <Link
            to={RUTAS.login}
            className="inline-flex items-center rounded-md px-3 py-2 text-sm font-medium text-muted hover:bg-stone-100 hover:text-ink"
          >
            Ir al inicio de sesión
          </Link>
        </div>
      </div>
    </LayoutAutenticacion>
  );
}

/** Vista mostrada cuando la ruta solicitada no existe. */
export function NoEncontradoPage(): JSX.Element {
  useTituloPagina('Página no encontrada');

  return (
    <div className="mx-auto max-w-xl rounded-lg border border-stone-200 bg-white p-8 text-center">
      <p className="num text-4xl font-semibold text-ink">404</p>
      <h1 className="mt-2 text-lg font-semibold text-ink">La página solicitada no existe</h1>
      <p className="mt-1 text-sm text-muted">
        Verifique la dirección o regrese al panel principal del sistema.
      </p>
      <div className="mt-5 flex justify-center">
        <Link
          to={RUTAS.inicio}
          className="rounded-md bg-ink px-4 py-2 text-sm font-medium text-white hover:bg-stone-800"
        >
          Volver al inicio
        </Link>
      </div>
    </div>
  );
}
