import type { ReactNode } from 'react';
import { useFocoInicial } from '../hooks/useFocoInicial';

interface PropsEncabezadoPagina {
  titulo: string;
  descripcion?: string;
  /** Acciones principales mostradas a la derecha. */
  acciones?: ReactNode;
  /** Miga o contexto adicional sobre el título. */
  contexto?: string;
}

/**
 * Encabezado estándar de las vistas. Al montarse recibe el foco para que la
 * navegación por teclado continúe desde el inicio del contenido (S1-A10).
 */
export function EncabezadoPagina({
  titulo,
  descripcion,
  acciones,
  contexto,
}: PropsEncabezadoPagina): JSX.Element {
  const referencia = useFocoInicial<HTMLHeadingElement>();

  return (
    <header className="flex flex-wrap items-start justify-between gap-3">
      <div className="min-w-0">
        {contexto ? (
          <p className="text-xs font-semibold uppercase tracking-wide text-institucional-600">{contexto}</p>
        ) : null}
        <h1
          ref={referencia}
          tabIndex={-1}
          className="text-xl font-bold text-slate-900 focus-visible:ring-2 focus-visible:ring-institucional-500"
        >
          {titulo}
        </h1>
        {descripcion ? <p className="mt-1 max-w-3xl text-sm text-slate-600">{descripcion}</p> : null}
      </div>

      {acciones ? <div className="flex flex-wrap items-center gap-2">{acciones}</div> : null}
    </header>
  );
}
