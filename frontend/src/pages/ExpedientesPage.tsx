import { useState } from 'react';
import { Link } from 'react-router-dom';
import { expedientesApi, mensajeDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, Campo, Etiqueta, Selector, Tabla, Tarjeta } from '../components/ui';
import type { ColumnaTabla } from '../components/ui';
import { useMensajes } from '../hooks/useMensajes';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { Expediente } from '../types/expedientes';
import { ESTADOS_EXPEDIENTE_TODOS, ETIQUETA_ESTADO, ETIQUETA_TIPO_TRAMITE } from '../types/comunes';
import { formatearFechaHora } from '../utils/formato';

/**
 * Bandeja de expedientes (S1-A07 / S1-A09): listado con filtro por estado y
 * opción de reintentar la publicación de los expedientes que fallaron.
 */
export function ExpedientesPage(): JSX.Element {
  useTituloPagina('Bandeja de expedientes');

  const [filtroEstado, setFiltroEstado] = useState('');
  const expedientes = usePeticion(() => expedientesApi.listar(filtroEstado || undefined), [filtroEstado]);
  const { mensaje, exito, error: reportarError, limpiar } = useMensajes();
  const [republicando, setRepublicando] = useState<string | null>(null);

  const republicar = async (expediente: Expediente): Promise<void> => {
    limpiar();
    setRepublicando(expediente.id);
    try {
      const respuesta = await expedientesApi.republicar(expediente.id);
      exito(`Expediente ${respuesta.codigo}: ${respuesta.mensaje}`);
      expedientes.recargar();
    } catch (fallo) {
      reportarError(mensajeDeError(fallo));
    } finally {
      setRepublicando(null);
    }
  };

  const columnas: Array<ColumnaTabla<Expediente>> = [
    {
      clave: 'codigo',
      encabezado: 'Código',
      render: (expediente) => (
        <div>
          <p className="font-mono text-xs font-semibold text-slate-800">{expediente.codigo}</p>
          <p className="mt-0.5 text-xs text-slate-500">{formatearFechaHora(expediente.fechaCreacionUtc)}</p>
        </div>
      ),
    },
    {
      clave: 'tramite',
      encabezado: 'Trámite',
      render: (expediente) => (
        <div className="space-y-1">
          <Etiqueta tono="informacion">{ETIQUETA_TIPO_TRAMITE[expediente.tipoTramite] ?? expediente.tipoTramite}</Etiqueta>
          <p className="text-xs text-slate-500">{expediente.rutaNormativa}</p>
        </div>
      ),
    },
    {
      clave: 'carreras',
      encabezado: 'Ruta académica',
      render: (expediente) => (
        <span className="text-xs text-slate-600">
          {expediente.carreraOrigenCodigo} → {expediente.carreraDestinoCodigo}
        </span>
      ),
    },
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: (expediente) => (
        <Etiqueta
          tono={
            expediente.estado === 'EVALUADO'
              ? 'exito'
              : expediente.estado === 'REGISTRADO_PENDIENTE_PUBLICACION'
                ? 'error'
                : 'advertencia'
          }
        >
          {ETIQUETA_ESTADO[expediente.estado] ?? expediente.estado}
        </Etiqueta>
      ),
    },
    {
      clave: 'publicacion',
      encabezado: 'Publicación',
      render: (expediente) => (
        <div className="space-y-1 text-xs">
          <Etiqueta tono={expediente.eventoPublicado ? 'exito' : 'error'}>
            {expediente.eventoPublicado ? 'Publicado' : 'No publicado'}
          </Etiqueta>
          <p className="text-slate-500">Intentos: {expediente.intentosPublicacion}</p>
          {expediente.ultimoErrorPublicacion ? (
            <p className="max-w-xs text-rose-600" title={expediente.ultimoErrorPublicacion}>
              {expediente.ultimoErrorPublicacion.slice(0, 80)}
            </p>
          ) : null}
        </div>
      ),
    },
    {
      clave: 'contenido',
      encabezado: 'Contenidos',
      render: (expediente) => (
        <span className="text-xs text-slate-600">
          {expediente.documentos.length} doc · {expediente.asignaturas.length} materia(s)
        </span>
      ),
    },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      render: (expediente) => (
        <div className="flex flex-col gap-1">
          <Link
            to={`/expedientes/${expediente.id}`}
            className="text-sm font-semibold text-institucional-700 hover:underline"
          >
            Ver detalle
          </Link>
          {!expediente.eventoPublicado ? (
            <Boton
              variante="fantasma"
              tamano="sm"
              cargando={republicando === expediente.id}
              textoCargando="Republicando…"
              onClick={() => void republicar(expediente)}
            >
              Reintentar publicación
            </Boton>
          ) : null}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Expedientes"
        titulo="Bandeja de expedientes"
        descripcion="Expedientes recibidos por el Microservicio B, ordenados por fecha de creación (se muestran los últimos 100)."
      />

      {mensaje ? (
        <Alerta tono={mensaje.tipo === 'exito' ? 'exito' : 'error'} detalles={mensaje.detalles}>
          {mensaje.texto}
        </Alerta>
      ) : null}

      <Tarjeta
        titulo="Expedientes"
        descripcion={`${expedientes.datos?.length ?? 0} expediente(s) en el listado.`}
        compacta
        acciones={
          <div className="flex flex-wrap items-end gap-2">
            <div className="w-56">
              <Campo htmlFor="filtro-estado" etiqueta="Estado">
                <Selector
                  id="filtro-estado"
                  value={filtroEstado}
                  onChange={(evento) => setFiltroEstado(evento.target.value)}
                >
                  <option value="">Todos los estados</option>
                  {ESTADOS_EXPEDIENTE_TODOS.map((estado) => (
                    <option key={estado} value={estado}>
                      {ETIQUETA_ESTADO[estado] ?? estado}
                    </option>
                  ))}
                </Selector>
              </Campo>
            </div>
            <Boton variante="secundario" tamano="sm" onClick={() => expedientes.recargar()}>
              Actualizar
            </Boton>
          </div>
        }
      >
        {expedientes.error ? (
          <div className="p-4">
            <Alerta tono="error" titulo="No se pudo cargar la bandeja de expedientes">
              {expedientes.error}
            </Alerta>
          </div>
        ) : (
          <Tabla
            columnas={columnas}
            filas={expedientes.datos ?? []}
            claveFila={(expediente) => expediente.id}
            cargando={expedientes.cargando}
            mensajeVacio="No hay expedientes registrados para el estado seleccionado."
            pie={`${expedientes.datos?.length ?? 0} expediente(s)`}
          />
        )}
      </Tarjeta>
    </div>
  );
}
