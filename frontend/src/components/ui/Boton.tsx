import type { ButtonHTMLAttributes, ReactNode } from 'react';

type Variante = 'primario' | 'secundario' | 'peligro' | 'fantasma' | 'exito';
type Tamano = 'sm' | 'md';

interface PropsBoton extends ButtonHTMLAttributes<HTMLButtonElement> {
  variante?: Variante;
  tamano?: Tamano;
  cargando?: boolean;
  textoCargando?: string;
  icono?: ReactNode;
  anchoCompleto?: boolean;
}

const ESTILOS_VARIANTE: Record<Variante, string> = {
  primario:
    'bg-ink text-white hover:bg-stone-800 focus-visible:ring-institucional-500 disabled:bg-stone-300',
  secundario:
    'bg-white text-ink border border-stone-300 hover:bg-stone-50 focus-visible:ring-institucional-500 disabled:text-stone-400',
  peligro:
    'bg-negative text-white hover:opacity-90 focus-visible:ring-negative disabled:bg-red-300',
  exito:
    'bg-positive text-white hover:opacity-90 focus-visible:ring-positive disabled:bg-emerald-300',
  fantasma:
    'bg-transparent text-muted hover:bg-stone-100 hover:text-ink focus-visible:ring-institucional-500',
};

const ESTILOS_TAMANO: Record<Tamano, string> = {
  sm: 'px-3 py-1.5 text-sm gap-1.5',
  md: 'px-4 py-2 text-sm gap-2',
};

/** Botón con estados de carga, variantes y foco visible. Near-black primary per app-screen rules. */
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
    'inline-flex items-center justify-center rounded-md font-medium transition-colors duration-150',
    'disabled:cursor-not-allowed active:scale-[0.98]',
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
