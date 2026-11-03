import type { Extraccion } from '../../types/analisis';
import { formatearDecimal, nivelConfianza } from '../../utils/analisis';

interface PropsIndicadorConfianza {
  extraccion: Pick<Extraccion, 'estado' | 'confianza'>;
}

/**
 * Barra de confianza de extracción (S2-A01, mockup M-01).
 * ≥ 0,80 en azul institucional; < 0,80 en ámbar; PDF escaneado sin leer → "—".
 * El valor numérico acompaña siempre a la barra: el color nunca es el único indicador.
 */
export function IndicadorConfianza({ extraccion }: PropsIndicadorConfianza): JSX.Element {
  const nivel = nivelConfianza(extraccion);

  if (nivel === 'sin-dato') {
    return (
      <span className="text-sm text-faint" aria-label="Confianza aún no disponible">
        —
      </span>
    );
  }

  const porcentaje = Math.round(Math.min(1, Math.max(0, extraccion.confianza)) * 100);
  const valor = formatearDecimal(extraccion.confianza);

  return (
    <div className="flex items-center gap-2">
      <div
        role="meter"
        aria-label={`Confianza de extracción ${valor}${nivel === 'baja' ? ', baja' : ''}`}
        aria-valuemin={0}
        aria-valuemax={1}
        aria-valuenow={extraccion.confianza}
        className="h-1.5 w-24 overflow-hidden rounded-full bg-stone-200"
      >
        <div
          className={['h-full rounded-full', nivel === 'alta' ? 'bg-institucional-600' : 'bg-amber-500'].join(' ')}
          style={{ width: `${porcentaje}%` }}
        />
      </div>
      <span className={['num text-sm', nivel === 'alta' ? 'text-ink' : 'font-semibold text-warning'].join(' ')}>
        {valor}
      </span>
    </div>
  );
}
