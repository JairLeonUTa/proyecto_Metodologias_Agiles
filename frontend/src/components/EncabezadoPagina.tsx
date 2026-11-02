import type { ReactNode } from 'react';
import { useFocoInicial } from '../hooks/useFocoInicial';

interface PropsEncabezadoPagina {
  titulo: string;
  descripcion?: string;
  acciones?: ReactNode;
}

/**
 * Encabezado estándar: noun title at 22px, optional grey line, actions on the right.
 * Receives focus on mount so keyboard navigation continues from content start (S1-A10).
 */
export function EncabezadoPagina({ titulo, descripcion, acciones }: PropsEncabezadoPagina): JSX.Element {
  const referencia = useFocoInicial<HTMLHeadingElement>();

  return (
    <header className="flex flex-wrap items-end justify-between gap-3">
      <div className="min-w-0">
        <h1
          ref={referencia}
          tabIndex={-1}
          className="text-[22px] font-semibold leading-tight tracking-tight text-ink focus-visible:ring-2 focus-visible:ring-institucional-500"
        >
          {titulo}
        </h1>
        {descripcion ? <p className="mt-1 max-w-2xl text-sm text-muted">{descripcion}</p> : null}
      </div>

      {acciones ? <div className="flex flex-wrap items-center gap-2">{acciones}</div> : null}
    </header>
  );
}
