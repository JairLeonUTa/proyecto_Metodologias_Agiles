import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import { detallesDeError, extraccionesApi, mensajeDeError } from '../api';
import { Alerta, AreaTexto, Boton, CargandoBloque, Etiqueta, Tabla } from '../components/ui';
import type { ColumnaTabla, TonoEtiqueta } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { RUTAS } from '../layouts/navegacion';
import type { CoberturaUnidad, EvaluacionMateria, SolicitudAnalisis } from '../types/analisis';
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

function Veredicto({ cumple }: { cumple: boolean }): JSX.Element {
  return cumple ? (
    <Etiqueta tono="exito">✔ Cumple el {MINIMO} %</Etiqueta>
  ) : (
    <Etiqueta tono="advertencia">? A criterio del docente</Etiqueta>
  );
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
  return (
    <article className="space-y-4 rounded-lg border border-slate-200 p-4">
      <header className="flex flex-wrap items-center gap-3">
        <h3 className="text-base font-semibold text-ink">{e.materiaDestino}</h3>
        <span className="text-sm text-muted">
          ← {e.materiasOrigen.map((o) => o.nombre).join(' + ')} ({carreraOrigen})
        </span>
        <span className="ml-auto">
          <Veredicto cumple={cumple} />
        </span>
      </header>

      {e.unidades.length > 0 ? (
        <div className="grid gap-4 md:grid-cols-3">
          <Medidor etiqueta="Contenidos" valor={e.contenidos} ayuda="Temas de la materia de destino que ya estudió." />
          <Medidor etiqueta="Profundidad" valor={e.profundidad} ayuda="Horas dedicadas a esos temas." />
          <Medidor etiqueta="Carga horaria" valor={e.cargaHoraria} ayuda="Horas totales aprobadas." />
        </div>
      ) : null}

      <p className={`rounded-md px-3 py-2 text-sm font-medium ${cumple ? 'bg-emerald-50 text-emerald-900' : 'bg-amber-50 text-amber-900'}`}>
        {e.conclusion}
      </p>

      <div>
        <p className="text-sm font-semibold text-ink">¿Por qué?</p>
        <ul className="mt-1 space-y-1 text-sm text-slate-700">
          {e.explicacion.map((linea) => (
            <li key={linea}>{linea}</li>
          ))}
        </ul>
      </div>

      {e.unidades.length > 0 ? (
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

/** Decisión del docente revisor: un botón por materia, en lenguaje simple. */
function SuDecision({ solicitud, alGuardar }: { solicitud: SolicitudAnalisis; alGuardar: (s: SolicitudAnalisis) => void }): JSX.Element {
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
  const faltan = solicitud.resultadoFinal.filter((m) => decisiones[m.codigoDestino] === undefined).length;

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

  const botonClase = (activo: boolean, color: string): string =>
    `rounded-lg border-2 px-4 py-2 text-sm font-semibold transition ${activo ? `${color} text-white` : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-50'}`;

  return (
    <div className="space-y-3 rounded-lg bg-slate-50 p-4">
      <p className="text-sm font-semibold text-ink">
        {previa ? 'Usted ya respondió. Puede cambiar su decisión:' : 'Indique su decisión para cada materia:'}
      </p>
      {solicitud.resultadoFinal.map((m) => (
        <div key={m.codigoDestino} className="flex flex-wrap items-center gap-3 rounded-md bg-white px-3 py-2">
          <span className="min-w-64 flex-1 font-medium text-ink">{m.nombreDestino}</span>
          <Veredicto cumple={m.estimacion === 'CUMPLE'} />
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

  useEffect(() => {
    let activo = true;
    let temporizador: ReturnType<typeof setTimeout> | undefined;
    const cargar = async (): Promise<void> => {
      const analisis = await extraccionesApi.obtenerSolicitud(id).catch(() => null);
      if (!activo) return;
      setSolicitud(analisis);
      setCargando(false);
      // Mientras el agente trabaja, se vuelve a consultar cada 3 segundos.
      if (!analisis || analisis.estado === 'PENDIENTE') temporizador = setTimeout(() => void cargar(), 3000);
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
                  <Veredicto cumple={e.estimacion === 'CUMPLE'} />
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
          <Link to={RUTAS.reglasHomologacion} className="mt-3 inline-block text-sm font-semibold text-institucional-700 hover:underline">
            Ver todas las reglas
          </Link>
        </Paso>
      ) : null}

      {resultado ? (
        <Paso
          numero={4}
          titulo="Análisis del agente, materia por materia"
          ayuda={`El agente compara los sílabos oficiales. Una materia cumple si llega al ${MINIMO} % en contenidos, profundidad y carga horaria; si no, deciden los docentes.`}
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
              <ul className="space-y-1">
                {solicitud.resultadoFinal.map((m) => (
                  <li key={m.codigoDestino} className="flex items-center gap-3 text-sm">
                    <span className="min-w-64 font-medium">{m.nombreDestino}</span>
                    <Etiqueta tono={RESULTADO[m.resultado]?.tono ?? 'neutro'}>{RESULTADO[m.resultado]?.texto}</Etiqueta>
                  </li>
                ))}
              </ul>
            ) : null}
            {solicitud.puedeResponder ? <SuDecision solicitud={solicitud} alGuardar={setSolicitud} /> : null}
          </div>
        </Paso>
      ) : null}
    </div>
  );
}
