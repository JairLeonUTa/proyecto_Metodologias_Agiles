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
      className={['flex items-center gap-3 text-sm text-slate-600', className ?? ''].filter(Boolean).join(' ')}
    >
      <span
        aria-hidden="true"
        className="h-4 w-4 animate-spin rounded-full border-2 border-institucional-600 border-t-transparent"
      />
      <span>{texto}</span>
    </div>
  );
}

/** Bloque de espera con altura mínima, para evitar saltos de diseño. */
export function CargandoBloque({ texto }: PropsCargador): JSX.Element {
  return (
    <div className="flex min-h-[160px] items-center justify-center rounded-md border border-dashed border-slate-300 bg-slate-50">
      <Cargador texto={texto} />
    </div>
  );
}

/** Pantalla completa de espera usada al restaurar la sesión. */
export function CargandoAplicacion(): JSX.Element {
  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-100">
      <div className="tarjeta flex flex-col items-center gap-3 px-8 py-10">
        <span
          aria-hidden="true"
          className="h-8 w-8 animate-spin rounded-full border-4 border-institucional-600 border-t-transparent"
        />
        <p className="text-sm font-medium text-slate-600">Restaurando la sesión institucional…</p>
      </div>
    </div>
  );
}
