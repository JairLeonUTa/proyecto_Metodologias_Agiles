import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { detallesDeError, expedientesApi, extraccionesApi, mensajeDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, AreaTexto, Boton, CargandoBloque, Etiqueta, Tabla, Tarjeta } from '../components/ui';
import type { ColumnaTabla, TonoEtiqueta } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { RUTAS } from '../layouts/navegacion';
import type { CoberturaUnidadPropuesta, PropuestaHomologacion, SolicitudAnalisis } from '../types/analisis';
import type { Expediente } from '../types/expedientes';
import { anosDesdeFecha, formatearPorcentaje } from '../utils/analisis';
import { estadoSolicitud } from './SolicitudesPage';

type AsignaturaExp = Expediente['asignaturas'][number];

const RESULTADO: Record<string, { texto: string; tono: TonoEtiqueta }> = {
  PENDIENTE: { texto: 'Pendiente', tono: 'neutro' },
  HOMOLOGADA: { texto: 'Homologada', tono: 'exito' },
  NO_HOMOLOGADA: { texto: 'No homologada', tono: 'error' },
  SIN_ACUERDO: { texto: 'Sin acuerdo entre docentes', tono: 'advertencia' },
};

const COLUMNAS_MATERIAS: Array<ColumnaTabla<AsignaturaExp>> = [
  {
    clave: 'materia',
    encabezado: 'Materia aprobada',
    render: (a) => (
      <div>
        <p className="text-sm text-ink">{a.nombreOrigen}</p>
        <p className="font-mono text-xs text-faint">{a.codigoOrigen}</p>
      </div>
    ),
  },
  { clave: 'semestre', encabezado: 'Semestre', className: 'num', render: (a) => a.semestre ?? '—' },
  { clave: 'creditos', encabezado: 'Créditos', className: 'num', render: (a) => a.creditos ?? '—' },
  { clave: 'horas', encabezado: 'Horas', className: 'num', render: (a) => a.horas ?? '—' },
  {
    clave: 'fecha',
    encabezado: 'Aprobada el',
    render: (a) => {
      const anos = anosDesdeFecha(a.fechaAprobacionCertificada);
      return a.fechaAprobacionCertificada ? `${a.fechaAprobacionCertificada}${anos !== null ? ` (hace ${anos} años)` : ''}` : '—';
    },
  },
  { clave: 'nota', encabezado: 'Nota', className: 'num', render: (a) => (a.nota !== null ? a.nota.toFixed(2) : '—') },
];

const COLUMNAS_UNIDADES: Array<ColumnaTabla<CoberturaUnidadPropuesta>> = [
  { clave: 'unidad', encabezado: 'Unidad de destino', render: (u) => `U${u.numero}. ${u.nombre}` },
  { clave: 'horas', encabezado: 'Horas', className: 'num', render: (u) => u.horas },
  {
    clave: 'origen',
    encabezado: 'Más parecido en lo aprobado',
    render: (u) => (u.codigoOrigen ? `${u.codigoOrigen} · U${u.numeroUnidadOrigen}. ${u.nombreUnidadOrigen}` : '—'),
  },
  { clave: 'parecido', encabezado: 'Parecido', className: 'num', render: (u) => formatearPorcentaje(u.similitud) },
  {
    clave: 'cubierta',
    encabezado: 'Cubierta',
    render: (u) => <Etiqueta tono={u.cubierta ? 'exito' : 'error'}>{u.cubierta ? 'Sí' : 'No'}</Etiqueta>,
  },
];

/** Una materia de destino con la estimación del agente y el detalle por unidad (plegable). */
function Estimacion({ p, cumple }: { p: PropuestaHomologacion; cumple: boolean }): JSX.Element {
  return (
    <details className="rounded-lg border border-slate-200 px-4 py-3">
      <summary className="flex cursor-pointer flex-wrap items-center gap-x-3 gap-y-1">
        <span className="font-semibold text-ink">
          {p.codigoDestino} · {p.nombreDestino}
        </span>
        <span className="text-sm text-muted">← {p.origenes.map((o) => `${o.codigo} ${o.nombre}`).join(' + ')}</span>
        <span className="ml-auto flex items-center gap-2">
          <span className="text-sm font-semibold">{formatearPorcentaje(p.pcont)}</span>
          <Etiqueta tono={cumple ? 'exito' : 'advertencia'}>{cumple ? 'Cumple' : 'A criterio del docente'}</Etiqueta>
        </span>
      </summary>
      <div className="mt-3 space-y-3">
        {p.alertas.map((a) => (
          <p key={a} className="text-sm text-muted">
            {a}
          </p>
        ))}
        <Tabla columnas={COLUMNAS_UNIDADES} filas={p.unidades} claveFila={(u) => u.numero} />
        {p.precedentes.length > 0 ? (
          <p className="text-xs text-muted">Precedentes: {p.precedentes.map((x) => x.resolucion).join(', ')}</p>
        ) : null}
      </div>
    </details>
  );
}

