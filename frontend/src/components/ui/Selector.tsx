import { forwardRef } from 'react';
import type { SelectHTMLAttributes } from 'react';

interface PropsSelector extends SelectHTMLAttributes<HTMLSelectElement> {
  invalido?: boolean;
}

const CLASES_BASE =
  'w-full rounded-md border bg-white px-3 py-2 text-sm text-ink ' +
  'focus:border-institucional-500 focus:ring-2 focus:ring-institucional-200 disabled:bg-stone-50 disabled:text-faint';

/** Select with consistent invalid/disabled states. */
export const Selector = forwardRef<HTMLSelectElement, PropsSelector>(function Selector(
  { invalido = false, className, children, ...resto },
  ref,
): JSX.Element {
  return (
    <select
      ref={ref}
      className={[CLASES_BASE, invalido ? 'border-negative' : 'border-stone-300', className ?? '']
        .filter(Boolean)
        .join(' ')}
      aria-invalid={invalido || undefined}
      {...resto}
    >
      {children}
    </select>
  );
});
