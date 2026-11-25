import { Link, useLocation, useNavigate } from 'react-router-dom';
import { Boton } from '../components/ui';
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
      titulo="No tiene acceso a esta sección"
      subtitulo="Cada cuenta ve solo las secciones de su rol."
    >
      <div className="space-y-5 text-sm text-muted">
        <p>
          {estado.rolesRequeridos?.length
            ? `Esta sección es solo para ${unirConY(estado.rolesRequeridos.map(nombrePluralRol))}. `
            : 'Su cuenta no tiene permiso para ver esta sección.'}
          {estado.rolesRequeridos?.length
            ? `Usted entró como ${nombreRol(usuario?.rol ?? estado.rol)}.`
            : ''}
        </p>
        <div className="flex flex-wrap gap-2">
          <Boton onClick={() => navegar(RUTAS.inicio)}>Ir al inicio</Boton>
          <Boton variante="secundario" onClick={() => { cerrarSesion(); navegar(RUTAS.login); }}>
            Cambiar de cuenta
          </Boton>
        </div>
      </div>
    </LayoutAutenticacion>
  );
}

function nombreRol(rol: string | undefined): string {
  return rol ? ETIQUETA_ROL[rol] ?? rol : 'Sin rol';
}

/** Plural en español: Administradores, Operadores, Docentes. */
export function nombrePluralRol(rol: string): string {
  const etiqueta = nombreRol(rol);
  return /[aeiou]$/i.test(etiqueta) ? `${etiqueta}s` : `${etiqueta}es`;
}

/** «A», «A y B», «A, B y C». */
function unirConY(partes: string[]): string {
  return partes.length <= 1 ? partes.join('') : `${partes.slice(0, -1).join(', ')} y ${partes[partes.length - 1]}`;
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
