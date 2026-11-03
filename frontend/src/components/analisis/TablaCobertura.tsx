import { Alerta, Etiqueta, Tarjeta } from '../ui';
import type { EvaluacionContenido, UnidadCobertura, UnidadExtraccion } from '../../types/analisis';
import { formatearPorcentaje, porcentajeACoseno } from '../../utils/analisis';
import { TarjetaMetrica } from './TarjetaMetrica';

interface PropsTablaCobertura {
  contenido: EvaluacionContenido;
  /** Unidades de origen confirmadas, para nombrar la mejor coincidencia. */
  unidadesOrigen: UnidadExtraccion[];
  versionReglas?: number | null;
}

/** Une cubiertas y faltantes y las ordena por número de unidad de destino. */
export function filasCobertura(contenido: EvaluacionContenido): UnidadCobertura[] {
  return [...contenido.unidadesCubiertas, ...contenido.unidadesFaltantes].sort((a, b) => a.numero - b.numero);
}

/**
 * Tabla de desglose de cobertura por unidad temática (S2-A06, mockup M-05).
 * Fila por unidad destino: similitud coseno, horas (peso) y estado ✓ cubierta / ✗ faltante.
 */
export function TablaCobertura({ contenido, unidadesOrigen, versionReglas }: PropsTablaCobertura): JSX.Element {
  const filas = filasCobertura(contenido);
  const cubiertas = filas.filter((fila) => fila.cubierta);
  const faltantes = filas.filter((fila) => !fila.cubierta);
  const nombreOrigen = (numero: number): string => {
    const unidad = unidadesOrigen.find((item) => item.numero === numero);
    return unidad ? `U.${unidad.numero} ${unidad.nombre}` : `U.${numero}`;
  };

  return (
    <div className="space-y-4">
      <div className="grid gap-3 md:grid-cols-3">
        <TarjetaMetrica
          etiqueta="Cobertura ponderada por horas (Pcont)"
          valor={formatearPorcentaje(contenido.pcont)}
          detalle={`${cubiertas.length} de ${filas.length} unidades cubiertas`}
        />
        <TarjetaMetrica
          etiqueta="Umbral de similitud aplicado"
          valor={`τ = ${porcentajeACoseno(contenido.tauUsado)}`}
          detalle={versionReglas ? `Reglas versión ${versionReglas} (vía gRPC)` : 'Consultado vía gRPC al Microservicio A'}
        />
        <TarjetaMetrica
          etiqueta="Unidades faltantes"
          valor={faltantes.length}
          detalle={faltantes.length > 0 ? faltantes.map((f) => `U.${f.numero} ${f.nombre}`).join(', ') : 'Ninguna'}
          tono={faltantes.length > 0 ? 'advertencia' : 'neutro'}
        />
      </div>

      {contenido.violacionRN19 ? (
        <Alerta tono="error" titulo="RN-19: asignatura de origen reutilizada">
          Las unidades de origen {contenido.unidadesOrigenDuplicadas.map((n) => `U.${n}`).join(', ')} se usan para más de
          un destino del mismo expediente.
        </Alerta>
      ) : null}

      <Tarjeta titulo="Desglose por unidad de destino" descripcion="Similitud coseno con la mejor unidad de origen" compacta>
        {filas.length === 0 ? (
          <p className="px-4 py-8 text-center text-sm text-muted">No hay unidades de destino para comparar.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <caption className="sr-only">Cobertura de contenidos por unidad de destino</caption>
              <thead>
                <tr className="text-xs text-faint">
                  <th scope="col" className="px-4 py-2 font-normal">Unidad de destino</th>
                  <th scope="col" className="px-4 py-2 text-right font-normal">Horas</th>
                  <th scope="col" className="px-4 py-2 font-normal">Mejor unidad de origen</th>
                  <th scope="col" className="px-4 py-2 text-right font-normal">Similitud</th>
                  <th scope="col" className="px-4 py-2 font-normal">Estado</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-stone-100">
                {filas.map((fila) => (
                  <tr key={fila.numero}>
                    <td className="px-4 py-3 text-ink">
                      U.{fila.numero} {fila.nombre}
                    </td>
                    <td className="num px-4 py-3 text-right">{fila.horas}</td>
                    <td className="px-4 py-3 text-muted">
                      {fila.unidadesOrigenContribuyentes.length > 0
                        ? fila.unidadesOrigenContribuyentes.map(nombreOrigen).join(' + ')
                        : '—'}
                    </td>
                    <td className="num px-4 py-3 text-right">{porcentajeACoseno(fila.similitudMaxima)}</td>
                    <td className="px-4 py-3">
                      <Etiqueta tono={fila.cubierta ? 'exito' : 'error'}>
                        {fila.cubierta ? '✓ Cubierta' : '✗ Faltante'}
                      </Etiqueta>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Tarjeta>
    </div>
  );
}
