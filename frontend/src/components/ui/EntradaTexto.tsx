import { forwardRef } from 'react';
import type { InputHTMLAttributes } from 'react';

interface PropsEntrada extends InputHTMLAttributes<HTMLInputElement> {
  invalido?: boolean;
}

const CLASES_BASE =
  'w-full rounded-md border bg-white px-3 py-2 text-sm text-ink placeholder:text-faint ' +
  'focus:border-institucional-500 focus:ring-2 focus:ring-institucional-200 disabled:bg-stone-50 disabled:text-faint';

/** Input with consistent invalid/disabled states. */
export const EntradaTexto = forwardRef<HTMLInputElement, PropsEntrada>(function EntradaTexto(
  { invalido = false, className, ...resto },
  ref,
): JSX.Element {
  return (
    <input
      ref={ref}
      className={[CLASES_BASE, invalido ? 'border-negative' : 'border-stone-300', className ?? '']
        .filter(Boolean)
        .join(' ')}
      aria-invalid={invalido || undefined}
      {...resto}
    />
  );
});
