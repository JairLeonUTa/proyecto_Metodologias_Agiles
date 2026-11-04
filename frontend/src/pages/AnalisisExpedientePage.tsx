import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { asignaturasApi, carrerasApi, detallesDeError, expedientesApi, extraccionesApi, mallasApi, mensajeDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, CargandoBloque, Etiqueta, Tabla, Tarjeta } from '../components/ui';
import type { ColumnaTabla } from '../components/ui';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { AnalisisAutomatico, CoberturaUnidadPropuesta, Extraccion, PropuestaHomologacion } from '../types/analisis';
import { ESTADOS_EXTRACCION } from '../types/analisis';
import type { Asignatura } from '../types/catalogo';
import type { Expediente } from '../types/expedientes';
import { anosDesdeFecha, formatearPorcentaje, presentarCategoriaCreditos, presentarEstadoFinal } from '../utils/analisis';

interface Caso {
  extraccion: Extraccion;
  expediente: Expediente | null;
  materias: Extraccion[];
}

/** Carga la materia elegida en la bandeja, su expediente (MS B) y todas las materias del expediente. */
async function cargarCaso(id: string): Promise<Caso> {
  const extraccion = await extraccionesApi.obtener(id);
  const [expediente, materias] = await Promise.all([
    expedientesApi.obtener(extraccion.expedienteId).catch(() => null),
    extraccionesApi.bandeja(undefined, extraccion.expedienteId).catch(() => [extraccion]),
  ]);
  return { extraccion, expediente, materias: materias.filter((m) => m.expedienteId === extraccion.expedienteId) };
}

/** Todas las asignaturas de la malla vigente de la carrera de destino, con sus unidades. */
async function catalogoDestino(codigoCarrera: string): Promise<Asignatura[]> {
  const carreras = await carrerasApi.listar();
  const carrera = carreras.find((c) => c.codigo.toUpperCase() === codigoCarrera.toUpperCase());
  if (!carrera) throw new Error(`La carrera de destino ${codigoCarrera} no existe en el catálogo.`);
  const mallas = await mallasApi.listar(carrera.id);
  const malla = mallas.find((m) => m.vigente) ?? mallas[0];
  if (!malla) throw new Error(`La carrera ${codigoCarrera} no tiene una malla vigente en el catálogo.`);
  const asignaturas = await asignaturasApi.listar(malla.id);
  return asignaturas.filter((a) => a.unidades.length > 0);
}

const COLUMNAS_UNIDADES: Array<ColumnaTabla<CoberturaUnidadPropuesta>> = [
  {
    clave: 'unidad',
    encabezado: 'Unidad de destino',
    render: (u) => (
      <span className="text-sm">
        U{u.numero}. {u.nombre}
      </span>
    ),
  },
  { clave: 'horas', encabezado: 'Horas', className: 'num', render: (u) => u.horas },
  {
    clave: 'origen',
    encabezado: 'Contenido más parecido en lo aprobado',
    render: (u) =>
      u.codigoOrigen ? (
        <span className="text-sm">
          <span className="font-mono text-xs text-faint">{u.codigoOrigen}</span> · U{u.numeroUnidadOrigen}. {u.nombreUnidadOrigen}
        </span>
      ) : (
        '—'
      ),
  },
  { clave: 'similitud', encabezado: 'Parecido', className: 'num', render: (u) => formatearPorcentaje(u.similitud) },
  {
    clave: 'cubierta',
    encabezado: 'Cubierta',
    render: (u) => <Etiqueta tono={u.cubierta ? 'exito' : 'error'}>{u.cubierta ? 'Sí' : 'No'}</Etiqueta>,
  },
];

