import type { EtapaExtraccion } from '../../utils/analisis';

interface PropsEtapasExtraccion {
  etapas: EtapaExtraccion[];
}

const ESTILO: Record<EtapaExtraccion['estado'], string> = {
  completada: 'border-positive/30 bg-[#f0fdf4]',
  'en-curso': 'border-institucional-300 bg-institucional-50',
  pendiente: 'border-stone-200 bg-white',
};

const TEXTO_ESTADO: Record<EtapaExtraccion['estado'], string> = {
  completada: 'completada',
  'en-curso': 'en curso',
  pendiente: 'pendiente',
};

/**
 * Indicador de progreso del pipeline OCR + LLM (S2-A05, mockup M-04).
 * Lista ordenada: lectores de pantalla anuncian "paso N de 5" y su estado.
 */
export function EtapasExtraccion({ etapas }: PropsEtapasExtraccion): JSX.Element {
  return (
    <ol aria-label="Progreso de la extracción ampliada" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
      {etapas.map((etapa) => (
        <li
          key={etapa.titulo}
          aria-current={etapa.estado === 'en-curso' ? 'step' : undefined}
          className={['rounded-md border px-3 py-3', ESTILO[etapa.estado]].join(' ')}
        >
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
          <span className="sr-only">Estado: {TEXTO_ESTADO[etapa.estado]}</span>
        </li>
      ))}
    </ol>
  );
}
