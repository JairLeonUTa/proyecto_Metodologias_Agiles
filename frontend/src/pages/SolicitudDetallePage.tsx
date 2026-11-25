import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import { detallesDeError, expedientesApi, extraccionesApi, mensajeDeError } from '../api';
import { ErrorApi } from '../api/httpClient';
import { Alerta, AreaTexto, Boton, CargandoBloque, Etiqueta, Tabla } from '../components/ui';
import type { ColumnaTabla, TonoEtiqueta } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { RUTAS } from '../layouts/navegacion';
import type { CoberturaUnidad, EvaluacionMateria, ResultadoFinalMateria, SolicitudAnalisis } from '../types/analisis';
import { ROLES } from '../types/comunes';
import { formatearPorcentaje } from '../utils/analisis';
import { estadoSolicitud } from './SolicitudesPage';

const MINIMO = 80;

const RESULTADO: Record<string, { texto: string; tono: TonoEtiqueta }> = {
  PENDIENTE: { texto: 'Esperando a los docentes', tono: 'neutro' },
  HOMOLOGADA: { texto: 'Homologada', tono: 'exito' },
  NO_HOMOLOGADA: { texto: 'No homologada', tono: 'error' },
  SIN_ACUERDO: { texto: 'Los docentes no coinciden', tono: 'advertencia' },
};

const nota = (n: number): string => n.toFixed(2).replace('.', ',');

/** Bloque numerado: guía al usuario paso a paso. */
function Paso({ id, numero, titulo, ayuda, children }: { id?: string; numero: number; titulo: string; ayuda?: string; children: ReactNode }): JSX.Element {
  return (
    <section id={id} className="scroll-mt-4 rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <header className="mb-4 flex items-start gap-3">
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-institucional-700 text-sm font-bold text-white">
          {numero}
        </span>
        <div>
          <h2 className="text-lg font-semibold text-ink">{titulo}</h2>
          {ayuda ? <p className="text-sm text-muted">{ayuda}</p> : null}
        </div>
      </header>
      {children}
    </section>
  );
}

/**
 * Recomendación del agente: CUMPLE (llega al 80 % en contenidos y créditos), NO_CUMPLE (no llega: recomienda no
 * homologar) o sin sílabo. Es solo una recomendación: la decisión es siempre de los dos docentes revisores.
 */
function Veredicto({ estimacion, sinSilabo = false, bloqueadaPorCreditos = false }: { estimacion: string; sinSilabo?: boolean; bloqueadaPorCreditos?: boolean }): JSX.Element {
  if (bloqueadaPorCreditos) return <Etiqueta tono="error">No se puede homologar</Etiqueta>;
  if (sinSilabo) return <Etiqueta tono="neutro">Sin sílabo para comparar · decide el docente</Etiqueta>;
  if (estimacion === 'CUMPLE') return <Etiqueta tono="exito">✔ Cumple el {MINIMO} % · recomienda homologar</Etiqueta>;
  if (estimacion === 'NO_CUMPLE') return <Etiqueta tono="advertencia">✘ No llega al {MINIMO} % · recomienda no homologar</Etiqueta>;
  return <Etiqueta tono="advertencia">? A criterio del docente</Etiqueta>;
}

/** Barra de 0 a 100 % con la marca del mínimo exigido (80 %). */
function Medidor({ etiqueta, valor, ayuda }: { etiqueta: string; valor: number; ayuda: string }): JSX.Element {
  const ok = valor >= MINIMO;
  return (
    <div>
      <div className="flex items-baseline justify-between">
        <span className="text-sm font-medium text-ink">{etiqueta}</span>
        <span className={`text-lg font-bold ${ok ? 'text-emerald-700' : 'text-amber-700'}`}>{formatearPorcentaje(valor)}</span>
      </div>
      <div className="relative mt-1 h-3 rounded-full bg-slate-100" role="img" aria-label={`${etiqueta}: ${valor} %`}>
        <div className={`h-3 rounded-full ${ok ? 'bg-emerald-500' : 'bg-amber-400'}`} style={{ width: `${Math.min(100, valor)}%` }} />
        <div className="absolute -top-1 h-5 w-0.5 bg-slate-500" style={{ left: `${MINIMO}%` }} title={`Mínimo ${MINIMO} %`} />
      </div>
      <p className="mt-1 text-xs text-faint">{ayuda}</p>
    </div>
  );
}

