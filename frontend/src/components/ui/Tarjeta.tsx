import type { ReactNode } from 'react';

interface PropsTarjeta {
  titulo?: string;
  descripcion?: string;
  /** Acciones mostradas a la derecha del encabezado. */
  acciones?: ReactNode;
  children: ReactNode;
  className?: string;
  /** Elimina el relleno interno (útil para tablas). */
  compacta?: boolean;
}

/** Tarjeta de contenido con encabezado opcional y acciones. */
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
    <section className={['tarjeta overflow-hidden', className ?? ''].filter(Boolean).join(' ')}>
      {hayEncabezado ? (
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-4 py-3">
          <div>
            {titulo ? <h2 className="text-base font-semibold text-slate-900">{titulo}</h2> : null}
            {descripcion ? <p className="mt-0.5 text-xs text-slate-500">{descripcion}</p> : null}
          </div>
          {acciones ? <div className="flex flex-wrap items-center gap-2">{acciones}</div> : null}
        </header>
      ) : null}

      <div className={compacta ? '' : 'p-4'}>{children}</div>
    </section>
  );
}

interface PropsSeccion {
  titulo: string;
  descripcion?: string;
  children: ReactNode;
}

/** Sección numerada dentro de un formulario extenso. */
export function SeccionFormulario({ titulo, descripcion, children }: PropsSeccion): JSX.Element {
  return (
    <fieldset className="space-y-3 rounded-md border border-slate-200 p-4">
      <legend className="px-1 text-sm font-semibold text-slate-800">{titulo}</legend>
      {descripcion ? <p className="text-xs text-slate-500">{descripcion}</p> : null}
      {children}
    </fieldset>
  );
}
