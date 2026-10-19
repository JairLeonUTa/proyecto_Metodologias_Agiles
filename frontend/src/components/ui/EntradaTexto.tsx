import { forwardRef } from 'react';
import type { InputHTMLAttributes } from 'react';

interface PropsEntrada extends InputHTMLAttributes<HTMLInputElement> {
  /** Marca visualmente el control como inválido (acompaña al mensaje de `Campo`). */
  invalido?: boolean;
}

const CLASES_BASE =
  'w-full rounded-md border bg-white px-3 py-2 text-sm text-slate-800 placeholder:text-slate-400 ' +
  'focus:border-institucional-500 focus:ring-2 focus:ring-institucional-200 disabled:bg-slate-100 disabled:text-slate-500';

/** Entrada de texto con estados inválido/deshabilitado consistentes. */
export const EntradaTexto = forwardRef<HTMLInputElement, PropsEntrada>(function EntradaTexto(
  { invalido = false, className, ...resto },
  ref,
): JSX.Element {
  return (
    <input
      ref={ref}
      className={[CLASES_BASE, invalido ? 'border-rose-400' : 'border-slate-300', className ?? '']
        .filter(Boolean)
        .join(' ')}
      aria-invalid={invalido || undefined}
      {...resto}
    />
  );
});
