import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { expedientesApi, extraccionesApi } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, Etiqueta, Tabla } from '../components/ui';
import type { ColumnaTabla, TonoEtiqueta } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { RUTAS } from '../layouts/navegacion';
import type { SolicitudAnalisis } from '../types/analisis';
import { ROLES } from '../types/comunes';
import type { Expediente } from '../types/expedientes';
import { formatearFechaHora } from '../utils/formato';
import { esFormatoActual, guardarVistas, leerVistas, solicitudesNuevas } from '../utils/solicitudes';

/** Cada cuánto se vuelve a consultar la lista del docente para mostrar las asignaciones nuevas. */
const REFRESCO_DOCENTE_MS = 60000;

interface Fila {
  expediente: Expediente;
  analisis: SolicitudAnalisis | null;
}

/** Solicitudes con certificado SIHA y su estado (análisis del agente y revisión docente). */
async function cargar(): Promise<Fila[]> {
  const expedientes = (await expedientesApi.listar()).filter(esFormatoActual);
  return Promise.all(
    expedientes.map(async (expediente) => ({
      expediente,
      analisis: await extraccionesApi.obtenerSolicitud(expediente.id).catch(() => null),
    })),
  );
}

export function estadoSolicitud(analisis: SolicitudAnalisis | null): { texto: string; tono: TonoEtiqueta } {
  if (!analisis || analisis.estado === 'PENDIENTE') return { texto: 'Analizando…', tono: 'marca' };
  if (analisis.estado === 'PREPARANDO') return { texto: 'El agente se está preparando', tono: 'marca' };
  if (analisis.estado === 'ERROR') return { texto: 'Análisis con error', tono: 'error' };
  if (analisis.estadoRevision === 'ESPERA_ADMINISTRADOR') return { texto: 'Falta la decisión del administrador', tono: 'advertencia' };
  if (analisis.resultadoFinal?.some((m) => m.resultado === 'SIN_ACUERDO'))
    return { texto: 'Docentes sin acuerdo · decide el administrador', tono: 'advertencia' };
  if (analisis.estadoRevision === 'RESUELTA') return { texto: 'Resuelta', tono: 'exito' };
  const respondieron = analisis.revisores.filter((r) => r.respondio).length;
  return { texto: `Revisión docente ${respondieron}/2`, tono: 'advertencia' };
}

/**
 * Lista única de solicitudes. El operador y el administrador ven todas; cada docente ve solo
 * las que tiene asignadas como revisor (filtro del servidor).
 */