const COLUMNAS_UNIDADES: Array<ColumnaTabla<CoberturaUnidad>> = [
  { clave: 'unidad', encabezado: 'Unidad que exige la carrera de destino', render: (u) => `${u.numero}. ${u.nombre} (${u.horas} h)` },
  {
    clave: 'origen',
    encabezado: 'Lo más parecido en lo aprobado',
    render: (u) => (u.materiaOrigen ? `${u.materiaOrigen} · unidad ${u.numeroUnidadOrigen} (${u.horasUnidadOrigen} h)` : '—'),
  },
  { clave: 'parecido', encabezado: 'Parecido', className: 'num', render: (u) => formatearPorcentaje(u.similitud) },
  {
    clave: 'cubierta',
    encabezado: '¿Se cubre?',
    render: (u) => <Etiqueta tono={u.cubierta ? 'exito' : 'error'}>{u.cubierta ? 'Sí' : 'No'}</Etiqueta>,
  },
];

/** Análisis de una materia pedida, con su explicación completa. */
function AnalisisMateria({ e, carreraOrigen }: { e: EvaluacionMateria; carreraOrigen: string }): JSX.Element {
  const cumple = e.estimacion === 'CUMPLE';
  const creditosOrigen = e.creditosOrigen ?? null;
  const ayudaCreditos = e.creditosPorHoras
    ? 'No se conocían los créditos: se compararon las horas, que es lo mismo.'
    : creditosOrigen !== null && e.creditosDestino
      ? `${creditosOrigen} crédito(s) aprobado(s) frente a ${e.creditosDestino}.`
      : 'Créditos aprobados frente a los de la materia de destino.';
  return (
    <article className="space-y-4 rounded-lg border border-slate-200 p-4">
      <header className="flex flex-wrap items-center gap-3">
        <h3 className="text-base font-semibold text-ink">{e.materiaDestino}</h3>
        <span className="text-sm text-muted">
          ← {e.materiasOrigen.map((o) => o.nombre).join(' + ')} ({carreraOrigen})
        </span>
        <span className="ml-auto">
          <Veredicto estimacion={e.estimacion} sinSilabo={e.sinSilabo} bloqueadaPorCreditos={e.bloqueadaPorCreditos} />
        </span>
      </header>

      {e.unidades.length > 0 && !e.sinSilabo ? (
        <div className="space-y-3">
          {/* Lo que decide: 80 % en contenidos y 80 % en créditos, sí o sí. */}
          <div className="grid gap-4 md:grid-cols-2">
            <Medidor etiqueta="Contenidos" valor={e.contenidos} ayuda="Temas de la materia de destino que ya estudió." />
            <Medidor etiqueta="Créditos" valor={e.porcentajeCreditos ?? e.cargaHoraria} ayuda={ayudaCreditos} />
          </div>
          <p className="text-xs text-faint">
            Solo como dato: profundidad {formatearPorcentaje(e.profundidad)} (horas dedicadas a esos temas) · carga horaria{' '}
            {formatearPorcentaje(e.cargaHoraria)} (horas totales aprobadas). No deciden el resultado.
          </p>
        </div>
      ) : null}

      <p
        className={`rounded-md px-3 py-2 text-sm font-medium ${
          e.sinSilabo ? 'bg-slate-100 text-slate-800' : cumple ? 'bg-emerald-50 text-emerald-900' : 'bg-amber-50 text-amber-900'
        }`}
      >
        {e.bloqueadaPorCreditos ? 'Los créditos no llegan al 80 %.' : e.conclusion}
      </p>

      <div>
        <p className="text-sm font-semibold text-ink">¿Por qué?</p>
        <ul className="mt-1 space-y-1 text-sm text-slate-700">
          {e.explicacion.map((linea) => (
            <li key={linea}>{linea}</li>
          ))}
        </ul>
      </div>

      {e.unidades.length > 0 && !e.sinSilabo ? (
        <details>
          <summary className="cursor-pointer text-sm font-semibold text-institucional-700">Ver la comparación en una tabla</summary>
          <div className="mt-2">
            <Tabla columnas={COLUMNAS_UNIDADES} filas={e.unidades} claveFila={(u) => u.numero} />
          </div>
        </details>
      ) : null}
    </article>
  );
}

