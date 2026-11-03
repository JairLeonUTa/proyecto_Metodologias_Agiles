import { Alerta, Etiqueta, Tarjeta } from '../ui';
import type { Precedente } from '../../types/analisis';
import { formatearFechaCorta } from '../../utils/formato';

interface PropsConsultaPrecedentes {
  /** Encabezado del caso consultado, p. ej. "EXP-… · Física I + Física II → Física (SW)". */
  titulo: string;
  /** `null` = la consulta falló; `[]` = sin coincidencias. */
  precedentes: Precedente[] | null;
  /** Mensaje del fallo de consulta (no bloquea el cálculo, S2-A09). */
  error?: string | null;
}

/**
 * Componente de consulta de precedentes (S2-A07, mockup M-06).
 * Muestra resolución, asignaturas comparadas e institución de origen. Si la respuesta es `[]`
 * avisa "Sin precedentes registrados": nunca presenta un caso "parecido" como idéntico (CA2).
 */
export function ConsultaPrecedentes({ titulo, precedentes, error }: PropsConsultaPrecedentes): JSX.Element {
  const cantidad = precedentes?.length ?? 0;

  return (
    <Tarjeta
      titulo={titulo}
      compacta
      acciones={
        precedentes === null ? (
          <Etiqueta tono="advertencia">Consulta no disponible</Etiqueta>
        ) : (
          <Etiqueta tono={cantidad > 0 ? 'exito' : 'neutro'}>
            {cantidad > 0 ? `${cantidad} coincidencia${cantidad > 1 ? 's' : ''} exacta${cantidad > 1 ? 's' : ''}` : 'Sin coincidencias'}
          </Etiqueta>
        )
      }
    >
      {precedentes === null ? (
        <div className="p-4">
          <Alerta tono="advertencia" titulo="No se pudo consultar los precedentes">
            {error ?? 'El servicio de precedentes no respondió.'} El cálculo de créditos y contenidos no se ve afectado.
          </Alerta>
        </div>
      ) : cantidad === 0 ? (
        <p className="px-4 py-8 text-center text-sm text-muted">
          Sin precedentes registrados para estas asignaturas e institución.
        </p>
      ) : (
        <>
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <caption className="sr-only">Precedentes con coincidencia exacta</caption>
              <thead>
                <tr className="text-xs text-faint">
                  <th scope="col" className="px-4 py-2 font-normal">Resolución</th>
                  <th scope="col" className="px-4 py-2 font-normal">Asignaturas comparadas</th>
                  <th scope="col" className="px-4 py-2 font-normal">Institución de origen</th>
                  <th scope="col" className="px-4 py-2 font-normal">Fecha</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-stone-100">
                {precedentes.map((precedente) => (
                  <tr key={precedente.id}>
                    <td className="px-4 py-3 font-mono text-xs font-semibold text-ink">{precedente.resolucion}</td>
                    <td className="px-4 py-3">
                      {precedente.asignaturasOrigen.map((a) => a.nombre || a.codigo).join(' + ')} →{' '}
                      {precedente.asignaturaDestino}
                    </td>
                    <td className="px-4 py-3">{precedente.institucionOrigen}</td>
                    <td className="num px-4 py-3">{formatearFechaCorta(precedente.fecha)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="p-4 pt-0">
            <Alerta tono="informacion">
              El precedente <strong>no omite</strong> el control de vigencia de 10 años, la revisión del docente ni el
              registro de la resolución del caso actual.
            </Alerta>
          </div>
        </>
      )}
    </Tarjeta>
  );
}
