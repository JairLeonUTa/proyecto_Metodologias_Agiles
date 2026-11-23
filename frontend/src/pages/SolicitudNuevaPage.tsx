import { useEffect, useRef, useState } from 'react';
import type { ChangeEvent, DragEvent } from 'react';
import { Link } from 'react-router-dom';
import { detallesDeError, expedientesApi, mensajeDeError } from '../api';
import { ErrorApi } from '../api/httpClient';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, Etiqueta, Tabla, Tarjeta } from '../components/ui';
import type { ColumnaTabla } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { RUTAS } from '../layouts/navegacion';
import { ETIQUETA_TIPO_TRAMITE } from '../types/comunes';
import type {
  CertificadoLeido,
  MateriaCursada,
  RechazoCertificado,
  SeccionVerificada,
  SilaboAdjunto,
  SolicitudCertificado,
  TipoRechazoCertificado,
} from '../types/expedientes';
import { formatearBytes } from '../utils/formato';

const TAMANO_MAXIMO = 10 * 1024 * 1024;

/** PDF de ejemplo con la plantilla SIHA-CH-01 (carpeta `public/plantillas`). */
const EJEMPLOS = [
  { archivo: 'SIHA-TI-SW-cambio-de-carrera.pdf', texto: 'Tecnologías de la Información → Software' },
  { archivo: 'SIHA-IND-SW-cambio-de-carrera.pdf', texto: 'Ingeniería Industrial → Software' },
  { archivo: 'SIHA-con-errores.pdf', texto: 'Certificado con errores (se rechaza)' },
];

const urlEjemplo = (archivo: string): string => `${import.meta.env.BASE_URL}plantillas/${archivo}`;

const COLUMNAS: Array<ColumnaTabla<MateriaCursada>> = [
  { clave: 'materia', encabezado: 'Materia', render: (m) => <span className="text-sm text-ink">{m.nombre}</span> },
  { clave: 'nivel', encabezado: 'Nivel', className: 'num', render: (m) => m.nivel },
  { clave: 'periodo', encabezado: 'Período académico', render: (m) => m.periodoAcademico },
  { clave: 'nota', encabezado: 'Nota', className: 'num', render: (m) => m.nota.toFixed(2).replace('.', ',') },
  {
    clave: 'estado',
    encabezado: 'Estado',
    render: (m) => <Etiqueta tono={m.aprobada ? 'exito' : 'error'}>{m.aprobada ? 'Aprobada' : 'Reprobada'}</Etiqueta>,
  },
];

/** Datos leídos del certificado: estudiante, carreras, historial y lo que pide homologar. */
function ResumenCertificado({ certificado }: { certificado: CertificadoLeido }): JSX.Element {
  const dato = (etiqueta: string, valor: string): JSX.Element => (
    <div>
      <dt className="text-xs uppercase tracking-wide text-faint">{etiqueta}</dt>
      <dd className="mt-0.5 text-sm text-ink">{valor}</dd>
    </div>
  );

  return (
    <div className="space-y-5">
      <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {dato('Estudiante', `${certificado.estudianteNombre} · ${certificado.estudianteIdentificacion}`)}
        {dato('Cambio', `${certificado.carreraOrigen} → ${certificado.carreraDestino}`)}
        {dato('Trámite', `${ETIQUETA_TIPO_TRAMITE[certificado.tipoTramite] ?? certificado.tipoTramite} · ${certificado.rutaNormativa}`)}
        {dato('Certificado', `${certificado.numeroCertificado} · ${certificado.fechaEmision}`)}
        {dato('Institución de origen', certificado.institucionOrigen)}
        {dato('Docentes revisores', certificado.revisores.map((r) => r.nombre).join(' y '))}
      </dl>
      <div>
        <p className="mb-2 text-sm font-semibold text-ink">Materias que pide homologar</p>
        <ul className="space-y-1">
          {certificado.solicitudes.map((s) => (
            <li key={s.numero} className="rounded-md border border-slate-200 px-3 py-2 text-sm">
              {s.materiasOrigen.join(' + ')} <span aria-hidden="true">→</span> <strong>{s.materiaDestino}</strong>
            </li>
          ))}
        </ul>
      </div>
      <div>
        <p className="mb-2 text-sm font-semibold text-ink">Historial académico</p>
        <Tabla columnas={COLUMNAS} filas={certificado.materias} claveFila={(m) => m.nombre} />
      </div>
    </div>
  );
}

