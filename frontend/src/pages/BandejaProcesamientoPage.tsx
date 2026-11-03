import { useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { IndicadorConfianza } from '../components/analisis/IndicadorConfianza';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, Campo, Etiqueta, Selector, Tabla, Tarjeta } from '../components/ui';
import type { ColumnaTabla } from '../components/ui';
import { useExtraccionesConExpediente } from '../hooks/useExtraccionesConExpediente';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { Extraccion } from '../types/analisis';
import { ESTADOS_EXTRACCION } from '../types/analisis';
import { contextoExpediente, presentarEstadoExtraccion, rutaAccionExtraccion } from '../utils/analisis';
import { formatearFechaHora } from '../utils/formato';

const FILTROS = [
  { valor: '', etiqueta: 'Todos los estados' },
  { valor: ESTADOS_EXTRACCION.PENDIENTE_CONFIRMACION, etiqueta: 'Confirmación pendiente' },
  { valor: ESTADOS_EXTRACCION.PENDIENTE_OCR_LLM, etiqueta: 'Pendiente OCR / LLM' },
  { valor: ESTADOS_EXTRACCION.CONFIRMADA, etiqueta: 'Datos extraídos' },
];

/**
 * Bandeja de procesamiento (S2-A01, US-07, mockup M-01).
 * Lista los expedientes que el Microservicio C está leyendo, con su confianza de extracción
 * y la alerta "Confirmación pendiente" cuando la confianza es menor a 0,80.
 */
export function BandejaProcesamientoPage(): JSX.Element {
  useTituloPagina('Bandeja de procesamiento');
  const ubicacion = useLocation();
  const avisoInicial = (ubicacion.state as { aviso?: string } | null)?.aviso ?? null;

  const [filtro, setFiltro] = useState('');
  const bandeja = useExtraccionesConExpediente(filtro);
  const filas = bandeja.datos?.extracciones ?? [];
  const pendientes = filas.filter((fila) => fila.estado === ESTADOS_EXTRACCION.PENDIENTE_CONFIRMACION).length;

  const columnas: Array<ColumnaTabla<Extraccion>> = [
    {
      clave: 'codigo',
      encabezado: 'Código',
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
      clave: 'tramite',
      encabezado: 'Trámite',
      render: (extraccion) => {
        const contexto = contextoExpediente(extraccion, bandeja.datos?.expedientes[extraccion.expedienteId]);
        return contexto.tipoTramite ? (
          <div className="space-y-1">
            <Etiqueta tono="informacion">{contexto.tipoTramite === 'EXTERNO' ? 'Externo' : 'Interno'}</Etiqueta>
            <p className="text-xs text-faint">{contexto.rutaAcademica}</p>
          </div>
        ) : (
          <span className="text-xs text-faint">—</span>
        );
      },
    },
    {
      clave: 'documento',
      encabezado: 'Documento',
      render: (extraccion) => {
        const contexto = contextoExpediente(extraccion, bandeja.datos?.expedientes[extraccion.expedienteId]);
        return (
          <div>
            <p className="text-sm text-ink">{contexto.documento ?? extraccion.nombreAsignaturaOrigen}</p>
            <p className="text-xs text-faint">{extraccion.codigoAsignaturaOrigen}</p>
          </div>
        );
      },
    },
    {
      clave: 'confianza',
      encabezado: 'Confianza',
      render: (extraccion) => <IndicadorConfianza extraccion={extraccion} />,
    },
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
      render: (extraccion) => (
        <Link
          to={rutaAccionExtraccion(extraccion)}
          className="text-sm font-semibold text-institucional-700 hover:underline focus-visible:ring-2 focus-visible:ring-institucional-500"
        >
          {presentarEstadoExtraccion(extraccion.estado).accion}
          <span className="sr-only"> de {extraccion.nombreAsignaturaOrigen}</span>
        </Link>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        titulo="Bandeja de procesamiento"
        descripcion="Expedientes que el Microservicio C está leyendo. Los que tienen confianza menor a 0,80 esperan la confirmación del operador."
      />

      {avisoInicial ? <Alerta tono="exito">{avisoInicial}</Alerta> : null}

      {pendientes > 0 ? (
        <Alerta tono="advertencia" titulo="Confirmación pendiente">
          {pendientes} extracción(es) con datos dudosos. El cálculo no avanza hasta que el operador los confirme.
        </Alerta>
      ) : null}

      {bandeja.datos?.sinDatosExpediente ? (
        <Alerta tono="informacion">
          No se pudieron leer los expedientes del Microservicio B; se muestra el identificador corto del expediente.
        </Alerta>
      ) : null}

      <Tarjeta
        titulo="Expedientes en procesamiento"
        descripcion={`${filas.length} expediente(s)`}
        compacta
        acciones={
          <div className="flex flex-wrap items-end gap-2">
            <div className="w-56">
              <Campo htmlFor="filtro-estado-extraccion" etiqueta="Estado">
                <Selector
                  id="filtro-estado-extraccion"
                  value={filtro}
                  onChange={(evento) => setFiltro(evento.target.value)}
                >
                  {FILTROS.map((opcion) => (
                    <option key={opcion.valor} value={opcion.valor}>
                      {opcion.etiqueta}
                    </option>
                  ))}
                </Selector>
              </Campo>
            </div>
            <Boton variante="secundario" tamano="sm" onClick={() => bandeja.recargar()}>
              Actualizar
            </Boton>
          </div>
        }
      >
        {bandeja.error ? (
          <div className="p-4">
            <Alerta
              tono="error"
              titulo="No se pudo cargar la bandeja de procesamiento"
              accion={
                <Boton variante="secundario" tamano="sm" onClick={() => bandeja.recargar()}>
                  Reintentar
                </Boton>
              }
            >
              {bandeja.error}
            </Alerta>
          </div>
        ) : (
          <Tabla
            columnas={columnas}
            filas={filas}
            claveFila={(extraccion) => extraccion.id}
            cargando={bandeja.cargando}
            mensajeVacio="No hay expedientes en procesamiento para el estado seleccionado."
            pie={`Se muestran las últimas 100 extracciones · umbral de confianza 0,80`}
          />
        )}
      </Tarjeta>
    </div>
  );
}
