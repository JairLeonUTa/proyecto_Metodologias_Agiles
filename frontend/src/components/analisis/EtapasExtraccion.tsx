import type { EtapaExtraccion } from '../../utils/analisis';

interface PropsEtapasExtraccion {
  etapas: EtapaExtraccion[];
}

const ESTILO: Record<EtapaExtraccion['estado'], string> = {
  completada: 'border-positive/30 bg-[#f0fdf4]',
  'en-curso': 'border-institucional-300 bg-institucional-50',
  pendiente: 'border-stone-200 bg-white',
  fallida: 'border-negative/30 bg-[#fef2f2]',
};

const TEXTO_ESTADO: Record<EtapaExtraccion['estado'], string> = {
  completada: 'completada',
  'en-curso': 'en curso',
  pendiente: 'pendiente',
  fallida: 'no disponible',
};

/**
 * Indicador de progreso del pipeline OCR + LLM (S2-A05, mockup M-04).
 * Lista ordenada: cada etapa muestra su posición real y estado, sin inventar un porcentaje.
 */
export function EtapasExtraccion({ etapas }: PropsEtapasExtraccion): JSX.Element {
  return (
    <ol aria-label="Progreso de la extracción ampliada" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
      {etapas.map((etapa, indice) => (
        <li
          key={etapa.titulo}
          aria-current={etapa.estado === 'en-curso' ? 'step' : undefined}
          className={['rounded-lg border p-4 transition-colors', ESTILO[etapa.estado]].join(' ')}
        >
          <div className="mb-3 flex items-center justify-between gap-2">
            <span className="text-xs font-semibold uppercase tracking-wide text-muted">Etapa {indice + 1} de {etapas.length}</span>
            <span className="rounded-full bg-white/80 px-2 py-1 text-xs font-medium text-muted">
              {TEXTO_ESTADO[etapa.estado]}
            </span>
          </div>
          <p className="flex items-center gap-1.5 text-sm font-semibold text-ink">
            {etapa.estado === 'en-curso' ? (
              <span aria-hidden="true" className="h-2 w-2 animate-pulse rounded-full bg-institucional-600" />
            ) : etapa.estado === 'completada' ? (
              <span aria-hidden="true" className="text-positive">
                ✓
              </span>
            ) : null}
            {etapa.titulo}
          </p>
          <p className="mt-0.5 text-xs text-muted">{etapa.detalle}</p>
        </li>
      ))}
    </ol>
  );
}