export function SolicitudesPage(): JSX.Element {
  useTituloPagina('Solicitudes');
  const { usuario } = useAuth();
  const datos = usePeticion(cargar, []);
  const esDocente = usuario?.rol === ROLES.DOCENTE;
  const correo = usuario?.correoInstitucional?.toLowerCase() ?? '';

  // El docente ve la lista actualizarse sola: si le asignan una solicitud mientras la tiene abierta, aparece.
  const { actualizarDatos } = datos;
  useEffect(() => {
    if (!esDocente) return undefined;
    const reloj = setInterval(() => {
      cargar().then(actualizarDatos).catch(() => undefined);
    }, REFRESCO_DOCENTE_MS);
    return () => clearInterval(reloj);
  }, [esDocente, actualizarDatos]);

  const filas = datos.datos ?? [];
  const meToca = (analisis: SolicitudAnalisis | null): boolean =>
    esDocente &&
    analisis?.estado === 'ANALIZADO' &&
    analisis.revisores.some((rv) => rv.correo === correo && !rv.respondio);
  const pendientes = filas.filter((f) => meToca(f.analisis));

  // Solicitudes nuevas desde la última visita del docente (se recuerdan en este navegador).
  const [vistas, setVistas] = useState<Set<string> | null>(() => (correo ? leerVistas(correo) : null));
  const nuevas = useMemo(
    () =>
      esDocente && !datos.cargando
        ? solicitudesNuevas(
            filas.map((f) => f.expediente.id),
            vistas,
            new Set(pendientes.map((f) => f.expediente.id)),
          )
        : new Set<string>(),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [esDocente, datos.cargando, datos.datos, vistas],
  );
  // Primera visita en este navegador: lo que no espera su decisión queda como visto, para que la
  // próxima vez solo se avise de lo realmente nuevo.
  useEffect(() => {
    if (!esDocente || !correo || datos.cargando || datos.datos === null || vistas !== null) return;
    const base = new Set(datos.datos.filter((f) => !meToca(f.analisis)).map((f) => f.expediente.id));
    guardarVistas(correo, base);
    setVistas(base);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [esDocente, correo, datos.cargando, datos.datos, vistas]);

  const marcarVistas = (ids: Iterable<string>): void => {
    const todas = new Set([...(vistas ?? []), ...ids, ...filas.filter((f) => !nuevas.has(f.expediente.id)).map((f) => f.expediente.id)]);
    guardarVistas(correo, todas);
    setVistas(todas);
  };

  const columnas: Array<ColumnaTabla<Fila>> = [
    {
      clave: 'solicitud',
      encabezado: 'Solicitud',
      render: ({ expediente }) => (
        <div>
          <p className="flex items-center gap-2 text-sm font-medium text-ink">
            {expediente.estudianteNombre}
            {nuevas.has(expediente.id) ? <Etiqueta tono="exito">Nueva</Etiqueta> : null}
          </p>
          <p className="font-mono text-xs text-faint">
            {expediente.codigo} · {expediente.numeroCertificado}
          </p>
        </div>
      ),
    },
    {
      clave: 'carreras',
      encabezado: 'Cambio de carrera',
      render: ({ expediente }) => (
        <span className="text-sm">
          {expediente.carreraOrigenNombre ?? expediente.carreraOrigenCodigo} → <strong>{expediente.carreraDestinoNombre ?? expediente.carreraDestinoCodigo}</strong>
        </span>
      ),
    },
    {
      clave: 'materias',
      encabezado: 'Materias pedidas',
      className: 'num',
      render: ({ analisis, expediente }) =>
        analisis?.resultado?.evaluaciones.length ?? new Set(expediente.asignaturas.map((a) => a.solicitudNumero).filter(Boolean)).size,
    },
    {
      clave: 'revisores',
      encabezado: 'Docentes revisores',
      render: ({ expediente }) => (
        <ul className="text-xs text-muted">
          {(expediente.revisores ?? []).map((r) => (
            <li key={r.correo}>{r.nombre}</li>
          ))}
        </ul>
      ),
    },
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: ({ analisis }) => {
        const estado = estadoSolicitud(analisis);
        return (
          <div className="flex flex-col items-start gap-1">
            <Etiqueta tono={estado.tono}>{estado.texto}</Etiqueta>
            {meToca(analisis) ? <Etiqueta tono="marca">Falta su decisión</Etiqueta> : null}
          </div>
        );
      },
    },
    { clave: 'fecha', encabezado: 'Registrada', render: ({ expediente }) => formatearFechaHora(expediente.fechaCreacionUtc) },
    {
      clave: 'abrir',
      encabezado: '',
      render: ({ expediente }) => (
        <Link
          to={`/solicitudes/${expediente.id}`}
          onClick={() => (esDocente ? marcarVistas([expediente.id]) : undefined)}
          className="rounded-md bg-institucional-700 px-3 py-1.5 text-sm font-semibold text-white hover:bg-institucional-800"
        >
          Abrir
        </Link>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        titulo={esDocente ? 'Mis solicitudes por revisar' : 'Solicitudes'}
        descripcion={
          esDocente
            ? 'Solicitudes en las que usted es uno de los dos docentes revisores. Cada una ya trae el análisis del agente.'
            : 'Cada solicitud se analiza automáticamente al subir el certificado y se asigna a sus dos docentes revisores.'
        }
        acciones={
          !esDocente ? (
            <Link
              to={RUTAS.nuevaSolicitud}
              className="rounded-md bg-institucional-700 px-4 py-2 text-sm font-semibold text-white hover:bg-institucional-800"
            >
              Registrar solicitud
            </Link>
          ) : null
        }
      />
      {datos.error ? <Alerta tono="error">{datos.error}</Alerta> : null}
      {esDocente && nuevas.size > 0 ? (
        <Alerta tono="exito" titulo={nuevas.size === 1 ? 'Se le asignó una solicitud nueva' : `Se le asignaron ${nuevas.size} solicitudes nuevas`}>
          <p>
            {filas
              .filter((f) => nuevas.has(f.expediente.id))
              .map((f) => f.expediente.estudianteNombre)
              .join(', ')}
            . Ya traen el análisis del agente; ábralas para dar su decisión.
          </p>
          <Boton className="mt-2" variante="secundario" tamano="sm" onClick={() => marcarVistas(nuevas)}>
            Entendido
          </Boton>
        </Alerta>
      ) : null}
      {esDocente && pendientes.length > 0 && nuevas.size === 0 ? (
        <Alerta tono="informacion">
          {pendientes.length === 1
            ? 'Tiene 1 solicitud esperando su decisión.'
            : `Tiene ${pendientes.length} solicitudes esperando su decisión.`}
        </Alerta>
      ) : null}
      <Tabla
        columnas={columnas}
        filas={filas}
        claveFila={(f) => f.expediente.id}
        cargando={datos.cargando}
        mensajeVacio={esDocente ? 'No tiene solicitudes asignadas.' : 'Todavía no hay solicitudes registradas.'}
      />
    </div>
  );
}