const TITULO_RECHAZO: Record<TipoRechazoCertificado, string> = {
  NO_ES_CERTIFICADO: 'Este PDF no es el certificado SIHA',
  CAMPO_VACIO: 'El certificado tiene campos sin llenar',
  REGLA: 'Datos que no cumplen las reglas',
  CAMPOS_Y_REGLAS: 'Campos sin llenar y datos que no cumplen las reglas',
};

function sinTituloRepetido(mensaje: string, titulo: string): string {
  if (!mensaje.toLocaleLowerCase().startsWith(titulo.toLocaleLowerCase())) return mensaje;
  return mensaje.slice(titulo.length).replace(/^[.\s]+/, '');
}

/** Lee del error del servidor el rechazo con su lista de verificación, si la trae. */
function rechazoDe(fallo: unknown): RechazoCertificado | null {
  if (!(fallo instanceof ErrorApi) || fallo.estado !== 422) return null;
  const datos = fallo.datos as Partial<RechazoCertificado> | null;
  return datos && typeof datos.tipo === 'string' && Array.isArray(datos.secciones) ? (datos as RechazoCertificado) : null;
}

/** Lista de verificación por sección del certificado: ✔ si está bien, ✘ con el motivo si no. */
export function ListaVerificacion({ secciones }: { secciones: SeccionVerificada[] }): JSX.Element {
  return (
    <ul className="divide-y divide-slate-200 rounded-md border border-slate-200" aria-label="Revisión del certificado por sección">
      {secciones.map((s) => (
        <li key={s.seccion} className="flex gap-3 px-3 py-2">
          <span
            aria-hidden="true"
            className={`mt-0.5 font-bold ${s.ok ? 'text-positive' : 'text-negative'}`}
          >
            {s.ok ? '✔' : '✘'}
          </span>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-ink">
              {s.seccion}{' '}
              <span className="sr-only">{s.ok ? 'Sección correcta' : 'Sección con problemas'}</span>
            </p>
            {s.ok ? (
              <p className="text-xs text-faint">Sin problemas.</p>
            ) : (
              <ul className="mt-1 list-disc space-y-0.5 pl-4 text-sm text-slate-700">
                {s.problemas.map((p) => (
                  <li key={p}>{p}</li>
                ))}
              </ul>
            )}
          </div>
        </li>
      ))}
    </ul>
  );
}

/** Materias aprobadas que el certificado pide homologar, sin repetir (para adjuntar su sílabo). */
function materiasParaSilabo(certificado: CertificadoLeido): string[] {
  const vistas = new Set<string>();
  const materias: string[] = [];
  for (const solicitud of certificado.solicitudes) {
    for (const materia of solicitud.materiasOrigen) {
      const clave = materia.trim().toLowerCase();
      if (!vistas.has(clave)) {
        vistas.add(clave);
        materias.push(materia);
      }
    }
  }
  return materias;
}

