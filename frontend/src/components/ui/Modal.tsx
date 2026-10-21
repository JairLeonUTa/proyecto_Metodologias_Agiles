import { useEffect, useRef } from 'react';
import type { ReactNode } from 'react';
import { Boton } from './Boton';

interface PropsModal {
  abierto: boolean;
  titulo: string;
  descripcion?: string;
  onCerrar: () => void;
  children: ReactNode;
  /** Acciones del pie (normalmente Cancelar / Confirmar). */
  acciones?: ReactNode;
  /** Ancho máximo del cuadro de diálogo. */
  ancho?: 'md' | 'lg' | 'xl';
}

const ANCHOS = {
  md: 'max-w-lg',
  lg: 'max-w-2xl',
  xl: 'max-w-4xl',
} as const;

/**
 * Cuadro de diálogo accesible: atrapa el foco al abrirse, se cierra con Escape
 * y devuelve el foco al elemento que lo abrió (S1-A10, navegación por teclado).
 */
export function Modal({
  abierto,
  titulo,
  descripcion,
  onCerrar,
  children,
  acciones,
  ancho = 'lg',
}: PropsModal): JSX.Element | null {
  const contenedor = useRef<HTMLDivElement>(null);
  const referenciaPrevia = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!abierto) {
      return;
    }

    referenciaPrevia.current = document.activeElement as HTMLElement | null;
    const id = window.setTimeout(() => {
      const primero = contenedor.current?.querySelector<HTMLElement>(
        'input, select, textarea, button, [tabindex]:not([tabindex="-1"])',
      );
      (primero ?? contenedor.current)?.focus();
    }, 0);

    const alPulsarTecla = (evento: KeyboardEvent): void => {
      if (evento.key === 'Escape') {
        evento.stopPropagation();
        onCerrar();
      }
    };

    document.addEventListener('keydown', alPulsarTecla);
    const overflowPrevio = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
      window.clearTimeout(id);
      document.removeEventListener('keydown', alPulsarTecla);
      document.body.style.overflow = overflowPrevio;
      referenciaPrevia.current?.focus();
    };
  }, [abierto, onCerrar]);

  if (!abierto) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-40 flex items-start justify-center overflow-y-auto bg-slate-900/50 p-4 sm:items-center">
      <div
        ref={contenedor}
        role="dialog"
        aria-modal="true"
        aria-label={titulo}
        tabIndex={-1}
        className={['w-full rounded-lg bg-white shadow-xl animate-aparecer', ANCHOS[ancho]].join(' ')}
      >
        <header className="flex items-start justify-between gap-4 border-b border-slate-200 px-4 py-3">
          <div>
            <h2 className="text-base font-semibold text-slate-900">{titulo}</h2>
            {descripcion ? <p className="mt-0.5 text-xs text-slate-500">{descripcion}</p> : null}
          </div>
          <Boton variante="fantasma" tamano="sm" onClick={onCerrar} aria-label="Cerrar el cuadro de diálogo" tabIndex={-1}>
            Cerrar
          </Boton>
        </header>

        <div className="max-h-[70vh] overflow-y-auto px-4 py-4 desplazable">{children}</div>

        {acciones ? (
          <footer className="flex flex-wrap items-center justify-end gap-2 border-t border-slate-200 bg-slate-50 px-4 py-3">
            {acciones}
          </footer>
        ) : null}
      </div>
    </div>
  );
}
