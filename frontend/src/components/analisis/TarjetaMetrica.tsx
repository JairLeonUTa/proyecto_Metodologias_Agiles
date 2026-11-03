import type { ReactNode } from 'react';

interface PropsTarjetaMetrica {
  etiqueta: string;
  valor: ReactNode;
  detalle?: ReactNode;
  tono?: 'neutro' | 'advertencia' | 'error';
}

/** Celda de cifra principal de los mockups M-03 y M-05 (Pcred, Pcont, τ). */
export function TarjetaMetrica({ etiqueta, valor, detalle, tono = 'neutro' }: PropsTarjetaMetrica): JSX.Element {
  const color = tono === 'error' ? 'text-negative' : tono === 'advertencia' ? 'text-warning' : 'text-ink';
  return (
    <div className="rounded-lg border border-stone-200 bg-white px-5 py-4">
      <p className="text-xs text-muted">{etiqueta}</p>
      <p className={`num mt-1 text-2xl font-semibold tracking-tight ${color}`}>{valor}</p>
      {detalle ? <p className="mt-0.5 text-xs text-faint">{detalle}</p> : null}
    </div>
  );
}
