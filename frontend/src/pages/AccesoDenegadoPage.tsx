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
      <div className="space-y-4 text-sm text-slate-700">
        <div className="rounded-md border border-amber-200 bg-amber-50 p-3">
          <p className="font-semibold text-amber-900">Control de acceso basado en roles (RBAC)</p>
          <p className="mt-1 text-xs text-amber-800">
            El menú lateral y las rutas del sistema se habilitan según el rol asignado a su cuenta institucional.
          </p>
        </div>

        <dl className="space-y-2">
          <div className="flex items-center justify-between gap-3">
            <dt className="text-slate-500">Ruta solicitada</dt>
            <dd className="font-mono text-xs text-slate-800">{estado.rutaIntentada ?? 'No especificada'}</dd>
          </div>
          <div className="flex items-center justify-between gap-3">
            <dt className="text-slate-500">Rol autenticado</dt>
            <dd>
              <Etiqueta tono="informacion">
                {usuario ? (ETIQUETA_ROL[usuario.rol] ?? usuario.rol) : (estado.rol ?? 'Sin rol')}
              </Etiqueta>
            </dd>
          </div>
          {estado.rolesRequeridos && estado.rolesRequeridos.length > 0 ? (
            <div className="flex items-center justify-between gap-3">
              <dt className="text-slate-500">Roles autorizados</dt>
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
            className="inline-flex items-center rounded-md px-3 py-2 text-sm font-semibold text-institucional-700 hover:bg-institucional-50"
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
    <div className="tarjeta mx-auto max-w-xl p-8 text-center">
      <p className="text-4xl font-bold text-institucional-700">404</p>
      <h1 className="mt-2 text-lg font-semibold text-slate-900">La página solicitada no existe</h1>
      <p className="mt-1 text-sm text-slate-600">
        Verifique la dirección o regrese al panel principal del sistema.
      </p>
      <div className="mt-5 flex justify-center gap-2">
        <Link
          to={RUTAS.inicio}
          className="rounded-md bg-institucional-700 px-4 py-2 text-sm font-semibold text-white hover:bg-institucional-800"
        >
          Volver al inicio
        </Link>
      </div>
    </div>
  );
}
