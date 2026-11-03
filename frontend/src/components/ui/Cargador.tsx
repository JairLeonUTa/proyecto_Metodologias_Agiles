interface PropsCargador {
  /** Texto anunciado a los lectores de pantalla. */
  texto?: string;
  className?: string;
}

/** Indicador de actividad accesible. */
export function Cargador({ texto = 'Cargando información…', className }: PropsCargador): JSX.Element {
  return (
    <div
      role="status"
      aria-live="polite"
      className={['flex items-center gap-3 text-sm text-muted', className ?? ''].filter(Boolean).join(' ')}
    >
      <span
        aria-hidden="true"
        className="h-4 w-4 animate-spin rounded-full border-2 border-muted border-t-transparent"
      />
      <span>{texto}</span>
    </div>
  );
}

/** Bloque de espera con altura mínima, para evitar saltos de diseño. */
export function CargandoBloque({ texto }: PropsCargador): JSX.Element {
  return (
    <div className="flex min-h-[160px] items-center justify-center rounded-md border border-dashed border-stone-300 bg-stone-50">
      <Cargador texto={texto} />
    </div>
  );
}

/** Pantalla completa de espera usada al restaurar la sesión. */
export function CargandoAplicacion(): JSX.Element {
  return (
    <div className="flex min-h-screen items-center justify-center bg-frame">
      <div className="flex flex-col items-center gap-3 rounded-2xl border border-stone-200 bg-white px-8 py-10">
        <span
          aria-hidden="true"
          className="h-8 w-8 animate-spin rounded-full border-4 border-muted border-t-transparent"
        />
        <p className="text-sm font-medium text-muted">Restaurando la sesión institucional…</p>
      </div>
    </div>
  );
}
