import { useRef, useState } from 'react';
import type { ChangeEvent, DragEvent } from 'react';
import { Link } from 'react-router-dom';
import { detallesDeError, expedientesApi, mensajeDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, Etiqueta, Tabla, Tarjeta } from '../components/ui';
import type { ColumnaTabla } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { RUTAS } from '../layouts/navegacion';
import { ETIQUETA_TIPO_TRAMITE } from '../types/comunes';
import type { AsignaturaCertificada, CertificadoLeido, SolicitudCertificado } from '../types/expedientes';
import { anosDesdeFecha } from '../utils/analisis';
import { formatearBytes } from '../utils/formato';

const TAMANO_MAXIMO = 10 * 1024 * 1024;

/** PDF de ejemplo con la plantilla SIHA-CH-01 (carpeta `public/plantillas`). */
const EJEMPLOS = [
  { archivo: 'SIHA-IND-SW-completo.pdf', texto: 'Industrial → Software (Física I y II)' },
  { archivo: 'SIHA-IND-SW-solo-fisica1.pdf', texto: 'Industrial → Software (solo Física I)' },
  { archivo: 'SIHA-UCuenca-SW-externo.pdf', texto: 'Universidad de Cuenca → Software (externo)' },
];

const urlEjemplo = (archivo: string): string => `${import.meta.env.BASE_URL}plantillas/${archivo}`;

const COLUMNAS: Array<ColumnaTabla<AsignaturaCertificada>> = [
  {
    clave: 'asignatura',
    encabezado: 'Asignatura aprobada',
    render: (a) => (
      <div>
        <p className="text-sm font-medium text-ink">{a.nombre}</p>
        <p className="font-mono text-xs text-faint">{a.codigo}</p>
      </div>
    ),
  },
  { clave: 'semestre', encabezado: 'Semestre', className: 'num', render: (a) => a.semestre },
  { clave: 'creditos', encabezado: 'Créditos', className: 'num', render: (a) => a.creditos },
  { clave: 'horas', encabezado: 'Horas', className: 'num', render: (a) => a.horasTotales },
  {
    clave: 'fecha',
    encabezado: 'Aprobada el',
    render: (a) => {
      const anos = anosDesdeFecha(a.fechaAprobacion);
      return (
        <div>
          <p className="text-sm">{a.fechaAprobacion}</p>
          <p className="text-xs text-faint">{anos === null ? '—' : anos === 1 ? 'hace 1 año' : `hace ${anos} años`}</p>
        </div>
      );
    },
  },
  { clave: 'nota', encabezado: 'Nota', className: 'num', render: (a) => a.nota.toFixed(2) },
  {
    clave: 'unidades',
    encabezado: 'Contenidos',
    render: (a) => (
      <ul className="space-y-0.5 text-xs text-muted">
        {a.unidades.map((u) => (
          <li key={u.numero}>
            U{u.numero}. {u.nombre} ({u.horas} h)
          </li>
        ))}
      </ul>
    ),
  },
];

/** Datos leídos del certificado: estudiante, trámite, carreras y materias aprobadas. */
function ResumenCertificado({ certificado }: { certificado: CertificadoLeido }): JSX.Element {
  const dato = (etiqueta: string, valor: string): JSX.Element => (
    <div>
      <dt className="text-xs uppercase tracking-wide text-faint">{etiqueta}</dt>
      <dd className="mt-0.5 text-sm text-ink">{valor}</dd>
    </div>
  );

  return (
    <div className="space-y-4">
      <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {dato('Estudiante', `${certificado.estudianteNombre} · ${certificado.estudianteIdentificacion}`)}
        {dato('Certificado', `${certificado.numeroCertificado} · ${certificado.fechaEmision}`)}
        {dato('Trámite', `${ETIQUETA_TIPO_TRAMITE[certificado.tipoTramite] ?? certificado.tipoTramite} · ${certificado.rutaNormativa}`)}
        {dato('Institución de origen', certificado.institucionOrigen)}
        {dato('Carrera de origen', `${certificado.carreraOrigenCodigo} — ${certificado.carreraOrigenNombre}`)}
        {dato('Carrera de destino', `${certificado.carreraDestinoCodigo} — ${certificado.carreraDestinoNombre}`)}
        {dato('Emitido por', certificado.emitidoPor)}
        {dato('Docentes revisores', certificado.revisores.map((r) => r.nombre).join(' y '))}
      </dl>
      <Tabla columnas={COLUMNAS} filas={certificado.asignaturas} claveFila={(a) => a.codigo} />
    </div>
  );
}

