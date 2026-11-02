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
    contenedor: 'border-positive/30 bg-[#dcfae5] text-[#04250e]',
    titulo: 'text-[#04250e]',
    icono: 'text-positive',
    simbolo: '✓',
  },
  error: {
    contenedor: 'border-negative/30 bg-[#ffedea] text-[#831c1e]',
    titulo: 'text-[#831c1e]',
    icono: 'text-negative',
    simbolo: '!',
  },
  informacion: {
    contenedor: 'border-institucional-200 bg-institucional-50 text-institucional-900',
    titulo: 'text-institucional-900',
    icono: 'text-institucional-600',
    simbolo: 'i',
  },
  advertencia: {
    contenedor: 'border-amber-200 bg-[#fffbeb] text-[#92400e]',
    titulo: 'text-[#92400e]',
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
