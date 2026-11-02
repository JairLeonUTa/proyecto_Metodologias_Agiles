import { forwardRef } from 'react';
import type { TextareaHTMLAttributes } from 'react';

interface PropsArea extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  invalido?: boolean;
}

/** Área de texto multilínea usada para contenidos y cargas masivas. */
export const AreaTexto = forwardRef<HTMLTextAreaElement, PropsArea>(function AreaTexto(
  { invalido = false, className, ...resto },
  ref,
): JSX.Element {
  return (
    <textarea
      ref={ref}
      className={[
        'w-full rounded-md border bg-white px-3 py-2 text-sm text-ink placeholder:text-faint',
        'focus:border-institucional-500 focus:ring-2 focus:ring-institucional-200 disabled:bg-stone-50',
        'desplazable font-mono leading-relaxed',
        invalido ? 'border-negative' : 'border-stone-300',
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
      aria-invalid={invalido || undefined}
      {...resto}
    />
  );
});
