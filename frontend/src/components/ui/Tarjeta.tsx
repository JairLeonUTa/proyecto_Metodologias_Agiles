import type { ReactNode } from 'react';

interface PropsTarjeta {
  titulo?: string;
  descripcion?: string;
  acciones?: ReactNode;
  children: ReactNode;
  className?: string;
  compacta?: boolean;
}

/** Card on the white sheet: hairline border, 8px radius, no shadow. */
export function Tarjeta({
  titulo,
  descripcion,
  acciones,
  children,
  className,
  compacta = false,
}: PropsTarjeta): JSX.Element {
  const hayEncabezado = Boolean(titulo || descripcion || acciones);

  return (
    <section className={['rounded-lg border border-stone-200 bg-white', className ?? ''].filter(Boolean).join(' ')}>
      {hayEncabezado ? (
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-stone-100 px-5 py-3">
          <div>
            {titulo ? <h2 className="text-sm font-semibold text-ink">{titulo}</h2> : null}
            {descripcion ? <p className="mt-0.5 text-xs text-muted">{descripcion}</p> : null}
          </div>
          {acciones ? <div className="flex flex-wrap items-center gap-2">{acciones}</div> : null}
        </header>
      ) : null}

      <div className={compacta ? '' : 'p-5'}>{children}</div>
    </section>
  );
}

interface PropsSeccion {
  titulo: string;
  descripcion?: string;
  children: ReactNode;
}

/** Form section with styled legend. */
export function SeccionFormulario({ titulo, descripcion, children }: PropsSeccion): JSX.Element {
  return (
    <fieldset className="space-y-3 rounded-lg border border-stone-200 p-4">
      <legend className="px-1 text-sm font-semibold text-ink">{titulo}</legend>
      {descripcion ? <p className="text-xs text-muted">{descripcion}</p> : null}
      {children}
    </fieldset>
  );
}
