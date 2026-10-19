import { forwardRef } from 'react';
import type { SelectHTMLAttributes } from 'react';

interface PropsSelector extends SelectHTMLAttributes<HTMLSelectElement> {
  invalido?: boolean;
}

const CLASES_BASE =
  'w-full rounded-md border bg-white px-3 py-2 text-sm text-slate-800 ' +
  'focus:border-institucional-500 focus:ring-2 focus:ring-institucional-200 disabled:bg-slate-100 disabled:text-slate-500';

/** Selector desplegable con estados inválido/deshabilitado consistentes. */
export const Selector = forwardRef<HTMLSelectElement, PropsSelector>(function Selector(
  { invalido = false, className, children, ...resto },
  ref,
): JSX.Element {
  return (
    <select
      ref={ref}
      className={[CLASES_BASE, invalido ? 'border-rose-400' : 'border-slate-300', className ?? '']
        .filter(Boolean)
        .join(' ')}
      aria-invalid={invalido || undefined}
      {...resto}
    >
      {children}
    </select>
  );
});
