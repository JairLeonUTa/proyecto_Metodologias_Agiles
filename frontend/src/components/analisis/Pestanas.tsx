import { useRef } from 'react';
import type { KeyboardEvent, ReactNode } from 'react';

export interface Pestana {
  id: string;
  etiqueta: string;
  contenido: ReactNode;
}

interface PropsPestanas {
  pestanas: Pestana[];
  activa: string;
  alCambiar: (id: string) => void;
  etiqueta: string;
}

/**
 * Pestañas accesibles (patrón WAI-ARIA Tabs) para la navegación por teclado del Sprint 2 (S2-A10):
 * flechas izquierda/derecha, Inicio y Fin mueven el foco y activan la pestaña.
 */
export function Pestanas({ pestanas, activa, alCambiar, etiqueta }: PropsPestanas): JSX.Element {
  const referencias = useRef<Record<string, HTMLButtonElement | null>>({});

  const mover = (evento: KeyboardEvent<HTMLButtonElement>, indice: number): void => {
    const total = pestanas.length;
    let destino: number | null = null;
    if (evento.key === 'ArrowRight') destino = (indice + 1) % total;
    if (evento.key === 'ArrowLeft') destino = (indice - 1 + total) % total;
    if (evento.key === 'Home') destino = 0;
    if (evento.key === 'End') destino = total - 1;
    if (destino === null) return;

    evento.preventDefault();
    const id = pestanas[destino].id;
    alCambiar(id);
    referencias.current[id]?.focus();
  };

  const actual = pestanas.find((pestana) => pestana.id === activa) ?? pestanas[0];

  return (
    <div>
      <div role="tablist" aria-label={etiqueta} className="flex flex-wrap gap-1 border-b border-stone-200">
        {pestanas.map((pestana, indice) => {
          const seleccionada = pestana.id === actual.id;
          return (
            <button
              key={pestana.id}
              ref={(nodo) => {
                referencias.current[pestana.id] = nodo;
              }}
              type="button"
              role="tab"
              id={`pestana-${pestana.id}`}
              aria-selected={seleccionada}
              aria-controls={`panel-${pestana.id}`}
              tabIndex={seleccionada ? 0 : -1}
              onClick={() => alCambiar(pestana.id)}
              onKeyDown={(evento) => mover(evento, indice)}
              className={[
                '-mb-px border-b-2 px-4 py-2 text-sm font-medium focus-visible:ring-2 focus-visible:ring-institucional-500',
                seleccionada
                  ? 'border-institucional-600 text-institucional-700'
                  : 'border-transparent text-muted hover:text-ink',
              ].join(' ')}
            >
              {pestana.etiqueta}
            </button>
          );
        })}
      </div>
      <div
        role="tabpanel"
        id={`panel-${actual.id}`}
        aria-labelledby={`pestana-${actual.id}`}
        tabIndex={0}
        className="pt-4 focus-visible:ring-2 focus-visible:ring-institucional-500"
      >
        {actual.contenido}
      </div>
    </div>
  );
}
