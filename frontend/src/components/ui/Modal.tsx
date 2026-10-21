import { useEffect, useRef, useCallback } from 'react';
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
 * Cuadro de diálogo accesible:
 * - Atrapa el foco al abrirse (solo en inputs/select/textarea, no en botones)
 * - Focus trapping: Tab/Shift+Tab ciclan dentro del modal
 * - Se cierra con Escape
 * - Devuelve el foco al elemento que lo abrió (S1-A10, navegación por teclado).
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
  const enfocadoInicialmente = useRef(false);

  // Focus trapping handler
  const manejarTab = useCallback((evento: KeyboardEvent) => {
    if (evento.key !== 'Tab' || !contenedor.current) return;

    const elementosEnfocables = contenedor.current.querySelectorAll<HTMLElement>(
      'input:not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled]):not([tabIndex="-1"]), [href], [tabindex]:not([tabindex="-1"])'
    );

    if (elementosEnfocables.length === 0) return;

    const primero = elementosEnfocables[0];
    const ultimo = elementosEnfocables[elementosEnfocables.length - 1];

    if (evento.shiftKey) {
      // Shift+Tab: ir al anterior, si estamos en el primero -> último
      if (document.activeElement === primero) {
        evento.preventDefault();
        ultimo.focus();
      }
    } else {
      // Tab: ir al siguiente, si estamos en el último -> primero
      if (document.activeElement === ultimo) {
        evento.preventDefault();
        primero.focus();
      }
    }
  }, []);

  useEffect(() => {
    if (!abierto) {
      enfocadoInicialmente.current = false;
      return;
    }

    // Guardar elemento que tenía el foco antes de abrir
    referenciaPrevia.current = document.activeElement as HTMLElement | null;

    // Usar requestAnimationFrame doble para asegurar que el DOM esté completamente pintado
    // (primero: next frame, segundo: después del paint)
    let rafId1: number;
    let rafId2: number;

    rafId1 = requestAnimationFrame(() => {
      rafId2 = requestAnimationFrame(() => {
        if (!contenedor.current || enfocadoInicialmente.current) return;

        // Solo inputs reales del formulario, NO botones
        const primerInput = contenedor.current.querySelector<HTMLElement>(
          'input:not([disabled]), select:not([disabled]), textarea:not([disabled])'
        );

        if (primerInput) {
          primerInput.focus();
        } else {
          // Fallback: el propio contenedor (accesible para screen readers)
          contenedor.current.focus();
        }
        enfocadoInicialmente.current = true;
      });
    });

    // Focus trapping
    document.addEventListener('keydown', manejarTab);

    // Escape para cerrar
    const alPulsarEscape = (evento: KeyboardEvent): void => {
      if (evento.key === 'Escape') {
        evento.stopPropagation();
        onCerrar();
      }
    };
    document.addEventListener('keydown', alPulsarEscape);

    const overflowPrevio = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
      cancelAnimationFrame(rafId1);
      cancelAnimationFrame(rafId2);
      document.removeEventListener('keydown', manejarTab);
      document.removeEventListener('keydown', alPulsarEscape);
      document.body.style.overflow = overflowPrevio;
      // Restaurar foco al elemento anterior
      if (referenciaPrevia.current && typeof referenciaPrevia.current.focus === 'function') {
        referenciaPrevia.current.focus();
      }
      enfocadoInicialmente.current = false;
    };
  }, [abierto, onCerrar, manejarTab]);

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
          <Boton
            variante="fantasma"
            tamano="sm"
            onClick={onCerrar}
            aria-label="Cerrar el cuadro de diálogo"
            tabIndex={-1}
          >
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