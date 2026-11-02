import type { ReactNode } from 'react';
import { Cargador } from './Cargador';

export interface ColumnaTabla<T> {
  clave: string;
  encabezado: string;
  className?: string;
  render: (fila: T) => ReactNode;
}

interface PropsTabla<T> {
  columnas: Array<ColumnaTabla<T>>;
  filas: T[];
  claveFila: (fila: T) => string | number;
  cargando?: boolean;
  mensajeVacio?: string;
  pie?: ReactNode;
}

/**
 * Tabla: 12px grey sentence-case headers, no fill, no rule under header.
 * Rows on hairline rules. Numbers right-aligned via `num` class on columns.
 */
export function Tabla<T>({
  columnas,
  filas,
  claveFila,
  cargando = false,
  mensajeVacio = 'No hay registros para mostrar.',
  pie,
}: PropsTabla<T>): JSX.Element {
  if (cargando) {
    return (
      <div className="flex min-h-[140px] items-center justify-center">
        <Cargador />
      </div>
    );
  }

  if (filas.length === 0) {
    return (
      <div className="flex min-h-[140px] flex-col items-center justify-center gap-1 text-sm text-muted">
        <span className="font-medium text-ink">{mensajeVacio}</span>
        <span className="text-xs">Ajuste los filtros o registre la información correspondiente.</span>
      </div>
    );
  }

  return (
    <div className="desplazable overflow-x-auto">
      <table className="min-w-full text-left text-sm">
        <caption className="sr-only">Listado de registros del sistema</caption>
        <thead>
          <tr>
            {columnas.map((columna) => (
              <th
                key={columna.clave}
                scope="col"
                className={['px-4 py-2 text-xs font-normal text-faint', columna.className ?? '']
                  .filter(Boolean)
                  .join(' ')}
              >
                {columna.encabezado}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-stone-100">
          {filas.map((fila) => (
            <tr key={claveFila(fila)} className="transition-colors hover:bg-stone-50">
              {columnas.map((columna) => (
                <td
                  key={columna.clave}
                  className={['px-4 py-3 align-top text-muted', columna.className ?? '']
                    .filter(Boolean)
                    .join(' ')}
                >
                  {columna.render(fila)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {pie ? <div className="border-t border-stone-100 px-4 py-2 text-xs text-faint">{pie}</div> : null}
    </div>
  );
}