/** Formulario de respuesta final del docente revisor (de acuerdo o no con el agente, materia por materia). */
function FormularioRespuesta({ solicitud, alGuardar }: { solicitud: SolicitudAnalisis; alGuardar: (s: SolicitudAnalisis) => void }): JSX.Element {
  const { usuario } = useAuth();
  const previa = solicitud.dictamenes.find((d) => d.correoDocente === usuario?.correoInstitucional?.toLowerCase());
  const [decisiones, setDecisiones] = useState<Record<string, boolean | undefined>>(() =>
    Object.fromEntries(
      solicitud.resultadoFinal.map((m) => [
        m.codigoDestino,
        previa?.decisiones.find((d) => d.codigoDestino === m.codigoDestino)?.aprobada ?? (m.estimacion === 'CUMPLE' ? true : undefined),
      ]),
    ),
  );
  const [comentario, setComentario] = useState(previa?.comentario ?? '');
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState<{ texto: string; detalles: string[] } | null>(null);
  const completo = solicitud.resultadoFinal.every((m) => decisiones[m.codigoDestino] !== undefined);

  const enviar = async (): Promise<void> => {
    setEnviando(true);
    setError(null);
    try {
      alGuardar(
        await extraccionesApi.responder(solicitud.expedienteId, {
          decisiones: solicitud.resultadoFinal.map((m) => ({
            codigoDestino: m.codigoDestino,
            aprobada: decisiones[m.codigoDestino] === true,
            observacion: null,
          })),
          comentario: comentario.trim() || null,
        }),
      );
    } catch (fallo) {
      setError({ texto: mensajeDeError(fallo), detalles: detallesDeError(fallo) });
    } finally {
      setEnviando(false);
    }
  };

  return (
    <div className="space-y-3">
      {solicitud.resultadoFinal.map((m) => (
        <fieldset key={m.codigoDestino} className="flex flex-wrap items-center gap-3 rounded-md border border-slate-200 px-3 py-2">
          <legend className="sr-only">
            {m.codigoDestino} {m.nombreDestino}
          </legend>
          <span className="min-w-56 text-sm font-medium text-ink">
            {m.codigoDestino} · {m.nombreDestino}
          </span>
          <Etiqueta tono={m.estimacion === 'CUMPLE' ? 'exito' : 'advertencia'}>
            Agente: {m.estimacion === 'CUMPLE' ? 'cumple' : 'a su criterio'}
          </Etiqueta>
          <label className="ml-auto flex items-center gap-1 text-sm">
            <input
              type="radio"
              name={`decision-${m.codigoDestino}`}
              checked={decisiones[m.codigoDestino] === true}
              onChange={() => setDecisiones({ ...decisiones, [m.codigoDestino]: true })}
            />
            Aprobar
          </label>
          <label className="flex items-center gap-1 text-sm">
            <input
              type="radio"
              name={`decision-${m.codigoDestino}`}
              checked={decisiones[m.codigoDestino] === false}
              onChange={() => setDecisiones({ ...decisiones, [m.codigoDestino]: false })}
            />
            No aprobar
          </label>
        </fieldset>
      ))}
      <AreaTexto
        aria-label="Comentario para la resolución"
        placeholder="Comentario (opcional): por qué está o no de acuerdo con el análisis"
        value={comentario}
        onChange={(e) => setComentario(e.target.value)}
        rows={3}
      />
      {error ? (
        <Alerta tono="error" detalles={error.detalles}>
          {error.texto}
        </Alerta>
      ) : null}
      <Boton onClick={() => void enviar()} cargando={enviando} textoCargando="Enviando…" disabled={!completo}>
        {previa ? 'Actualizar mi respuesta' : 'Enviar mi respuesta'}
      </Boton>
    </div>
  );
}

/**
 * Detalle de una solicitud: datos del certificado, estimación del agente y respuesta final de los dos
 * docentes revisores. Todo en una sola pantalla.
 */