/** Tarjeta de una homologación propuesta o de un destino que no alcanza el contenido. */
function TarjetaPropuesta({ propuesta, institucion }: { propuesta: PropuestaHomologacion; institucion: string }): JSX.Element {
  const estado = presentarEstadoFinal(propuesta.estadoFinal);
  const categoria = presentarCategoriaCreditos(propuesta.categoriaCreditos);
  const horasCubiertas = propuesta.unidades.filter((u) => u.cubierta).reduce((suma, u) => suma + u.horas, 0);
  const horasTotales = propuesta.unidades.reduce((suma, u) => suma + u.horas, 0);

  return (
    <Tarjeta
      titulo={`${propuesta.codigoDestino} · ${propuesta.nombreDestino}`}
      descripcion={`${propuesta.creditosDestino} créditos${propuesta.semestreDestino ? ` · semestre ${propuesta.semestreDestino}` : ''} · con ${propuesta.origenes
        .map((o) => `${o.codigo} ${o.nombre}`)
        .join(' + ')}${propuesta.origenes.length > 1 ? ' (varias materias para una)' : ''}`}
    >
      <div className="space-y-4">
        <div className="flex flex-wrap items-center gap-2">
          <Etiqueta tono={estado.tono === 'informacion' ? 'neutro' : estado.tono}>{estado.etiqueta}</Etiqueta>
          <Etiqueta tono={categoria.tono}>Créditos: {categoria.etiqueta}</Etiqueta>
        </div>

        <dl className="grid gap-4 sm:grid-cols-3">
          <div>
            <dt className="text-xs uppercase tracking-wide text-faint">Contenido cubierto</dt>
            <dd className="mt-0.5 text-2xl font-semibold text-ink">{formatearPorcentaje(propuesta.pcont)}</dd>
            <dd className="text-xs text-faint">
              {horasCubiertas} de {horasTotales} horas de la materia de destino
            </dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-faint">Proporción de créditos</dt>
            <dd className="mt-0.5 text-2xl font-semibold text-ink">{formatearPorcentaje(propuesta.pcred)}</dd>
            <dd className="text-xs text-faint">
              {propuesta.origenes.reduce((s, o) => s + (o.creditos ?? 0), 0)} créditos aprobados frente a {propuesta.creditosDestino}
            </dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-faint">Materias aprobadas usadas</dt>
            {propuesta.origenes.map((o) => (
              <dd key={o.extraccionId} className="mt-0.5 text-sm text-ink">
                {o.codigo} {o.nombre}
                <span className="block text-xs text-faint">
                  Aprobada {o.fechaAprobacion ?? '—'}
                  {o.anosTranscurridos !== null ? ` (hace ${o.anosTranscurridos} años)` : ''} · matrícula {o.numeroMatricula ?? '—'}
                </span>
              </dd>
            ))}
          </div>
        </dl>

        {propuesta.alertas.length > 0 ? (
          <ul aria-label="Observaciones" className="space-y-1">
            {propuesta.alertas.map((alerta) => (
              <li key={alerta} className="text-sm text-muted">
                ⚠ {alerta}
              </li>
            ))}
          </ul>
        ) : null}

        <Tabla columnas={COLUMNAS_UNIDADES} filas={propuesta.unidades} claveFila={(u) => u.numero} />

        {propuesta.alcanzaContenido ? (
          propuesta.precedentes.length > 0 ? (
            <Alerta tono="informacion" titulo="Precedentes idénticos">
              {propuesta.precedentes.map((p) => `${p.resolucion} (${p.fecha})`).join(' · ')}
            </Alerta>
          ) : (
            <p className="text-xs text-faint">Sin precedentes idénticos registrados para {institucion}.</p>
          )
        ) : null}
      </div>
    </Tarjeta>
  );
}

/**
 * Análisis automático del expediente (homologación por contenidos).
 * Toma todas las materias aprobadas del certificado SIHA y las compara con todas las materias de la
 * carrera de destino. Propone solo las homologaciones que alcanzan el contenido mínimo vigente; el
 * docente no escribe códigos, años ni matrículas. Los umbrales no se muestran: deciden en el servidor.
 */
