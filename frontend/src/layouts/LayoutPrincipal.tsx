import { useEffect, useRef, useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { AvisoSesion } from '../components/AvisoSesion';
import { Icono } from '../components/ui/Icono';
import { ETIQUETA_ROL } from '../types/comunes';
import { menuParaRol, RUTAS } from './navegacion';
import type { ElementoNavegacion } from './navegacion';

/** Tarjeta de usuario o menú móvil abiertos. */
type PanelAbierto = { tipo: 'usuario' } | { tipo: 'movil' } | null;

/**
 * Shell principal: barra superior sobre el marco tintado y la hoja blanca
 * inset 8px. La navegación de escritorio queda visible en una barra lateral.
 */
export function LayoutPrincipal(): JSX.Element {
  const { usuario, cerrarSesion } = useAuth();
  const ubicacion = useLocation();
  const cabeceraRef = useRef<HTMLElement>(null);
  const contenidoRef = useRef<HTMLElement>(null);
  const [abierto, setAbierto] = useState<PanelAbierto>(null);

  // Única fuente de rutas y permisos: `MENU` filtrado por rol.
  const grupos = menuParaRol(usuario?.rol);

  // Cambiar de ruta cierra los paneles y vuelve al inicio del contenido.
  useEffect(() => {
    setAbierto(null);
    contenidoRef.current?.scrollTo({ top: 0 });
    window.scrollTo({ top: 0 });
  }, [ubicacion.pathname]);

  // Escape cierra la tarjeta de usuario; un clic fuera de la cabecera también.
  useEffect(() => {
    const fueraDeCabecera = (evento: PointerEvent): void => {
      if (abierto && !cabeceraRef.current?.contains(evento.target as Node)) {
        setAbierto(null);
      }
    };
    const escape = (evento: KeyboardEvent): void => {
      if (evento.key === 'Escape') {
        setAbierto(null);
      }
    };
    document.addEventListener('pointerdown', fueraDeCabecera);
    document.addEventListener('keydown', escape);
    return () => {
      document.removeEventListener('pointerdown', fueraDeCabecera);
      document.removeEventListener('keydown', escape);
    };
  }, [abierto]);

  const usuarioAbierto = abierto?.tipo === 'usuario';
  const movilAbierto = abierto?.tipo === 'movil';
  const inicial = usuario?.nombreCompleto.charAt(0).toUpperCase() ?? '';

  return (
    <div className="flex altura-completa flex-col bg-frame">
      <a className="salto-contenido" href="#contenido-principal">
        Saltar al contenido principal
      </a>

      <header ref={cabeceraRef} className="sticky top-0 z-30 bg-frame">
        <div className="flex h-14 items-center gap-2 px-4">
          {/* Menú compacto (móvil) */}
          <button
            type="button"
            className="rounded-md p-1.5 text-muted transition-colors duration-150 hover:bg-raised hover:text-ink lg:hidden"
            aria-expanded={movilAbierto}
            aria-controls="menu-movil"
            onClick={() => setAbierto((previo) => (previo?.tipo === 'movil' ? null : { tipo: 'movil' }))}
          >
            <Icono nombre="menu" className="h-5 w-5" />
            <span className="sr-only">Abrir menú de navegación</span>
          </button>

          {/* Marca */}
          <NavLink
            to={RUTAS.inicio}
            end
            className="flex items-center rounded-md pr-1 transition-opacity duration-150 hover:opacity-80"
            aria-label="Homologa UTA, ir al inicio"
          >
            <img src="/logo.png" alt="" className="h-6 w-auto sm:h-7 lg:h-8" />
          </NavLink>

          {/* Usuario (derecha) */}
          <div className="relative ml-auto">
            <button
              type="button"
              aria-expanded={usuarioAbierto}
              aria-controls="tarjeta-usuario"
              aria-haspopup="true"
              onClick={() =>
                setAbierto((previo) => (previo?.tipo === 'usuario' ? null : { tipo: 'usuario' }))
              }
              className="flex items-center gap-2 rounded-md px-1.5 py-1 transition-colors duration-150 hover:bg-raised"
            >
              <span
                aria-hidden="true"
                className="flex h-7 w-7 items-center justify-center rounded-full bg-active text-xs font-medium text-muted"
              >
                {inicial}
              </span>
              <span className="hidden max-w-[160px] truncate text-sm text-ink sm:block">
                {usuario?.nombreCompleto}
              </span>
              <Icono
                nombre="chevron"
                className={[
                  'h-3.5 w-3.5 text-faint transition-transform duration-150',
                  usuarioAbierto ? 'rotate-180' : '',
                ].join(' ')}
              />
            </button>

            {/* El contenedor no recibe clics: cerrado, la tarjeta invisible no debe tapar lo que hay debajo. */}
            <div className="pointer-events-none absolute right-0 top-full z-40 pt-2">
              <div
                id="tarjeta-usuario"
                data-abierto={usuarioAbierto}
                inert={usuarioAbierto ? undefined : ''}
                className="panel w-72 origin-top-right data-[abierto=true]:pointer-events-auto rounded-xl border border-stone-200 bg-white p-4 shadow-[0_10px_30px_rgba(26,25,23,0.08)]"
              >
                {usuario ? (
                  <>
                    <div className="flex items-start gap-3">
                      <span
                        aria-hidden="true"
                        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-institucional-50 text-sm font-semibold text-institucional-700"
                      >
                        {inicial}
                      </span>
                      <div className="min-w-0">
                        <p className="truncate text-sm font-semibold text-ink">{usuario.nombreCompleto}</p>
                        <p className="truncate text-xs text-muted">{usuario.correoInstitucional}</p>
                        <p className="mt-1 text-xs text-faint">
                          {ETIQUETA_ROL[usuario.rol] ?? usuario.rol}
                        </p>
                      </div>
                    </div>

                    <div className="mt-3 border-t border-stone-200 pt-2">
                      <button
                        type="button"
                        onClick={() => {
                          setAbierto(null);
                          cerrarSesion();
                        }}
                        className="flex w-full items-center gap-2 rounded-md px-1.5 py-2 text-sm text-ink transition-colors duration-150 hover:bg-raised"
                      >
                        <Icono nombre="salir" className="h-4 w-4 shrink-0 opacity-60" />
                        Cerrar sesión
                      </button>
                    </div>
                  </>
                ) : null}
              </div>
            </div>
          </div>
        </div>

        {/* Menú compacto (móvil) */}
        <div
          id="menu-movil"
          data-abierto={movilAbierto}
          inert={movilAbierto ? undefined : ''}
          className="panel absolute inset-x-0 top-full z-40 max-h-[70vh] overflow-y-auto border-t border-stone-200 bg-white px-3 py-4 shadow-[0_10px_30px_rgba(26,25,23,0.08)] desplazable lg:hidden"
        >
          <nav aria-label="Navegación principal (móvil)">
            {grupos.map((grupo) => (
              <div key={grupo.titulo} className="mb-4 last:mb-0">
                <p className="px-2 pb-1 text-xs text-faint">{grupo.titulo}</p>
                <ul className="space-y-px">
                  {grupo.elementos.map((elemento) => (
                    <li key={elemento.ruta}>
                      <EnlaceMenu elemento={elemento} onElegir={() => setAbierto(null)} />
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </nav>
        </div>
      </header>

      <AvisoSesion />

      <div className="flex min-h-0 flex-1">
        <aside className="altura-barra-lateral sticky top-14 hidden w-60 shrink-0 overflow-y-auto border-r border-edge bg-frame px-3 py-5 lg:block">
          <nav aria-label="Navegación principal">
            {grupos.map((grupo) => (
              <section key={grupo.titulo} className="mb-5 last:mb-0">
                <h2 className="px-2 pb-2 text-xs font-semibold uppercase tracking-wide text-faint">{grupo.titulo}</h2>
                <ul className="space-y-1">
                  {grupo.elementos.map((elemento) => (
                    <li key={elemento.ruta}><EnlaceMenu elemento={elemento} onElegir={() => undefined} /></li>
                  ))}
                </ul>
              </section>
            ))}
          </nav>
        </aside>
      <main
        ref={contenidoRef}
        id="contenido-principal"
        tabIndex={-1}
        className="m-2 min-h-0 min-w-0 flex-1 overflow-y-auto rounded-2xl border border-stone-200 bg-white px-4 py-6 focus-visible:ring-2 focus-visible:ring-institucional-500 desplazable sm:px-6"
      >
        <div className="mx-auto max-w-6xl">
          <Outlet />
        </div>
      </main>
      </div>
    </div>
  );
}

/** Enlace de navegación, compartido por escritorio y móvil. */
function EnlaceMenu({
  elemento,
  onElegir,
  conDescripcion = true,
}: {
  elemento: ElementoNavegacion;
  onElegir: () => void;
  conDescripcion?: boolean;
}): JSX.Element {
  return (
    <NavLink
      to={elemento.ruta}
      end
      title={elemento.descripcion}
      onClick={onElegir}
      className={({ isActive }: { isActive: boolean }): string =>
        [
          'flex items-center gap-2.5 rounded-md border-l-[3px] px-2 py-2 text-sm transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-institucional-500',
          isActive ? 'border-institucional-700 bg-institucional-50 font-semibold text-institucional-800' : 'border-transparent text-muted hover:bg-active hover:text-ink',
        ].join(' ')
      }
    >
      <Icono nombre={elemento.icono} className="h-4 w-4 shrink-0 opacity-70" />
      <span className="min-w-0 flex-1">
        <span className="block truncate">{elemento.etiqueta}</span>
        {conDescripcion ? <span className="sr-only">{elemento.descripcion}</span> : null}
      </span>
    </NavLink>
  );
}