/**
 * La solicitud no llega al agente: normalmente porque el agente (MS C) estaba apagado cuando se registró.
 * Se explica en lenguaje simple y el operador o el administrador pueden volver a enviarla.
 */
function NoLlegaAlAgente({ id, agenteApagado }: { id: string; agenteApagado: boolean }): JSX.Element {
  const { usuario } = useAuth();
  const puedeReenviar = usuario?.rol === ROLES.OPERADOR || usuario?.rol === ROLES.ADMINISTRADOR;
  const [enviando, setEnviando] = useState(false);
  const [aviso, setAviso] = useState<{ tono: 'exito' | 'error'; texto: string } | null>(null);

  const reenviar = async (): Promise<void> => {
    setEnviando(true);
    setAviso(null);
    try {
      await expedientesApi.reenviarAgente(id);
      setAviso({ tono: 'exito', texto: 'Se volvió a enviar al agente. En unos segundos esta pantalla mostrará el análisis.' });
    } catch (fallo) {
      setAviso({ tono: 'error', texto: mensajeDeError(fallo) });
    } finally {
      setEnviando(false);
    }
  };

  return (
    <Alerta tono="advertencia" titulo={agenteApagado ? 'El agente no está respondiendo' : 'La solicitud todavía no llega al agente'}>
      <p>
        {agenteApagado
          ? 'El servicio del agente (MS C) no responde, así que no puede analizar la solicitud.'
          : 'La solicitud quedó registrada, pero el agente no la ha recibido. Suele pasar si el agente estaba apagado cuando se registró.'}{' '}
        Quien administra el sistema debe encenderlo con <code>docker compose --profile sprint23 up -d</code>.
        {puedeReenviar ? ' Cuando esté encendido, pulse «Volver a enviar al agente».' : ''}
      </p>
      {puedeReenviar ? (
        <Boton className="mt-2" onClick={() => void reenviar()} cargando={enviando} textoCargando="Enviando…">
          Volver a enviar al agente
        </Boton>
      ) : null}
      {aviso ? (
        <p className={`mt-2 text-sm font-medium ${aviso.tono === 'exito' ? 'text-emerald-800' : 'text-red-800'}`}>{aviso.texto}</p>
      ) : null}
      <p className="mt-2 text-xs text-muted">Esta pantalla se sigue actualizando sola.</p>
    </Alerta>
  );
}

/**
 * Cuando los dos docentes no coinciden en una materia, la decide el administrador (coordinador o director de
 * carrera) con un comentario obligatorio. Los demás ven que falta esa decisión.
 */
function DecisionDelAdministrador({
  solicitud,
  alGuardar,
}: {
  solicitud: SolicitudAnalisis;
  alGuardar: (s: SolicitudAnalisis) => void;
}): JSX.Element {
  const { usuario } = useAuth();
  const esAdministrador = usuario?.rol === ROLES.ADMINISTRADOR;
  const sinAcuerdo = solicitud.resultadoFinal.filter((m) => m.resultado === 'SIN_ACUERDO');

  if (!esAdministrador) {
    return (
      <Alerta tono="advertencia" titulo="Los docentes no coinciden">
        En {sinAcuerdo.map((m) => `«${m.nombreDestino}»`).join(', ')} un docente aprobó y el otro no. La decisión final la toma el
        administrador (coordinador o director de carrera).
      </Alerta>
    );
  }

  return (
    <div className="space-y-3 rounded-lg border-2 border-amber-300 bg-amber-50 p-4">
      <p className="text-sm font-semibold text-ink">Los docentes no coinciden: su decisión final</p>
      <p className="text-sm text-slate-700">
        Un docente aprobó y el otro no. Como coordinador o director de carrera, decida cada materia y escriba el motivo.
      </p>
      {sinAcuerdo.map((m) => (
        <DecisionFinalMateria key={m.codigoDestino} expedienteId={solicitud.expedienteId} materia={m} alGuardar={alGuardar} />
      ))}
    </div>
  );
}

