import { Link } from 'react-router-dom';
import { IndicadorConfianza } from '../components/analisis/IndicadorConfianza';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, Etiqueta, Tabla, Tarjeta } from '../components/ui';
import type { ColumnaTabla } from '../components/ui';
import { useExtraccionesConExpediente } from '../hooks/useExtraccionesConExpediente';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { Extraccion } from '../types/analisis';
import { ESTADOS_EXTRACCION } from '../types/analisis';
import { contextoExpediente, presentarEstadoExtraccion } from '../utils/analisis';
import { formatearFechaHora } from '../utils/formato';

/**
 * Bandeja de análisis del docente evaluador (S2-A08).
 * Solo las extracciones confirmadas pueden analizarse; las demás muestran su estado de lectura.
 */
export function BandejaAnalisisPage(): JSX.Element {
  useTituloPagina('Bandeja de análisis');
  const bandeja = useExtraccionesConExpediente();
  const filas = bandeja.datos?.extracciones ?? [];
  const listas = filas.filter((fila) => fila.estado === ESTADOS_EXTRACCION.CONFIRMADA).length;

  const columnas: Array<ColumnaTabla<Extraccion>> = [
    {
      clave: 'expediente',
      encabezado: 'Expediente',
      render: (extraccion) => {
        const contexto = contextoExpediente(extraccion, bandeja.datos?.expedientes[extraccion.expedienteId]);
        return (
          <div>
            <p className="font-mono text-xs font-semibold text-ink">{contexto.codigo}</p>
            <p className="mt-0.5 text-xs text-faint">{formatearFechaHora(extraccion.creadoUtc)}</p>
          </div>
        );
      },
    },
    {
      clave: 'asignatura',
      encabezado: 'Asignatura de origen',
      render: (extraccion) => (
        <div>
          <p className="text-sm text-ink">{extraccion.nombreAsignaturaOrigen || '—'}</p>
          <p className="text-xs text-faint">
            {extraccion.codigoAsignaturaOrigen} · {extraccion.creditos ?? '—'} créditos · {extraccion.unidades.length}{' '}
            unidades
          </p>
        </div>
      ),
    },
    { clave: 'confianza', encabezado: 'Confianza', render: (extraccion) => <IndicadorConfianza extraccion={extraccion} /> },
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: (extraccion) => {
        const estado = presentarEstadoExtraccion(extraccion.estado);
        return <Etiqueta tono={estado.tono}>{estado.etiqueta}</Etiqueta>;
      },
    },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      render: (extraccion) =>
        extraccion.estado === ESTADOS_EXTRACCION.CONFIRMADA ? (
          <Link
            to={`/evaluacion/${extraccion.id}`}
            className="text-sm font-semibold text-institucional-700 hover:underline focus-visible:ring-2 focus-visible:ring-institucional-500"
          >
            Analizar<span className="sr-only"> {extraccion.nombreAsignaturaOrigen}</span>
          </Link>
        ) : (
          <Link to={`/procesamiento/${extraccion.id}/estado`} className="text-sm text-muted hover:underline">
            Ver estado
          </Link>
        ),
    },
  ];

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        titulo="Bandeja de análisis"
        descripcion="Expedientes con datos confirmados por el operador, listos para que el agente calcule créditos, cobertura de contenidos y precedentes."
      />

      <Tarjeta
        titulo="Expedientes para evaluación"
        descripcion={`${listas} de ${filas.length} listos para analizar`}
        compacta
        acciones={
          <Boton variante="secundario" tamano="sm" onClick={() => bandeja.recargar()}>
            Actualizar
          </Boton>
        }
      >
        {bandeja.error ? (
          <div className="p-4">
            <Alerta tono="error" titulo="No se pudo cargar la bandeja de análisis">
              {bandeja.error}
            </Alerta>
          </div>
        ) : (
          <Tabla
            columnas={columnas}
            filas={filas}
            claveFila={(extraccion) => extraccion.id}
            cargando={bandeja.cargando}
            mensajeVacio="Todavía no hay extracciones registradas."
          />
        )}
      </Tarjeta>
    </div>
  );
}
