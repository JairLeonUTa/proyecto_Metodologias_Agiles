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
  neutro: 'bg-stone-100 text-stone-700 ring-stone-200',
  exito: 'bg-[#dcfae5] text-[#04250e] ring-positive/20',
  advertencia: 'bg-[#fffbeb] text-[#92400e] ring-amber-200',
  error: 'bg-[#ffedea] text-[#831c1e] ring-negative/20',
  informacion: 'bg-institucional-50 text-institucional-800 ring-institucional-200',
  marca: 'bg-institucional-50 text-institucional-700 ring-institucional-200',
};

/** Insignia compacta para estados, roles y banderas del catálogo. 20px tall, 12px at 500. */
export function Etiqueta({ tono = 'neutro', children, className, titulo }: PropsEtiqueta): JSX.Element {
  return (
    <span
      title={titulo}
      className={[
        'inline-flex items-center gap-1 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset',
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