/**
 * Registro de la solicitud con UN SOLO PDF: el certificado de homologación SIHA (SIHA-CH-01).
 * El certificado llega preaprobado por Secretaría con todos los datos (estudiante, carreras, materias
 * aprobadas con semestre, créditos, horas, fecha, nota, matrícula y contenidos). El operador no
 * digita ni selecciona nada: el sistema lo lee, lo valida y, si cumple, lo registra y lo envía.
 */
export function SolicitudNuevaPage(): JSX.Element {
  useTituloPagina('Registrar solicitud');
  const { usuario } = useAuth();
  const entrada = useRef<HTMLInputElement>(null);
  const [archivo, setArchivo] = useState<File | null>(null);
  const [leyendo, setLeyendo] = useState(false);
  const [certificado, setCertificado] = useState<CertificadoLeido | null>(null);
  const [error, setError] = useState<{ texto: string; detalles: string[] } | null>(null);
  const [registrando, setRegistrando] = useState(false);
  const [resultado, setResultado] = useState<SolicitudCertificado | null>(null);
  const [arrastrando, setArrastrando] = useState(false);

  const reiniciar = (): void => {
    setArchivo(null);
    setCertificado(null);
    setError(null);
    setResultado(null);
    if (entrada.current) entrada.current.value = '';
  };

  const elegir = async (seleccionado: File | undefined): Promise<void> => {
    setError(null);
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
    setLeyendo(true);
    try {
      setCertificado(await expedientesApi.validarCertificado(seleccionado));
    } catch (fallo) {
      setError({ texto: mensajeDeError(fallo), detalles: detallesDeError(fallo) });
    } finally {
      setLeyendo(false);
    }
  };

  const registrar = async (): Promise<void> => {
    if (!archivo || !certificado) return;
    setRegistrando(true);
    setError(null);
    try {
      setResultado(await expedientesApi.registrarCertificado(archivo));
    } catch (fallo) {
      setError({ texto: mensajeDeError(fallo), detalles: detallesDeError(fallo) });
    } finally {
      setRegistrando(false);
    }
  };

  const soltar = (evento: DragEvent<HTMLDivElement>): void => {
    evento.preventDefault();
    setArrastrando(false);
    void elegir(evento.dataTransfer.files[0]);
  };

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        titulo="Registrar solicitud de homologación"
        descripcion="Cargue el certificado de homologación SIHA (PDF preaprobado por Secretaría). Trae todo: estudiante, carreras, materias aprobadas con notas y contenidos, y los dos docentes revisores. Al registrarla, el agente la analiza y queda asignada a esos docentes."
        acciones={usuario ? <Etiqueta tono="marca">Operador: {usuario.nombreCompleto}</Etiqueta> : null}
      />

      {resultado ? (
        <Tarjeta
          titulo={resultado.publicado ? 'Solicitud registrada y enviada' : 'Solicitud registrada — envío pendiente'}
          descripcion={resultado.mensaje}
        >
          <div className="space-y-4">
            <Alerta tono={resultado.publicado ? 'exito' : 'advertencia'} titulo={`Expediente ${resultado.expediente.codigo}`}>
              {resultado.certificado.estudianteNombre} · {resultado.certificado.carreraOrigenCodigo} →{' '}
              {resultado.certificado.carreraDestinoCodigo} · {resultado.certificado.asignaturas.length} materia(s) aprobada(s).
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

            {error ? (
              <Alerta tono="error" titulo="El documento no se puede registrar" detalles={error.detalles}>
                {error.texto}
              </Alerta>
            ) : null}
          </div>
        </Tarjeta>
      ) : null}

      {certificado && !resultado ? (
        <Tarjeta
          titulo="Datos leídos del certificado"
          descripcion="Revise que corresponden al documento. Nada de esto se edita aquí: si algo está mal, el certificado debe corregirse en Secretaría."
        >
          <div className="space-y-4">
            <ResumenCertificado certificado={certificado} />
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