export function SolicitudDetallePage(): JSX.Element {
  useTituloPagina('Solicitud');
  const { id = '' } = useParams();
  const [expediente, setExpediente] = useState<Expediente | null>(null);
  const [solicitud, setSolicitud] = useState<SolicitudAnalisis | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [reanalizando, setReanalizando] = useState(false);

  useEffect(() => {
    let activo = true;
    let temporizador: ReturnType<typeof setTimeout> | undefined;
    const cargar = async (): Promise<void> => {
      try {
        const exp = await expedientesApi.obtener(id);
        const analisis = await extraccionesApi.obtenerSolicitud(id).catch(() => null);
        if (!activo) return;
        setExpediente(exp);
        setSolicitud(analisis);
        // Mientras el agente trabaja, se consulta de nuevo cada 3 segundos.
        if (!analisis || analisis.estado === 'PENDIENTE') temporizador = setTimeout(() => void cargar(), 3000);
      } catch (fallo) {
        if (activo) setError(mensajeDeError(fallo));
      }
    };
    void cargar();
    return () => {
      activo = false;
      if (temporizador) clearTimeout(temporizador);
    };
  }, [id]);

  if (error) {
    return (
      <Alerta tono="error" titulo="No se pudo abrir la solicitud">
        {error}
      </Alerta>
    );
  }
  if (!expediente) return <CargandoBloque />;

  const estado = estadoSolicitud(solicitud);
  const resultado = solicitud?.resultado ?? null;

  const reanalizar = async (): Promise<void> => {
    setReanalizando(true);
    try {
      setSolicitud(await extraccionesApi.reanalizar(id));
    } catch (fallo) {
      setError(mensajeDeError(fallo));
    } finally {
      setReanalizando(false);
    }
  };

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        titulo={expediente.estudianteNombre ?? expediente.codigo}
        descripcion={`${expediente.codigo} · ${expediente.numeroCertificado ?? ''} · ${expediente.carreraOrigenCodigo} → ${expediente.carreraDestinoCodigo} · ${expediente.rutaNormativa} · ${expediente.institucionOrigen ?? ''}`}
        acciones={
          <div className="flex items-center gap-3">
            <Etiqueta tono={estado.tono}>{estado.texto}</Etiqueta>
            <Link to={RUTAS.solicitudes} className="text-sm font-semibold text-institucional-700 hover:underline">
              Volver
            </Link>
          </div>
        }
      />

      <Tarjeta
        titulo="Certificado"
        descripcion={`Docentes revisores: ${(expediente.revisores ?? []).map((r) => `${r.nombre} (${r.correo})`).join(' y ')}`}
      >
        <Tabla columnas={COLUMNAS_MATERIAS} filas={expediente.asignaturas} claveFila={(a) => a.codigoOrigen} />
      </Tarjeta>

      <Tarjeta
        titulo="Análisis del agente"
        descripcion="Compara los contenidos de cada materia aprobada con la malla de la carrera de destino. Cumple con 80 % o más; si sale menos, decide el docente."
      >
        {!solicitud || solicitud.estado === 'PENDIENTE' ? (
          <Alerta tono="informacion">El agente está comparando los contenidos… esta pantalla se actualiza sola.</Alerta>
        ) : solicitud.estado === 'ERROR' ? (
          <div className="space-y-3">
            <Alerta tono="error" titulo="El análisis no pudo completarse">
              {solicitud.mensaje}
            </Alerta>
            <Boton onClick={() => void reanalizar()} cargando={reanalizando} textoCargando="Analizando…">
              Volver a analizar
            </Boton>
          </div>
        ) : resultado ? (
          <div className="space-y-3">
            {resultado.propuestas.map((p) => (
              <Estimacion key={p.codigoDestino} p={p} cumple />
            ))}
            {resultado.sinCoberturaSuficiente.map((p) => (
              <Estimacion key={p.codigoDestino} p={p} cumple={false} />
            ))}
            {resultado.propuestas.length + resultado.sinCoberturaSuficiente.length === 0 ? (
              <Alerta tono="informacion">Ninguna materia aprobada tiene contenidos equivalentes en la carrera de destino.</Alerta>
            ) : null}
            {resultado.origenesSinAsignar.length > 0 ? (
              <p className="text-sm text-muted">
                Sin equivalente en la carrera de destino: {resultado.origenesSinAsignar.map((o) => `${o.codigo} ${o.nombre}`).join(', ')}.
              </p>
            ) : null}
          </div>
        ) : null}
      </Tarjeta>

      {solicitud?.estado === 'ANALIZADO' && solicitud.resultadoFinal.length > 0 ? (
        <Tarjeta
          titulo="Respuesta de los docentes"
          descripcion="Cada revisor indica si aprueba o no cada materia. Queda homologada cuando los dos la aprueban."
        >
          <div className="space-y-4">
            <ul className="space-y-2">
              {solicitud.resultadoFinal.map((m) => (
                <li key={m.codigoDestino} className="flex flex-wrap items-center gap-3 text-sm">
                  <span className="min-w-56 font-medium text-ink">
                    {m.codigoDestino} · {m.nombreDestino}
                  </span>
                  <span className="text-muted">
                    {m.aprobaciones} aprueba(n) · {m.rechazos} no aprueba(n)
                  </span>
                  <Etiqueta tono={RESULTADO[m.resultado]?.tono ?? 'neutro'}>{RESULTADO[m.resultado]?.texto ?? m.resultado}</Etiqueta>
                </li>
              ))}
            </ul>
            <ul className="space-y-1 text-sm text-muted">
              {solicitud.revisores.map((r) => {
                const dictamen = solicitud.dictamenes.find((d) => d.correoDocente === r.correo);
                return (
                  <li key={r.correo}>
                    <strong className="text-ink">{r.nombre}</strong>: {r.respondio ? 'respondió' : 'pendiente'}
                    {dictamen?.comentario ? ` — “${dictamen.comentario}”` : ''}
                  </li>
                );
              })}
            </ul>
            {solicitud.puedeResponder ? <FormularioRespuesta solicitud={solicitud} alGuardar={setSolicitud} /> : null}
          </div>
        </Tarjeta>
      ) : null}
    </div>
  );
}