function DecisionFinalMateria({
  expedienteId,
  materia,
  alGuardar,
}: {
  expedienteId: string;
  materia: ResultadoFinalMateria;
  alGuardar: (s: SolicitudAnalisis) => void;
}): JSX.Element {
  const [aprobada, setAprobada] = useState<boolean | undefined>(undefined);
  const [comentario, setComentario] = useState('');
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const botonClase = (activo: boolean, color: string): string =>
    `rounded-lg border-2 px-4 py-2 text-sm font-semibold transition ${activo ? `${color} text-white` : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-50'}`;

  const guardar = async (): Promise<void> => {
    if (aprobada === undefined) return;
    setGuardando(true);
    setError(null);
    try {
      alGuardar(
        await extraccionesApi.decidirComoAdministrador(expedienteId, {
          codigoDestino: materia.codigoDestino,
          aprobada,
          comentario: comentario.trim(),
        }),
      );
    } catch (fallo) {
      setError(mensajeDeError(fallo));
    } finally {
      setGuardando(false);
    }
  };

  return (
    <div className="space-y-2 rounded-md bg-white p-3">
      <div className="flex flex-wrap items-center gap-3">
        <span className="min-w-64 flex-1 font-medium text-ink">{materia.nombreDestino}</span>
        <span className="text-sm text-muted">
          {materia.aprobaciones} a favor · {materia.rechazos} en contra
        </span>
        <div className="flex gap-2" role="group" aria-label={`Decisión final para ${materia.nombreDestino}`}>
          <button type="button" aria-pressed={aprobada === true} className={botonClase(aprobada === true, 'border-emerald-600 bg-emerald-600')} onClick={() => setAprobada(true)}>
            Sí, homologar
          </button>
          <button type="button" aria-pressed={aprobada === false} className={botonClase(aprobada === false, 'border-red-600 bg-red-600')} onClick={() => setAprobada(false)}>
            No homologar
          </button>
        </div>
      </div>
      <AreaTexto
        aria-label={`Motivo de la decisión final para ${materia.nombreDestino}`}
        placeholder="Motivo de la decisión (obligatorio)"
        value={comentario}
        onChange={(e) => setComentario(e.target.value)}
        rows={2}
        className="font-sans"
      />
      {error ? <Alerta tono="error">{error}</Alerta> : null}
      <Boton onClick={() => void guardar()} disabled={aprobada === undefined || !comentario.trim()} cargando={guardando} textoCargando="Guardando…">
        Guardar decisión final
      </Boton>
    </div>
  );
}

