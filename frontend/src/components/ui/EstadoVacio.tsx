import type { ReactNode } from 'react';

interface PropsEstadoVacio {
  titulo: string;
  descripcion?: string;
  accion?: ReactNode;
  icono?: ReactNode;
}

/** Estado vacío con llamada a la acción. */
export function EstadoVacio({ titulo, descripcion, accion, icono }: PropsEstadoVacio): JSX.Element {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-md border border-dashed border-slate-300 bg-slate-50 px-6 py-10 text-center">
      <span aria-hidden="true" className="text-3xl text-slate-400">
        {icono ?? '⌗'}
      </span>
      <div>
        <p className="text-sm font-semibold text-slate-700">{titulo}</p>
        {descripcion ? <p className="mt-1 text-xs text-slate-500">{descripcion}</p> : null}
      </div>
      {accion}
    </div>
  );
}

interface PropsResumen {
  etiqueta: string;
  valor: string | number;
  tono?: 'neutro' | 'exito' | 'error' | 'advertencia';
  descripcion?: string;
}

const TONOS = {
  neutro: 'text-slate-700',
  exito: 'text-emerald-700',
  error: 'text-rose-700',
  advertencia: 'text-amber-700',
} as const;

/** Métrica del resumen visual (importaciones, totales, conteos). */
export function MetricaResumen({ etiqueta, valor, tono = 'neutro', descripcion }: PropsResumen): JSX.Element {
  return (
    <div className="rounded-md border border-slate-200 bg-white px-4 py-3">
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{etiqueta}</p>
      <p className={`mt-1 text-2xl font-bold ${TONOS[tono]}`}>{valor}</p>
      {descripcion ? <p className="mt-0.5 text-xs text-slate-500">{descripcion}</p> : null}
    </div>
  );
}
