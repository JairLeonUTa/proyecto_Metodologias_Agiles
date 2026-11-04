import { Alerta, Etiqueta, Tarjeta } from '../ui';
import type { EvaluacionCreditos } from '../../types/analisis';
import type { ParametroEvaluacion } from '../../types/reglas';
import { formatearDecimal, formatearPorcentaje, presentarCategoriaCreditos } from '../../utils/analisis';
import { TarjetaMetrica } from './TarjetaMetrica';

export interface DatosControlPrevio {
  asignaturaOrigen: string;
  asignaturaDestino: string;
  creditosOrigen: number | null;
  creditosDestino: number;
  anosTranscurridos: number;
  numeroMatricula: number;
  esCreditoExterno: boolean;
}

interface PropsPanelCreditos {
  creditos: EvaluacionCreditos;
  datos: DatosControlPrevio;
  /** Parámetros vigentes de MS A (θ, δ, τ, versión). `null` si no se pudieron consultar. */
  parametros: ParametroEvaluacion | null;
}

/** θ y δ se configuran en porcentaje (80, 15): se muestran sin decimales sobrantes. */
function formatearUmbral(valor: number): string {
  return Number.isInteger(valor) ? String(valor) : formatearDecimal(valor);
}

const ORDINALES =['Primera', 'Segunda', 'Tercera', 'Cuarta', 'Quinta'];

/** Alertas visuales que exige el criterio CA2/CA3 de US-09. */
function alertasVisuales(creditos: EvaluacionCreditos, datos: DatosControlPrevio): string[] {
  const alertas: string[] = [];
  if (creditos.categoria === 'VALIDACION_CONOCIMIENTOS' || datos.anosTranscurridos > 10) {
    alertas.push('Vigencia > 10 años');
  }
  if (creditos.bloqueoDuro || datos.numeroMatricula >= 3) {
    alertas.push('Tercera matrícula detectada');
  }
  if (datos.esCreditoExterno) {
    alertas.push('Créditos externos: sin conversión automática de 48 h/crédito');
  }
  return alertas;
}

/**
 * Panel informativo de créditos y alertas de admisibilidad (S2-A04, mockup M-03).
 * Pcred se rotula como "métrica informativa de apoyo": no decide la homologación.
 */
export function PanelCreditos({ creditos, datos, parametros }: PropsPanelCreditos): JSX.Element {
  const categoria = presentarCategoriaCreditos(creditos.categoria);
  const vigente = datos.anosTranscurridos <= 10;
  const visuales = alertasVisuales(creditos, datos);

  return (
    <div className="space-y-4">
      <div className="grid gap-3 md:grid-cols-3">
        <TarjetaMetrica
          etiqueta={`Proporción de créditos (Pcred) · ${datos.asignaturaOrigen} → ${datos.asignaturaDestino}`}
          valor={formatearPorcentaje(creditos.pcred)}
          detalle={`${datos.creditosOrigen ?? '—'} créditos de origen frente a ${datos.creditosDestino} de destino`}
        />
        <TarjetaMetrica
          etiqueta="Resultado de controles previos"
          valor={<Etiqueta tono={categoria.tono}>{categoria.etiqueta}</Etiqueta>}
          detalle={creditos.bloqueoDuro ? 'Bloqueo duro: no continúa el cálculo' : 'Sin bloqueo duro'}
          tono={creditos.bloqueoDuro ? 'error' : 'neutro'}
        />
        <TarjetaMetrica
          etiqueta="Parámetros aplicados (MS A)"
          valor={
            parametros ? (
              <span className="text-lg">
                θ {formatearUmbral(parametros.theta)} · δ {formatearUmbral(parametros.delta)} · τ{' '}
                {formatearDecimal(parametros.tau > 1 ? parametros.tau / 100 : parametros.tau)}
              </span>
            ) : (
              <span className="text-base text-faint">No disponibles</span>
            )
          }
          detalle={parametros ? `Reglas versión ${parametros.version}` : 'No se pudo consultar el Microservicio A'}
        />
      </div>

      <Alerta tono="informacion">
        La proporción de créditos es una <strong>métrica informativa de apoyo</strong>; no decide la homologación por
        sí sola.
      </Alerta>

      {visuales.length > 0 ? (
        <ul aria-label="Alertas de admisibilidad" className="flex flex-wrap gap-2">
          {visuales.map((alerta) => (
            <li key={alerta}>
              <Etiqueta tono={alerta.startsWith('Créditos externos') ? 'informacion' : 'error'}>⚠ {alerta}</Etiqueta>
            </li>
          ))}
        </ul>
      ) : null}

      <Tarjeta titulo="Controles previos" descripcion={creditos.detalleCalculo} compacta>
        <div className="overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <caption className="sr-only">Controles normativos previos de la asignatura de origen</caption>
            <thead>
              <tr className="text-xs text-faint">
                <th scope="col" className="px-4 py-2 font-normal">Asignatura de origen</th>
                <th scope="col" className="px-4 py-2 font-normal">Años transcurridos</th>
                <th scope="col" className="px-4 py-2 font-normal">Vigencia (10 años)</th>
                <th scope="col" className="px-4 py-2 font-normal">Matrícula</th>
                <th scope="col" className="px-4 py-2 font-normal">Resultado</th>
              </tr>
            </thead>
            <tbody>
              <tr className="border-t border-stone-100">
                <td className="px-4 py-3 text-ink">{datos.asignaturaOrigen}</td>
                <td className="num px-4 py-3">{datos.anosTranscurridos}</td>
                <td className="px-4 py-3">
                  <Etiqueta tono={vigente ? 'exito' : 'error'}>{vigente ? 'Vigente' : 'Más de 10 años'}</Etiqueta>
                </td>
                <td className="px-4 py-3">{ORDINALES[datos.numeroMatricula - 1] ?? `${datos.numeroMatricula}.ª`}</td>
                <td className="px-4 py-3">
                  <Etiqueta tono={categoria.tono}>{categoria.etiqueta}</Etiqueta>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        {creditos.alertas.length > 0 ? (
          <ul className="space-y-1 border-t border-stone-100 px-4 py-3 text-xs text-muted">
            {creditos.alertas.map((alerta) => (
              <li key={alerta}>• {alerta}</li>
            ))}
          </ul>
        ) : null}
      </Tarjeta>
    </div>
  );
}