/** Sílabos de la otra universidad, uno por materia aprobada (solo movilidad externa, opcionales). */
function SilabosExternos({
  certificado,
  silabos,
  deshabilitado,
  onCambiar,
}: {
  certificado: CertificadoLeido;
  silabos: SilaboAdjunto[];
  deshabilitado: boolean;
  onCambiar: (materia: string, archivo: File | null) => void;
}): JSX.Element {
  const leidoDe = (materia: string) =>
    certificado.silabos?.find((s) => s.materia.trim().toLowerCase() === materia.trim().toLowerCase());
  const adjuntoDe = (materia: string) =>
    silabos.find((s) => s.materia.trim().toLowerCase() === materia.trim().toLowerCase());

  return (
    <Tarjeta
      titulo="Sílabos de la otra universidad (opcional)"
      descripcion={`El certificado es de una movilidad externa desde ${certificado.institucionOrigen}. Adjunte el sílabo o plan analítico (PDF con texto) de cada materia aprobada para que el agente compare sus contenidos con los de la UTA. Si una materia va sin sílabo, la deciden los docentes revisores con la documentación del estudiante.`}
    >
      <ul className="space-y-3">
        {materiasParaSilabo(certificado).map((materia) => {
          const adjunto = adjuntoDe(materia);
          const leido = leidoDe(materia);
          return (
            <li key={materia} className="rounded-md border border-slate-200 px-3 py-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <p className="text-sm font-semibold text-ink">{materia}</p>
                  <p className="mt-0.5 text-xs text-faint">
                    {adjunto
                      ? leido
                        ? `Sílabo leído: ${leido.unidades} unidad(es) y ${leido.horasTotales} hora(s)${leido.nombreEnSilabo ? ` de «${leido.nombreEnSilabo}»` : ''}.`
                        : adjunto.archivo.name
                      : 'Sin sílabo: esta materia la deciden los docentes revisores.'}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <label className="cursor-pointer rounded-md border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-50">
                    {adjunto ? 'Cambiar sílabo' : 'Adjuntar sílabo'}
                    <input
                      type="file"
                      accept="application/pdf,.pdf"
                      className="sr-only"
                      data-testid={`silabo-${materia}`}
                      disabled={deshabilitado}
                      onChange={(e: ChangeEvent<HTMLInputElement>) => {
                        const archivo = e.target.files?.[0] ?? null;
                        e.target.value = '';
                        onCambiar(materia, archivo);
                      }}
                    />
                  </label>
                  {adjunto ? (
                    <Boton variante="secundario" disabled={deshabilitado} onClick={() => onCambiar(materia, null)}>
                      Quitar
                    </Boton>
                  ) : null}
                </div>
              </div>
            </li>
          );
        })}
      </ul>
    </Tarjeta>
  );
}

/**
 * Registro de la solicitud con UN SOLO PDF: el certificado de homologación SIHA (SIHA-CH-01).
 * El certificado llega preaprobado por Secretaría (estudiante, carreras, historial con aprobadas y
 * reprobadas, materias que pide homologar y revisores). Contenidos y horas los pone el agente desde los
 * sílabos oficiales. El operador no digita nada: el sistema lo lee, lo valida y lo registra.
 */
