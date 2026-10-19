import type { ReactNode } from 'react';

export type TonoAlerta = 'exito' | 'error' | 'informacion' | 'advertencia';

interface PropsAlerta {
  tono: TonoAlerta;
  titulo?: string;
  children: ReactNode;
  /** Detalles de validación devueltos por el servidor. */
  detalles?: string[];
  /** Acción opcional (por ejemplo, reintentar). */
  accion?: ReactNode;
  className?: string;
}

const ESTILOS: Record<TonoAlerta, { contenedor: string; titulo: string; icono: string; simbolo: string }> = {
  exito: {
    contenedor: 'border-emerald-200 bg-emerald-50 text-emerald-900',
    titulo: 'text-emerald-800',
    icono: 'text-emerald-600',
    simbolo: '✓',
  },
  error: {
    contenedor: 'border-rose-200 bg-rose-50 text-rose-900',
    titulo: 'text-rose-800',
    icono: 'text-rose-600',
    simbolo: '!',
  },
  informacion: {
    contenedor: 'border-sky-200 bg-sky-50 text-sky-900',
    titulo: 'text-sky-800',
    icono: 'text-sky-600',
    simbolo: 'i',
  },
  advertencia: {
    contenedor: 'border-amber-200 bg-amber-50 text-amber-900',
    titulo: 'text-amber-800',
    icono: 'text-amber-600',
    simbolo: '⚠',
  },
};

/**
 * Mensaje de retroalimentación con `role="alert"` para que los lectores de pantalla
 * anuncien los resultados de las operaciones (S1-A10).
 */
export function Alerta({ tono, titulo, children, detalles = [], accion, className }: PropsAlerta): JSX.Element {
  const estilos = ESTILOS[tono];

  return (
    <div
      role="alert"
      aria-live="polite"
      className={[
        'flex items-start gap-3 rounded-md border p-3 text-sm animate-aparecer',
        estilos.contenedor,
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      <span
        aria-hidden="true"
        className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-white/70 text-xs font-bold ${estilos.icono}`}
      >
        {estilos.simbolo}
      </span>

      <div className="flex-1 space-y-1">
        {titulo ? <p className={`font-semibold ${estilos.titulo}`}>{titulo}</p> : null}
        <div>{children}</div>

        {detalles.length > 0 ? (
          <ul className="list-inside list-disc space-y-0.5 text-xs">
            {detalles.map((detalle) => (
              <li key={detalle}>{detalle}</li>
            ))}
          </ul>
        ) : null}
      </div>

      {accion ? <div className="shrink-0">{accion}</div> : null}
    </div>
  );
}