/** Decisión del docente revisor: un botón por materia, en lenguaje simple. */
function SuDecision({ solicitud, alGuardar }: { solicitud: SolicitudAnalisis; alGuardar: (s: SolicitudAnalisis) => void }): JSX.Element {
  const { usuario } = useAuth();
  const previa = solicitud.dictamenes.find((d) => d.correoDocente === usuario?.correoInstitucional?.toLowerCase());
  // El agente solo recomienda: el docente decide todas las materias, también las que no llegan al 80 %.
  const aDecidir = solicitud.resultadoFinal.filter((m) => !m.bloqueadaPorCreditos);
  const [decisiones, setDecisiones] = useState<Record<string, boolean | undefined>>(() =>
    Object.fromEntries(
      aDecidir.map((m) => [
        m.codigoDestino,
        previa?.decisiones.find((d) => d.codigoDestino === m.codigoDestino)?.aprobada ?? (m.estimacion === 'CUMPLE' ? true : undefined),
      ]),
    ),
  );
  const [comentario, setComentario] = useState(previa?.comentario ?? '');
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState<{ texto: string; detalles: string[] } | null>(null);
  const faltan = aDecidir.filter((m) => decisiones[m.codigoDestino] === undefined).length;

  const enviar = async (): Promise<void> => {
    setEnviando(true);
    setError(null);
    try {
      alGuardar(
        await extraccionesApi.responder(solicitud.expedienteId, {
          decisiones: aDecidir.map((m) => ({
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

  const botonClase = (activo: boolean, color: string): string =>
    `rounded-lg border-2 px-4 py-2 text-sm font-semibold transition ${activo ? `${color} text-white` : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-50'}`;

  return (
    <div className="space-y-3 rounded-lg bg-slate-50 p-4">
      <p className="text-sm font-semibold text-ink">
        {previa ? 'Usted ya respondió. Puede cambiar su decisión:' : 'Indique su decisión para cada materia:'}
      </p>
      {aDecidir.map((m) => (
        <div key={m.codigoDestino} className="flex flex-wrap items-center gap-3 rounded-md bg-white px-3 py-2">
          <span className="min-w-64 flex-1 font-medium text-ink">{m.nombreDestino}</span>
          <Veredicto
            estimacion={m.estimacion}
            sinSilabo={solicitud.resultado?.evaluaciones.find((e) => e.materiaDestino === m.codigoDestino)?.sinSilabo}
            bloqueadaPorCreditos={m.bloqueadaPorCreditos}
          />
          <div className="flex gap-2" role="group" aria-label={`Decisión para ${m.nombreDestino}`}>
            <button
              type="button"
              aria-pressed={decisiones[m.codigoDestino] === true}
              className={botonClase(decisiones[m.codigoDestino] === true, 'border-emerald-600 bg-emerald-600')}
              onClick={() => setDecisiones({ ...decisiones, [m.codigoDestino]: true })}
            >
              Sí, homologar
            </button>
            <button
              type="button"
              aria-pressed={decisiones[m.codigoDestino] === false}
              className={botonClase(decisiones[m.codigoDestino] === false, 'border-red-600 bg-red-600')}
              onClick={() => setDecisiones({ ...decisiones, [m.codigoDestino]: false })}
            >
              No homologar
            </button>
          </div>
        </div>
      ))}
      <AreaTexto
        aria-label="Comentario"
        placeholder="Comentario (opcional): por qué está o no de acuerdo con el agente"
        value={comentario}
        onChange={(e) => setComentario(e.target.value)}
        rows={3}
        className="font-sans"
      />
      {error ? (
        <Alerta tono="error" detalles={error.detalles}>
          {error.texto}
        </Alerta>
      ) : null}
      <div className="flex items-center gap-3">
        <Boton onClick={() => void enviar()} cargando={enviando} textoCargando="Enviando…" disabled={faltan > 0}>
          {previa ? 'Guardar cambios' : 'Enviar mi decisión'}
        </Boton>
        {faltan > 0 ? <span className="text-sm text-muted">Falta decidir {faltan} materia(s).</span> : null}
      </div>
    </div>
  );
}

/**
 * Una solicitud, en una sola pantalla y en pasos: qué pide, su historial, las reglas, el análisis del
 * agente (explicado) y la decisión de los dos docentes revisores.
 */
export function SolicitudDetallePage(): JSX.Element {
  useTituloPagina('Solicitud');
  const { id = '' } = useParams();
  const [solicitud, setSolicitud] = useState<SolicitudAnalisis | null>(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reanalizando, setReanalizando] = useState(false);
  /** Consultas seguidas en las que la solicitud todavía no llega al agente, y si el agente ni siquiera responde. */
  const [espera, setEspera] = useState<{ intentos: number; agenteApagado: boolean }>({ intentos: 0, agenteApagado: false });

  useEffect(() => {
    let activo = true;
    let temporizador: ReturnType<typeof setTimeout> | undefined;
    const cargar = async (): Promise<void> => {
      let agenteApagado = false;
      const analisis = await extraccionesApi.obtenerSolicitud(id).catch((fallo: unknown) => {
        // 404: el agente responde pero la solicitud aún no le llega. Otro error (502, 503, sin red): el agente no responde.
        agenteApagado = !(fallo instanceof ErrorApi && fallo.estado === 404);
        return null;
      });
      if (!activo) return;
      setSolicitud(analisis);
      setEspera((e) => (analisis ? { intentos: 0, agenteApagado: false } : { intentos: e.intentos + 1, agenteApagado }));
      setCargando(false);
      // Mientras el agente trabaja, se vuelve a consultar cada 3 segundos; si se está preparando
      // (reintenta solo cada cierto tiempo), cada 15 segundos.
      if (!analisis || analisis.estado === 'PENDIENTE') temporizador = setTimeout(() => void cargar(), 3000);
      else if (analisis.estado === 'PREPARANDO') temporizador = setTimeout(() => void cargar(), 15000);
    };
    void cargar();
    return () => {
      activo = false;
      if (temporizador) clearTimeout(temporizador);
    };
  }, [id]);

  if (cargando) return <CargandoBloque />;
  if (error) return <Alerta tono="error">{error}</Alerta>;
  if (!solicitud) {
    // Unos 20 segundos sin respuesta (o el agente no responde): se explica qué pasa y cómo seguir.
    if (espera.agenteApagado || espera.intentos >= 7) return <NoLlegaAlAgente id={id} agenteApagado={espera.agenteApagado} />;
    return <Alerta tono="informacion">Estamos recibiendo el certificado… esta pantalla se actualiza sola.</Alerta>;
  }

  const estado = estadoSolicitud(solicitud);
  const resultado = solicitud.resultado;
  const finalDe = (materia: string) => solicitud.resultadoFinal.find((m) => m.codigoDestino === materia);

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
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Link to={RUTAS.solicitudes} className="text-sm font-semibold text-institucional-700 hover:underline">
            ← Volver a solicitudes
          </Link>
          <h1 className="mt-1 text-2xl font-bold text-ink">{solicitud.estudianteNombre}</h1>
          <p className="text-base text-slate-700">
            Quiere pasar de <strong>{solicitud.carreraOrigen}</strong> a <strong>{solicitud.carreraDestino}</strong>
          </p>
          <p className="text-sm text-muted">
            Certificado {solicitud.numeroCertificado} · {solicitud.institucionOrigen} · Revisan:{' '}
            {solicitud.revisores.map((r) => r.nombre).join(' y ')}
          </p>
        </div>
        <div className="flex flex-col items-end gap-2">
          <Etiqueta tono={estado.tono}>{estado.texto}</Etiqueta>
          {solicitud.puedeResponder ? (
            <a href="#decision" className="rounded-md bg-institucional-700 px-4 py-2 text-sm font-semibold text-white hover:bg-institucional-800">
              Ir a mi decisión ↓
            </a>
          ) : null}
        </div>
      </div>

      {solicitud.estado === 'PENDIENTE' ? (
        <Alerta tono="informacion">El agente está comparando las materias con los sílabos… esta pantalla se actualiza sola.</Alerta>
      ) : null}
      {solicitud.estado === 'PREPARANDO' ? (
        <Alerta tono="informacion" titulo="El agente se está preparando">
          {solicitud.mensaje ??
            'La solicitud se analizará sola en cuanto el agente esté listo; no hace falta hacer nada.'}{' '}
          Esta pantalla se actualiza sola.
        </Alerta>
      ) : null}
      {solicitud.analisisDesactualizado && solicitud.estado === 'ANALIZADO' ? (
        <Alerta tono="advertencia" titulo="Este análisis se hizo con reglas anteriores">
          <p>
            Las reglas de homologación cambiaron después de analizar esta solicitud (ahora se exige el 80 % en contenidos
            y en créditos). Vuelva a analizarla para ver el resultado con las reglas vigentes.
            {solicitud.dictamenes.length > 0 ? ' Las decisiones que ya dieron los docentes se conservan.' : ''}
          </p>
          <Boton className="mt-2" onClick={() => void reanalizar()} cargando={reanalizando} textoCargando="Analizando…">
            Volver a analizar con las reglas vigentes
          </Boton>
        </Alerta>
      ) : null}
      {solicitud.estado === 'ERROR' ? (
        <Alerta tono="error" titulo="El análisis no se pudo completar">
          <p>{solicitud.mensaje}</p>
          <Boton className="mt-2" onClick={() => void reanalizar()} cargando={reanalizando} textoCargando="Analizando…">
            Volver a analizar
          </Boton>
        </Alerta>
      ) : null}

      <Paso numero={1} titulo="Lo que pide el estudiante" ayuda={resultado?.resumen}>
        <ul className="space-y-2">
          {(resultado?.evaluaciones ?? []).map((e) => {
            const final = finalDe(e.materiaDestino);
            return (
              <li key={e.numero} className="flex flex-wrap items-center gap-3 rounded-md border border-slate-200 px-3 py-2">
                <span className="text-sm text-slate-700">{e.materiasOrigen.map((o) => o.nombre).join(' + ')}</span>
                <span aria-hidden="true">→</span>
                <span className="font-semibold text-ink">{e.materiaDestino}</span>
                <span className="ml-auto flex gap-2">
                  <Veredicto estimacion={e.estimacion} sinSilabo={e.sinSilabo} bloqueadaPorCreditos={e.bloqueadaPorCreditos} />
                  {final && final.resultado !== 'PENDIENTE' ? (
                    <Etiqueta tono={RESULTADO[final.resultado]?.tono ?? 'neutro'}>{RESULTADO[final.resultado]?.texto}</Etiqueta>
                  ) : null}
                </span>
              </li>
            );
          })}
        </ul>
      </Paso>

      <Paso numero={2} titulo="Historial académico" ayuda={`Materias que cursó en ${solicitud.carreraOrigen}, aprobadas y reprobadas.`}>
        <Tabla
          columnas={[
            { clave: 'materia', encabezado: 'Materia', render: (m) => m.nombre },
            { clave: 'nivel', encabezado: 'Nivel', className: 'num', render: (m) => m.nivel },
            { clave: 'periodo', encabezado: 'Período académico', render: (m) => m.periodoAcademico },
            { clave: 'nota', encabezado: 'Nota', className: 'num', render: (m) => nota(m.nota) },
            {
              clave: 'estado',
              encabezado: 'Estado',
              render: (m) => <Etiqueta tono={m.aprobada ? 'exito' : 'error'}>{m.aprobada ? 'Aprobada' : 'Reprobada'}</Etiqueta>,
            },
          ]}
          filas={solicitud.historial}
          claveFila={(m) => m.nombre}
        />
      </Paso>

      {resultado ? (
        <Paso numero={3} titulo="Reglas de homologación" ayuda="Lo que pide el instructivo de la UTA y si esta solicitud lo cumple.">
          <ul className="space-y-2">
            {resultado.requisitos.map((r) => (
              <li key={r.regla} className="flex gap-3">
                <span className={`text-lg font-bold ${r.cumple ? 'text-emerald-600' : 'text-amber-600'}`} aria-hidden="true">
                  {r.cumple ? '✔' : '!'}
                </span>
                <div>
                  <p className="font-medium text-ink">{r.regla}</p>
                  <p className="text-sm text-muted">{r.explicacion}</p>
                </div>
              </li>
            ))}
          </ul>
        </Paso>
      ) : null}

      {resultado ? (
        <Paso
          numero={4}
          titulo="Análisis del agente, materia por materia"
          ayuda={`El agente compara los sílabos oficiales y recomienda: homologar si llega al ${MINIMO} % en contenidos y al ${MINIMO} % en créditos, y no homologar si no llega, explicando por qué. Es solo una recomendación: los dos docentes revisores deciden cada materia.`}
        >
          <div className="space-y-4">
            {resultado.evaluaciones.map((e) => (
              <AnalisisMateria key={e.numero} e={e} carreraOrigen={solicitud.carreraOrigen} />
            ))}
          </div>
        </Paso>
      ) : null}

      {solicitud.estado === 'ANALIZADO' ? (
        <Paso id="decision" numero={5} titulo="Decisión de los docentes" ayuda="Una materia queda homologada cuando los dos docentes revisores la aprueban.">
          <div className="space-y-4">
            <ul className="space-y-1 text-sm">
              {solicitud.revisores.map((r) => {
                const dictamen = solicitud.dictamenes.find((d) => d.correoDocente === r.correo);
                return (
                  <li key={r.correo}>
                    <strong>{r.nombre}</strong>: {r.respondio ? 'ya respondió' : 'todavía no responde'}
                    {dictamen?.comentario ? ` — “${dictamen.comentario}”` : ''}
                  </li>
                );
              })}
            </ul>
            {solicitud.estadoRevision === 'RESUELTA' ? (
              <ul className="space-y-2">
                {solicitud.resultadoFinal.map((m) => (
                  <li key={m.codigoDestino} className="space-y-1 text-sm">
                    <div className="flex flex-wrap items-center gap-3">
                      <span className="min-w-64 font-medium">{m.nombreDestino}</span>
                      <Etiqueta tono={RESULTADO[m.resultado]?.tono ?? 'neutro'}>{RESULTADO[m.resultado]?.texto}</Etiqueta>
                    </div>
                    {m.decisionFinal ? (
                      <p className="text-slate-700">
                        Los docentes no coincidieron. Decisión final de <strong>{m.decisionFinal.nombreAdministrador}</strong>:{' '}
                        {m.decisionFinal.aprobada ? 'homologar' : 'no homologar'} — “{m.decisionFinal.comentario}”
                      </p>
                    ) : null}
                  </li>
                ))}
              </ul>
            ) : null}
            {solicitud.resultadoFinal.some((m) => m.resultado === 'SIN_ACUERDO') ? (
              <DecisionDelAdministrador solicitud={solicitud} alGuardar={setSolicitud} />
            ) : null}
            {solicitud.puedeResponder ? <SuDecision solicitud={solicitud} alGuardar={setSolicitud} /> : null}
          </div>
        </Paso>
      ) : null}
    </div>
  );
}
