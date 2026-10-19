import type { ReactNode } from 'react';
import { Cargador } from './Cargador';

export interface ColumnaTabla<T> {
  clave: string;
  encabezado: string;
  /** Clases aplicadas a las celdas de esta columna. */
  className?: string;
  render: (fila: T) => ReactNode;
}

interface PropsTabla<T> {
  columnas: Array<ColumnaTabla<T>>;
  filas: T[];
  claveFila: (fila: T) => string | number;
  cargando?: boolean;
  /** Mensaje mostrado cuando no hay registros. */
  mensajeVacio?: string;
  /** Pie de tabla (paginación, totales). */
  pie?: ReactNode;
}

/** Tabla de datos con encabezado fijo, estado de espera y estado vacío. */
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
      <div className="flex min-h-[140px] flex-col items-center justify-center gap-1 text-sm text-slate-500">
        <span className="font-medium text-slate-600">{mensajeVacio}</span>
        <span className="text-xs">Ajuste los filtros o registre la información correspondiente.</span>
      </div>
    );
  }

  return (
    <div className="desplazable overflow-x-auto">
      <table className="min-w-full text-left text-sm">
        <caption className="sr-only">Listado de registros del sistema</caption>
        <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
          <tr>
            {columnas.map((columna) => (
              <th
                key={columna.clave}
                scope="col"
                className={['px-4 py-2.5 font-semibold', columna.className ?? ''].filter(Boolean).join(' ')}
              >
                {columna.encabezado}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {filas.map((fila) => (
            <tr key={claveFila(fila)} className="hover:bg-slate-50/70">
              {columnas.map((columna) => (
                <td
                  key={columna.clave}
                  className={['px-4 py-2.5 align-top text-slate-700', columna.className ?? '']
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
      {pie ? <div className="border-t border-slate-200 px-4 py-2 text-xs text-slate-500">{pie}</div> : null}
    </div>
  );
}
