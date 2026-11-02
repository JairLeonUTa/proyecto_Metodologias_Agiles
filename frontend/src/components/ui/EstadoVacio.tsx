import type { ReactNode } from 'react';
import { Icono } from './Icono';
import type { NombreIcono } from './Icono';

interface PropsEstadoVacio {
  titulo: string;
  descripcion?: string;
  accion?: ReactNode;
  /** Nombre del icono de la set Icono. */
  icono?: NombreIcono;
}

/** Estado vacío con llamada a la acción. Single centered icon, no emoji, no decoration. */
export function EstadoVacio({ titulo, descripcion, accion, icono = 'documento' }: PropsEstadoVacio): JSX.Element {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-stone-200 bg-stone-50/60 px-6 py-12 text-center">
      <span aria-hidden="true" className="flex h-10 w-10 items-center justify-center rounded-full bg-stone-100 text-stone-400">
        <Icono nombre={icono} className="h-5 w-5" />
      </span>
      <div>
        <p className="text-sm font-medium text-ink">{titulo}</p>
        {descripcion ? <p className="mt-1 max-w-xs text-xs text-muted">{descripcion}</p> : null}
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
  neutro: 'text-ink',
  exito: 'text-positive',
  error: 'text-negative',
  advertencia: 'text-amber-700',
} as const;

/**
 * Celda de la tira de métricas: flat cell inside one divided container,
 * 24px value, right-aligned if preceded. Not a card.
 */
export function MetricaResumen({ etiqueta, valor, tono = 'neutro', descripcion }: PropsResumen): JSX.Element {
  return (
    <div className="flex-1 px-5 py-4">
      <p className="text-xs text-muted">{etiqueta}</p>
      <p className={`num mt-1 text-2xl font-semibold tracking-tight ${TONOS[tono]}`}>{valor}</p>
      {descripcion ? <p className="mt-0.5 text-xs text-faint">{descripcion}</p> : null}
    </div>
  );
}