export function SolicitudNuevaPage(): JSX.Element {
  useTituloPagina('Registrar solicitud');
  const { usuario } = useAuth();
  const entrada = useRef<HTMLInputElement>(null);
  const [archivo, setArchivo] = useState<File | null>(null);
  const [leyendo, setLeyendo] = useState(false);
  const [certificado, setCertificado] = useState<CertificadoLeido | null>(null);
  const [error, setError] = useState<{ texto: string; detalles: string[]; expedienteId?: string; es409?: boolean } | null>(null);
  const [errorAlEnviar, setErrorAlEnviar] = useState(false);
  const avisoEnvio = useRef<HTMLDivElement>(null);
  const [registrando, setRegistrando] = useState(false);
  const [resultado, setResultado] = useState<SolicitudCertificado | null>(null);
  const [arrastrando, setArrastrando] = useState(false);
  const [silabos, setSilabos] = useState<SilaboAdjunto[]>([]);
  /** Rechazo del certificado con su lista de verificación (mientras exista, no se puede registrar). */
  const [rechazo, setRechazo] = useState<RechazoCertificado | null>(null);

  useEffect(() => {
    if (!error || !errorAlEnviar) return;
    const aviso = avisoEnvio.current;
    aviso?.focus();
    if (typeof aviso?.scrollIntoView === 'function') {
      const reducido = typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      aviso.scrollIntoView({ block: 'center', behavior: reducido ? 'auto' : 'smooth' });
    }
  }, [error, errorAlEnviar]);

  const reiniciar = (): void => {
    setArchivo(null);
    setCertificado(null);
    setError(null);
    setErrorAlEnviar(false);
    setRechazo(null);
    setResultado(null);
    setSilabos([]);
    if (entrada.current) entrada.current.value = '';
  };

  const elegir = async (seleccionado: File | undefined): Promise<void> => {
    setError(null);
    setErrorAlEnviar(false);
    setRechazo(null);
    setCertificado(null);
    setResultado(null);
    if (!seleccionado) return;

    if (!seleccionado.name.toLowerCase().endsWith('.pdf')) {
      setError({ texto: 'Solo se acepta un archivo PDF.', detalles: [] });
      return;
    }
    if (seleccionado.size > TAMANO_MAXIMO) {
      setError({ texto: `El PDF supera el máximo de ${formatearBytes(TAMANO_MAXIMO)}.`, detalles: [] });
      return;
    }

    setArchivo(seleccionado);
    setSilabos([]);
    setLeyendo(true);
    try {
      setCertificado(await expedientesApi.validarCertificado(seleccionado));
    } catch (fallo) {
      const conLista = rechazoDe(fallo);
      if (conLista) {
        setRechazo(conLista);
      } else {
        setError({ texto: mensajeDeError(fallo), detalles: detallesDeError(fallo) });
      }
    } finally {
      setLeyendo(false);
    }
  };

  /** Agrega, cambia o quita el sílabo de una materia y vuelve a validar para mostrar lo leído. */
  const cambiarSilabo = async (materia: string, archivoSilabo: File | null): Promise<void> => {
    if (!archivo) return;
    const clave = materia.trim().toLowerCase();
    const nuevos = silabos.filter((s) => s.materia.trim().toLowerCase() !== clave);
    if (archivoSilabo) nuevos.push({ materia, archivo: archivoSilabo });
    setSilabos(nuevos);
    setError(null);
    setLeyendo(true);
    try {
      setCertificado(await expedientesApi.validarCertificado(archivo, nuevos));
    } catch (fallo) {
      // El sílabo no se pudo leer: se retira para no bloquear el registro y se muestra el motivo.
      setSilabos(silabos);
      setError({ texto: mensajeDeError(fallo), detalles: detallesDeError(fallo) });
    } finally {
      setLeyendo(false);
    }
  };

  const registrar = async (): Promise<void> => {
    if (!archivo || !certificado) return;
    setRegistrando(true);
    setError(null);
    setErrorAlEnviar(true);
    try {
      setResultado(await expedientesApi.registrarCertificado(archivo, silabos));
    } catch (fallo) {
      // Si al registrar el servidor encuentra problemas en el certificado (por ejemplo, un revisor que
      // se desactivó), se muestra la lista de verificación y ya no se ofrece registrar.
      const conLista = rechazoDe(fallo);
      if (conLista) {
        setRechazo(conLista);
        setCertificado(null);
      } else {
        const datos = fallo instanceof ErrorApi && fallo.datos && typeof fallo.datos === 'object'
          ? fallo.datos as { expedienteId?: unknown }
          : null;
        setError({
          texto: mensajeDeError(fallo),
          detalles: detallesDeError(fallo),
          expedienteId: fallo instanceof ErrorApi && fallo.estado === 409 && typeof datos?.expedienteId === 'string'
            ? datos.expedienteId
            : undefined,
          es409: fallo instanceof ErrorApi && fallo.estado === 409,
        });
      }
    } finally {
      setRegistrando(false);
    }
  };

  const soltar = (evento: DragEvent<HTMLDivElement>): void => {
    evento.preventDefault();
    setArrastrando(false);
    void elegir(evento.dataTransfer.files[0]);
  };

  const tituloRechazo = rechazo
    ? TITULO_RECHAZO[rechazo.tipo] ?? 'El documento no se puede registrar'
    : '';
  const mensajeRechazo = rechazo ? sinTituloRepetido(rechazo.mensaje, tituloRechazo) : '';

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        titulo="Registrar solicitud de homologación"
        descripcion="Cargue el certificado de homologación SIHA (PDF preaprobado por Secretaría). Trae al estudiante, las carreras, su historial (materias aprobadas y reprobadas con notas), qué materias quiere homologar y los dos docentes revisores. Al registrarla, el agente la analiza con los sílabos oficiales y queda asignada a esos docentes."
        acciones={usuario ? <Etiqueta tono="marca">Operador: {usuario.nombreCompleto}</Etiqueta> : null}
      />

      {resultado ? (
        <Tarjeta
          titulo={resultado.publicado ? 'Solicitud registrada y enviada' : 'Solicitud registrada — envío pendiente'}
          descripcion={resultado.mensaje}
        >
          <div className="space-y-4">
            <Alerta tono={resultado.publicado ? 'exito' : 'advertencia'} titulo={`Expediente ${resultado.expediente.codigo}`}>
              {resultado.certificado.estudianteNombre} · {resultado.certificado.carreraOrigen} →{' '}
              {resultado.certificado.carreraDestino} · {resultado.certificado.solicitudes.length} materia(s) pedida(s).
              Asignada a {resultado.certificado.revisores.map((r) => r.nombre).join(' y ')}. El agente ya está analizando los contenidos.
            </Alerta>
            <div className="flex flex-wrap gap-2">
              <Link
                to={`/solicitudes/${resultado.expediente.id}`}
                className="rounded-md bg-institucional-700 px-4 py-2 text-sm font-semibold text-white hover:bg-institucional-800"
              >
                Ver la solicitud y su análisis
              </Link>
              <Link
                to={RUTAS.solicitudes}
                className="rounded-md border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
              >
                Ir a solicitudes
              </Link>
              <Boton variante="secundario" onClick={reiniciar}>
                Registrar otra solicitud
              </Boton>
            </div>
          </div>
        </Tarjeta>
      ) : null}

      {!resultado ? (
        <Tarjeta
          titulo="Certificado de homologación (único documento)"
          descripcion="Solo se acepta el formato oficial SIHA-CH-01. Un plan analítico suelto, un PDF escaneado, materias de hace más de 10 años, revisores que no son docentes del sistema o datos incompletos se rechazan."
        >
          <div className="space-y-4">
            <div
              role="button"
              tabIndex={0}
              aria-label="Seleccionar el certificado SIHA en PDF"
              onClick={() => entrada.current?.click()}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') entrada.current?.click();
              }}
              onDragOver={(e) => {
                e.preventDefault();
                setArrastrando(true);
              }}
              onDragLeave={() => setArrastrando(false)}
              onDrop={soltar}
              className={`flex cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed px-6 py-10 text-center transition ${
                arrastrando ? 'border-institucional-500 bg-institucional-50' : 'border-slate-300 hover:bg-slate-50'
              }`}
            >
              <p className="text-sm font-semibold text-ink">
                {archivo ? archivo.name : 'Arrastre aquí el certificado SIHA o haga clic para elegirlo'}
              </p>
              <p className="mt-1 text-xs text-faint">
                {archivo ? formatearBytes(archivo.size) : `PDF con texto seleccionable, máximo ${formatearBytes(TAMANO_MAXIMO)}`}
              </p>
              <input
                ref={entrada}
                type="file"
                accept="application/pdf,.pdf"
                className="sr-only"
                data-testid="certificado-pdf"
                onClick={(e) => e.stopPropagation()}
                onChange={(e: ChangeEvent<HTMLInputElement>) => void elegir(e.target.files?.[0])}
              />
            </div>

            <p className="text-xs text-muted">
              Ejemplos con la plantilla oficial:{' '}
              {EJEMPLOS.map((ejemplo, i) => (
                <span key={ejemplo.archivo}>
                  {i > 0 ? ' · ' : ''}
                  <a className="font-semibold text-institucional-700 hover:underline" href={urlEjemplo(ejemplo.archivo)} download>
                    {ejemplo.texto}
                  </a>
                </span>
              ))}
            </p>

            {leyendo ? <Alerta tono="informacion">Leyendo y validando el certificado…</Alerta> : null}

            {error && !errorAlEnviar ? (
              <Alerta tono="error" titulo="El documento no se puede registrar" detalles={error.detalles}>
                {error.texto}
              </Alerta>
            ) : null}

            {rechazo ? (
              <div className="space-y-3" data-testid="rechazo-certificado">
                <Alerta
                  tono="error"
                  titulo={tituloRechazo}
                  detalles={rechazo.secciones.length === 0 ? rechazo.detalles : undefined}
                >
                  {mensajeRechazo || null}
                </Alerta>
                {rechazo.secciones.length > 0 ? <ListaVerificacion secciones={rechazo.secciones} /> : null}
                <div className="flex flex-wrap items-center gap-3">
                  <Boton disabled>Registrar y enviar solicitud</Boton>
                  <span className="text-xs text-muted">
                    Solo se puede registrar un certificado sin problemas. Corríjalo en Secretaría y vuelva a cargarlo.
                  </span>
                </div>
              </div>
            ) : null}
          </div>
        </Tarjeta>
      ) : null}

      {certificado && !resultado && certificado.tipoTramite === 'EXTERNO' ? (
        <SilabosExternos
          certificado={certificado}
          silabos={silabos}
          deshabilitado={leyendo || registrando}
          onCambiar={(materia, archivoSilabo) => void cambiarSilabo(materia, archivoSilabo)}
        />
      ) : null}

      {certificado && !resultado ? (
        <Tarjeta
          titulo="Datos leídos del certificado"
          descripcion="Revise que corresponden al documento. Nada de esto se edita aquí: si algo está mal, el certificado debe corregirse en Secretaría."
        >
          <div className="space-y-4">
            {certificado.avisos && certificado.avisos.length > 0 ? (
              <Alerta
                tono="advertencia"
                titulo="Revise el nombre de la materia de destino"
                detalles={certificado.avisos.map((a) => a.mensaje)}
              >
                Puede registrar la solicitud, pero el agente no podrá comparar estas materias hasta que el certificado use el
                nombre oficial de la malla.
              </Alerta>
            ) : null}
            <ResumenCertificado certificado={certificado} />
            {error && errorAlEnviar ? (
              <div ref={avisoEnvio} tabIndex={-1}>
                <Alerta tono="error" titulo="El documento no se puede registrar" detalles={error.detalles}>
                  {error.texto}
                  {errorAlEnviar && error.es409 ? (
                    <p className="mt-2">
                      <Link className="text-institucional-700 underline" to={error.expedienteId ? RUTAS.detalleSolicitud.replace(':id', error.expedienteId) : RUTAS.solicitudes}>
                        {error.expedienteId ? 'Ver la solicitud registrada' : 'Ver la lista de solicitudes'}
                      </Link>
                    </p>
                  ) : null}
                </Alerta>
              </div>
            ) : null}
            <div className="flex flex-wrap gap-2">
              <Boton onClick={() => void registrar()} cargando={registrando} textoCargando="Registrando…">
                Registrar y enviar solicitud
              </Boton>
              <Boton variante="secundario" onClick={reiniciar} disabled={registrando}>
                Cambiar documento
              </Boton>
            </div>
          </div>
        </Tarjeta>
      ) : null}
    </div>
  );
}