export function AnalisisExpedientePage(): JSX.Element {
  useTituloPagina('Análisis del expediente');
  const { id = '' } = useParams();
  const caso = usePeticion(() => cargarCaso(id), [id]);
  const [analizando, setAnalizando] = useState(false);
  const [error, setError] = useState<{ texto: string; detalles: string[] } | null>(null);
  const [resultado, setResultado] = useState<AnalisisAutomatico | null>(null);

  if (caso.cargando) return <CargandoBloque />;

  if (caso.error || !caso.datos) {
    return (
      <div className="space-y-4">
        <EncabezadoPagina titulo="Análisis del expediente" />
        <Alerta tono="error" titulo="No se pudo cargar el expediente">
          {caso.error ?? 'La extracción no existe.'}
        </Alerta>
      </div>
    );
  }

  const { expediente, materias } = caso.datos;
  const sinConfirmar = materias.filter((m) => m.estado !== ESTADOS_EXTRACCION.CONFIRMADA);
  const datosCertificado = new Map((expediente?.asignaturas ?? []).map((a) => [a.codigoOrigen.toUpperCase(), a]));

  const analizar = async (): Promise<void> => {
    if (!expediente) return;
    setError(null);
    setAnalizando(true);
    try {
      const destino = await catalogoDestino(expediente.carreraDestinoCodigo);
      if (destino.length === 0) {
        throw new Error(`La carrera ${expediente.carreraDestinoCodigo} no tiene asignaturas con contenidos en el catálogo.`);
      }

      setResultado(
        await extraccionesApi.analizarExpediente(expediente.id, {
          rutaNormativa: expediente.rutaNormativa,
          tipoTramite: expediente.tipoTramite,
          institucionOrigen: expediente.institucionOrigen ?? '',
          asignaturasOrigen: expediente.asignaturas.map((a) => ({
            codigo: a.codigoOrigen,
            fechaAprobacion: a.fechaAprobacionCertificada ?? null,
            numeroMatricula: a.numeroMatriculaDestino ?? null,
          })),
          asignaturasDestino: destino.map((a) => ({
            codigo: a.codigo,
            nombre: a.nombre,
            creditos: a.creditos,
            semestre: a.semestre,
            unidades: a.unidades.map((u) => ({ numero: u.numero, nombre: u.nombre, horas: u.horas, contenidos: u.contenidos ?? '' })),
          })),
        }),
      );
    } catch (fallo) {
      setResultado(null);
      setError({ texto: mensajeDeError(fallo), detalles: detallesDeError(fallo) });
    } finally {
      setAnalizando(false);
    }
  };

  const columnasMaterias: Array<ColumnaTabla<Extraccion>> = [
    {
      clave: 'materia',
      encabezado: 'Materia aprobada',
      render: (m) => (
        <div>
          <p className="text-sm text-ink">{m.nombreAsignaturaOrigen}</p>
          <p className="font-mono text-xs text-faint">{m.codigoAsignaturaOrigen}</p>
        </div>
      ),
    },
    {
      clave: 'semestre',
      encabezado: 'Semestre',
      className: 'num',
      render: (m) => datosCertificado.get(m.codigoAsignaturaOrigen.toUpperCase())?.semestre ?? '—',
    },
    { clave: 'creditos', encabezado: 'Créditos', className: 'num', render: (m) => m.creditos ?? '—' },
    { clave: 'horas', encabezado: 'Horas', className: 'num', render: (m) => m.horas ?? '—' },
    {
      clave: 'fecha',
      encabezado: 'Aprobada el',
      render: (m) => {
        const fecha = datosCertificado.get(m.codigoAsignaturaOrigen.toUpperCase())?.fechaAprobacionCertificada ?? null;
        const anos = anosDesdeFecha(fecha);
        return fecha ? `${fecha}${anos !== null ? ` (hace ${anos} años)` : ''}` : '—';
      },
    },
    {
      clave: 'matricula',
      encabezado: 'Matrícula',
      className: 'num',
      render: (m) => datosCertificado.get(m.codigoAsignaturaOrigen.toUpperCase())?.numeroMatriculaDestino ?? '—',
    },
    { clave: 'unidades', encabezado: 'Unidades', className: 'num', render: (m) => m.unidades.length },
    {
      clave: 'estado',
      encabezado: 'Datos',
      render: (m) =>
        m.estado === ESTADOS_EXTRACCION.CONFIRMADA ? (
          <Etiqueta tono="exito">{m.confirmadoPor === 'CERTIFICADO_SIHA' ? 'Del certificado' : 'Confirmados'}</Etiqueta>
        ) : (
          <Etiqueta tono="advertencia">{m.estado}</Etiqueta>
        ),
    },
  ];

  const institucion = expediente?.institucionOrigen ?? '';

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        titulo="Análisis del expediente"
        descripcion={[
          expediente?.codigo,
          expediente?.estudianteNombre,
          expediente ? `${expediente.carreraOrigenCodigo} → ${expediente.carreraDestinoCodigo}` : null,
          expediente?.rutaNormativa,
          institucion,
        ]
          .filter(Boolean)
          .join(' · ')}
        acciones={
          <Link to="/evaluacion" className="text-sm font-semibold text-institucional-700 hover:underline">
            Volver a la bandeja
          </Link>
        }
      />

      {!expediente ? (
        <Alerta tono="advertencia" titulo="Expediente no disponible">
          No se pudo consultar el expediente en el Microservicio B; sin sus datos no se puede analizar.
        </Alerta>
      ) : null}

      <Tarjeta
        titulo="Materias aprobadas del certificado"
        descripcion="Tomadas del certificado SIHA preaprobado. El análisis las compara con todas las materias de la carrera de destino."
      >
        <div className="space-y-4">
          <Tabla columnas={columnasMaterias} filas={materias} claveFila={(m) => m.id} />
          {sinConfirmar.length > 0 ? (
            <Alerta tono="advertencia" titulo="Hay materias sin confirmar">
              {sinConfirmar.map((m) => m.codigoAsignaturaOrigen).join(', ')} deben confirmarse antes del análisis.
            </Alerta>
          ) : null}
          <Boton
            onClick={() => void analizar()}
            cargando={analizando}
            textoCargando="El agente está comparando contenidos…"
            disabled={!expediente || sinConfirmar.length > 0 || materias.length === 0}
          >
            Analizar automáticamente
          </Boton>
        </div>
      </Tarjeta>

      {error ? (
        <Alerta tono="error" titulo="El análisis no pudo completarse" detalles={error.detalles}>
          {error.texto}
        </Alerta>
      ) : null}

      {resultado ? (
        <section aria-label="Resultado del análisis" className="space-y-6">
          {resultado.alertas.length > 0 ? (
            <Alerta tono="advertencia" titulo="Observaciones" detalles={resultado.alertas}>
              Revise estas observaciones antes del dictamen.
            </Alerta>
          ) : null}

          <div className="space-y-4">
            <h2 className="text-lg font-semibold text-ink">
              Homologaciones propuestas ({resultado.propuestas.length})
            </h2>
            {resultado.propuestas.length === 0 ? (
              <Alerta tono="informacion">Ninguna materia aprobada alcanza el contenido mínimo de una materia de destino.</Alerta>
            ) : (
              resultado.propuestas.map((p) => <TarjetaPropuesta key={p.codigoDestino} propuesta={p} institucion={institucion} />)
            )}
          </div>

          {resultado.sinCoberturaSuficiente.length > 0 ? (
            <div className="space-y-4">
              <h2 className="text-lg font-semibold text-ink">No se pueden homologar: contenido insuficiente</h2>
              {resultado.sinCoberturaSuficiente.map((p) => (
                <TarjetaPropuesta key={p.codigoDestino} propuesta={p} institucion={institucion} />
              ))}
            </div>
          ) : null}

          {resultado.origenesSinAsignar.length > 0 ? (
            <Tarjeta titulo="Materias aprobadas sin equivalencia" descripcion="No alcanzan ninguna materia de la carrera de destino.">
              <ul className="space-y-1 text-sm">
                {resultado.origenesSinAsignar.map((o) => (
                  <li key={o.extraccionId}>
                    <span className="font-mono text-xs text-faint">{o.codigo}</span> {o.nombre}
                  </li>
                ))}
              </ul>
            </Tarjeta>
          ) : null}
        </section>
      ) : null}
    </div>
  );
}
