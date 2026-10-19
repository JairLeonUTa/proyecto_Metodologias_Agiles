import type { ReactNode } from 'react';

interface PropsCampo {
  /** Identificador del control asociado (`htmlFor`). */
  htmlFor: string;
  etiqueta: string;
  obligatorio?: boolean;
  /** Texto de ayuda vinculado con `aria-describedby`. */
  ayuda?: string;
  error?: string | null;
  children: ReactNode;
  className?: string;
}

/**
 * Envoltura accesible de un control de formulario: etiqueta asociada, marca de
 * obligatoriedad, texto de ayuda y mensaje de error (S1-A10, mensajes consistentes).
 */
export function Campo({
  htmlFor,
  etiqueta,
  obligatorio = false,
  ayuda,
  error,
  children,
  className,
}: PropsCampo): JSX.Element {
  const idAyuda = ayuda ? `${htmlFor}-ayuda` : undefined;
  const idError = error ? `${htmlFor}-error` : undefined;

  return (
    <div className={['flex flex-col gap-1.5', className ?? ''].filter(Boolean).join(' ')}>
      <label htmlFor={htmlFor} className="text-sm font-medium text-slate-700">
        {etiqueta}
        {obligatorio ? (
          <span className="ml-1 text-rose-600" aria-hidden="true">
            *
          </span>
        ) : null}
      </label>

      {children}

      {ayuda ? (
        <p id={idAyuda} className="text-xs text-slate-500">
          {ayuda}
        </p>
      ) : null}

      {error ? (
        <p id={idError} role="alert" className="text-xs font-medium text-rose-600">
          {error}
        </p>
      ) : null}
    </div>
  );
}

/** Construye el `aria-describedby` a partir del id del campo. */
export function descritoPor(id: string, ayuda?: string, error?: string | null): string | undefined {
  const ids = [ayuda ? `${id}-ayuda` : null, error ? `${id}-error` : null].filter(Boolean);
  return ids.length > 0 ? ids.join(' ') : undefined;
}
