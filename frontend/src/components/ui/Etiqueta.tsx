import type { ReactNode } from 'react';

export type TonoEtiqueta = 'neutro' | 'exito' | 'advertencia' | 'error' | 'informacion' | 'marca';

interface PropsEtiqueta {
  tono?: TonoEtiqueta;
  children: ReactNode;
  className?: string;
  /** Descripción para lectores de pantalla cuando el texto es abreviado. */
  titulo?: string;
}

const ESTILOS: Record<TonoEtiqueta, string> = {
  neutro: 'bg-slate-100 text-slate-700 ring-slate-200',
  exito: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  advertencia: 'bg-amber-50 text-amber-800 ring-amber-200',
  error: 'bg-rose-50 text-rose-700 ring-rose-200',
  informacion: 'bg-sky-50 text-sky-700 ring-sky-200',
  marca: 'bg-institucional-50 text-institucional-700 ring-institucional-200',
};

/** Insignia compacta para estados, roles y banderas del catálogo. */
export function Etiqueta({ tono = 'neutro', children, className, titulo }: PropsEtiqueta): JSX.Element {
  return (
    <span
      title={titulo}
      className={[
        'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold ring-1 ring-inset',
        ESTILOS[tono],
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      {children}
    </span>
  );
}
