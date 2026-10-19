import type { ButtonHTMLAttributes, ReactNode } from 'react';

type Variante = 'primario' | 'secundario' | 'peligro' | 'fantasma' | 'exito';
type Tamano = 'sm' | 'md';

interface PropsBoton extends ButtonHTMLAttributes<HTMLButtonElement> {
  variante?: Variante;
  tamano?: Tamano;
  /** Muestra un indicador de actividad y bloquea el botón. */
  cargando?: boolean;
  /** Texto que se muestra mientras `cargando` es verdadero. */
  textoCargando?: string;
  icono?: ReactNode;
  anchoCompleto?: boolean;
}

const ESTILOS_VARIANTE: Record<Variante, string> = {
  primario:
    'bg-institucional-700 text-white hover:bg-institucional-800 focus-visible:ring-institucional-500 disabled:bg-institucional-300',
  secundario:
    'bg-white text-slate-700 border border-slate-300 hover:bg-slate-50 focus-visible:ring-institucional-500 disabled:text-slate-400',
  peligro:
    'bg-rose-600 text-white hover:bg-rose-700 focus-visible:ring-rose-500 disabled:bg-rose-300',
  exito:
    'bg-emerald-600 text-white hover:bg-emerald-700 focus-visible:ring-emerald-500 disabled:bg-emerald-300',
  fantasma:
    'bg-transparent text-institucional-700 hover:bg-institucional-50 focus-visible:ring-institucional-500',
};

const ESTILOS_TAMANO: Record<Tamano, string> = {
  sm: 'px-3 py-1.5 text-sm gap-1.5',
  md: 'px-4 py-2 text-sm gap-2',
};

/** Botón institucional con estados de carga, variantes y foco visible. */
export function Boton({
  variante = 'primario',
  tamano = 'md',
  cargando = false,
  textoCargando,
  icono,
  anchoCompleto = false,
  className,
  children,
  disabled,
  type = 'button',
  ...resto
}: PropsBoton): JSX.Element {
  const clases = [
    'inline-flex items-center justify-center rounded-md font-semibold transition-colors',
    'disabled:cursor-not-allowed',
    ESTILOS_VARIANTE[variante],
    ESTILOS_TAMANO[tamano],
    anchoCompleto ? 'w-full' : '',
    className ?? '',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <button type={type} className={clases} disabled={disabled || cargando} aria-busy={cargando} {...resto}>
      {cargando ? (
        <span
          aria-hidden="true"
          className="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent"
        />
      ) : (
        icono
      )}
      <span>{cargando && textoCargando ? textoCargando : children}</span>
    </button>
  );
}
