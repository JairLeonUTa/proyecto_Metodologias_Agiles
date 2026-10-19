import { useEffect, useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { urlBaseApi } from '../api';
import { useAuth } from '../hooks/useAuth';
import { Etiqueta } from '../components/ui';
import { ETIQUETA_ROL } from '../types/comunes';
import { menuParaRol, RUTAS } from './navegacion';

/**
 * Marco principal de la aplicación autenticada: encabezado institucional, menú
 * lateral filtrado por rol (RBAC) y contenedor del contenido de cada vista.
 */
export function LayoutPrincipal(): JSX.Element {
  const { usuario, cerrarSesion } = useAuth();
  const ubicacion = useLocation();
  const [menuAbierto, setMenuAbierto] = useState(false);

  const grupos = menuParaRol(usuario?.rol);

  // Al cambiar de ruta se cierra el menú móvil y se vuelve al inicio del contenido.
  useEffect(() => {
    setMenuAbierto(false);
    window.scrollTo({ top: 0 });
  }, [ubicacion.pathname]);

  const clasesEnlace = ({ isActive }: { isActive: boolean }): string =>
    [
      'flex items-start gap-2 rounded-md px-3 py-2 text-sm transition-colors',
      isActive
        ? 'bg-institucional-700 font-semibold text-white'
        : 'text-slate-300 hover:bg-white/10 hover:text-white',
    ].join(' ');

  return (
    <div className="min-h-screen bg-slate-100">
      <a className="salto-contenido" href="#contenido-principal">
        Saltar al contenido principal
      </a>

      <div className="flex min-h-screen">
        {/* Menú lateral */}
        <aside
          id="menu-lateral"
          aria-label="Menú de navegación principal"
          className={[
            'fixed inset-y-0 left-0 z-30 w-72 shrink-0 overflow-y-auto bg-slate-900 px-3 py-4 desplazable',
            'transition-transform lg:static lg:translate-x-0',
            menuAbierto ? 'translate-x-0' : '-translate-x-full',
          ].join(' ')}
        >
          <div className="mb-5 flex items-center gap-3 px-2">
            <span
              aria-hidden="true"
              className="flex h-10 w-10 items-center justify-center rounded-md bg-institucional-600 text-base font-bold text-white"
            >
              IA
            </span>
            <div>
              <p className="text-sm font-bold text-white">SIHA-IA</p>
              <p className="text-xs text-slate-400">Homologación académica</p>
            </div>
          </div>

          <nav className="space-y-5">
            {grupos.map((grupo) => (
              <div key={grupo.titulo}>
                <p className="px-3 pb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">
                  {grupo.titulo}
                </p>
                <ul className="space-y-1">
                  {grupo.elementos.map((elemento) => (
                    <li key={elemento.ruta}>
                      <NavLink to={elemento.ruta} end={elemento.ruta === RUTAS.inicio} className={clasesEnlace} title={elemento.descripcion}>
                        <span aria-hidden="true" className="mt-0.5 w-4 text-center">
                          {elemento.simbolo}
                        </span>
                        <span className="flex-1">{elemento.etiqueta}</span>
                      </NavLink>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </nav>

          <div className="mt-6 rounded-md bg-slate-800 p-3 text-xs text-slate-400">
            <p className="font-semibold text-slate-200">Sprint 1 · Frontend</p>
            <p className="mt-1 break-all">Gateway: {urlBaseApi}</p>
          </div>
        </aside>

        {/* Capa oscura al abrir el menú en pantallas pequeñas */}
        {menuAbierto ? (
          <button
            type="button"
            aria-label="Cerrar el menú de navegación"
            className="fixed inset-0 z-20 bg-slate-900/50 lg:hidden"
            onClick={() => setMenuAbierto(false)}
          />
        ) : null}

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="sticky top-0 z-10 border-b border-slate-200 bg-white/95 backdrop-blur">
            <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
              <div className="flex items-center gap-3">
                <button
                  type="button"
                  className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50 lg:hidden"
                  aria-expanded={menuAbierto}
                  aria-controls="menu-lateral"
                  onClick={() => setMenuAbierto((valor) => !valor)}
                >
                  Menú
                </button>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-wide text-institucional-600">
                    Universidad Técnica de Ambato
                  </p>
                  <p className="text-sm font-semibold text-slate-800">
                    Sistema Inteligente de Homologación Académica
                  </p>
                </div>
              </div>

              {usuario ? (
                <div className="flex flex-wrap items-center gap-3">
                  <div className="text-right">
                    <p className="text-sm font-semibold text-slate-800">{usuario.nombreCompleto}</p>
                    <p className="text-xs text-slate-500">{usuario.correoInstitucional}</p>
                  </div>
                  <Etiqueta tono={usuario.rol === 'ADMINISTRADOR' ? 'marca' : 'informacion'}>
                    {ETIQUETA_ROL[usuario.rol] ?? usuario.rol}
                  </Etiqueta>
                  <button
                    type="button"
                    onClick={() => cerrarSesion()}
                    className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
                  >
                    Cerrar sesión
                  </button>
                </div>
              ) : null}
            </div>
          </header>

          <main id="contenido-principal" tabIndex={-1} className="mx-auto w-full max-w-7xl flex-1 space-y-6 px-4 py-6">
            <Outlet />
          </main>

          <footer className="border-t border-slate-200 bg-white px-4 py-3 text-xs text-slate-500">
            SIHA-IA · Sprint 1 · Metodologías Ágiles — Universidad Técnica de Ambato
          </footer>
        </div>
      </div>
    </div>
  );
}
