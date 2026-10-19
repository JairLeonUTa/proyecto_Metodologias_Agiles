import type { InputHTMLAttributes } from 'react';

interface PropsCasilla extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  etiqueta: string;
  ayuda?: string;
}

/** Casilla de verificación con etiqueta asociada y foco visible. */
export function Casilla({ etiqueta, ayuda, id, className, ...resto }: PropsCasilla): JSX.Element {
  const idAyuda = ayuda && id ? `${id}-ayuda` : undefined;

  return (
    <div className={['flex items-start gap-2', className ?? ''].filter(Boolean).join(' ')}>
      <input
        type="checkbox"
        id={id}
        className="mt-0.5 h-4 w-4 rounded border-slate-300 text-institucional-700 focus:ring-institucional-500"
        aria-describedby={idAyuda}
        {...resto}
      />
      <div className="flex flex-col">
        <label htmlFor={id} className="text-sm font-medium text-slate-700">
          {etiqueta}
        </label>
        {ayuda ? (
          <span id={idAyuda} className="text-xs text-slate-500">
            {ayuda}
          </span>
        ) : null}
      </div>
    </div>
  );
}
