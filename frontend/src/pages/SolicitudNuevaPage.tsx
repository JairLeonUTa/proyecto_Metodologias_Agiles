import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { asignaturasApi, carrerasApi, expedientesApi, mallasApi } from '../api';
import { mensajeDeError, detallesDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { CargadorArchivosPdf } from '../components/CargadorArchivosPdf';
import type { ArchivoAdjunto } from '../components/CargadorArchivosPdf';
import {
  Alerta,
  Boton,
  Campo,
  EntradaTexto,
  Etiqueta,
  SeccionFormulario,
  Selector,
  Tarjeta,
} from '../components/ui';
import { RUTAS, ROLES_EXPEDIENTES } from '../layouts/navegacion';
import { useAuth } from '../hooks/useAuth';
import { useMensajes } from '../hooks/useMensajes';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { ExpedienteAceptado } from '../types/expedientes';
import {
  DESCRIPCION_RUTA,
  ETIQUETA_ESTADO,
  ETIQUETA_TIPO_TRAMITE,
  INSTITUCION_OTRA,
  INSTITUCIONES_ORIGEN,
  RUTAS_NORMATIVAS,
  TIPOS_TRAMITE,
  TIPOS_TRAMITE_TODOS,
} from '../types/comunes';
import { aNumero } from '../utils/validaciones';

/** Fila del detalle de materias del expediente. */
interface FilaAsignatura {
  id: string;
  codigoOrigen: string;
  nombreOrigen: string;
  codigoDestino: string;
  nota: string;
}

interface FormularioSolicitud {
  tipoTramite: string;
  rutaNormativa: string;
  institucionOrigen: string;
  carreraOrigenCodigo: string;
  carreraDestinoCodigo: string;
}

const INSTITUCION_PROPIA = 'Universidad Técnica de Ambato';

const FORMULARIO_INICIAL: FormularioSolicitud = {
  tipoTramite: TIPOS_TRAMITE.INTERNO,
  rutaNormativa: RUTAS_NORMATIVAS.CAMBIO_CARRERA_OU,
  institucionOrigen: INSTITUCION_PROPIA,
  carreraOrigenCodigo: '',
  carreraDestinoCodigo: '',
};

const FILA_VACIA = (): FilaAsignatura => ({
  id: `fila-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`,
  codigoOrigen: '',
  nombreOrigen: '',
  codigoDestino: '',
  nota: '',
});

/**
 * Registro de solicitudes de homologación internas y externas (S1-A06, S1-A07 y S1-A09).
 *
 * Integra el catálogo real (carreras, mallas y asignaturas) a través del Gateway y sigue el
 * flujo acordado con el Microservicio B:
 *  1. registra el expediente como borrador (PENDIENTE_DOCUMENTOS, sin evento);
 *  2. carga cada PDF real en `POST /api/expedientes/{id}/documentos` (el servidor valida, almacena
 *     y calcula el SHA-256);
 *  3. envía el expediente (`POST /api/expedientes/{id}/enviar`); el servidor verifica los PDF en disco
 *     y publica el evento solo si se cumplen los requisitos documentales.
 * Si una carga falla, el borrador se conserva y el operador reintenta solo lo pendiente.
 */
export function SolicitudNuevaPage(): JSX.Element {
  useTituloPagina('Registrar solicitud');

  const { usuario } = useAuth();
  const [formulario, setFormulario] = useState<FormularioSolicitud>(FORMULARIO_INICIAL);
  const [filas, setFilas] = useState<FilaAsignatura[]>([FILA_VACIA()]);
  const [archivos, setArchivos] = useState<ArchivoAdjunto[]>([]);
  const [errores, setErrores] = useState<Record<string, string>>({});
  const [institucionPersonalizada, setInstitucionPersonalizada] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [aceptado, setAceptado] = useState<ExpedienteAceptado | null>(null);
  /** Borrador ya registrado en el servidor: permite reintentar cargas sin duplicar el expediente. */
  const [borrador, setBorrador] = useState<ExpedienteAceptado | null>(null);
  const [paso, setPaso] = useState('');
  const { mensaje, error: reportarError, limpiar } = useMensajes();

  const carreras = usePeticion(() => carrerasApi.listar(), []);
  const mallas = usePeticion(() => mallasApi.listar(), []);

  // Malla vigente (o más reciente) de la carrera destino, para sugerir las materias de destino.
  const mallaDestino = useMemo(() => {
    const lista = mallas.datos ?? [];
    const deCarrera = lista.filter((malla) => malla.carreraCodigo === formulario.carreraDestinoCodigo);
    return deCarrera.find((malla) => malla.vigente) ?? deCarrera[0] ?? null;
  }, [mallas.datos, formulario.carreraDestinoCodigo]);

  const mallaOrigen = useMemo(() => {
    const lista = mallas.datos ?? [];
    const deCarrera = lista.filter((malla) => malla.carreraCodigo === formulario.carreraOrigenCodigo);
    return deCarrera.find((malla) => malla.vigente) ?? deCarrera[0] ?? null;
  }, [mallas.datos, formulario.carreraOrigenCodigo]);

  const catalogoDestino = usePeticion(
    () => (mallaDestino ? asignaturasApi.listar(mallaDestino.id) : Promise.resolve([])),
    [mallaDestino?.id ?? 0],
  );

  const catalogoOrigen = usePeticion(
    () => (mallaOrigen ? asignaturasApi.listar(mallaOrigen.id) : Promise.resolve([])),
    [mallaOrigen?.id ?? 0],
  );

  const esExterno = formulario.tipoTramite === TIPOS_TRAMITE.EXTERNO;

  // Una vez registrado el borrador, los datos del trámite ya están en el servidor: solo se
  // pueden reintentar las cargas de documentos y el envío.
  const bloqueado = enviando || borrador !== null;

  const cambiarTipoTramite = (tipo: string): void => {
    setInstitucionPersonalizada(false);
    setFormulario((actual) => ({
      ...actual,
      tipoTramite: tipo,
      // Sugerencia normativa: trámite interno → CAU-P-011; trámite externo → CAU-P-122.
      rutaNormativa: tipo === TIPOS_TRAMITE.EXTERNO ? RUTAS_NORMATIVAS.MOVILIDAD : RUTAS_NORMATIVAS.CAMBIO_CARRERA_OU,
      institucionOrigen: tipo === TIPOS_TRAMITE.EXTERNO ? '' : INSTITUCION_PROPIA,
    }));
  };

  const actualizarFila = (indice: number, cambios: Partial<FilaAsignatura>): void => {
    setFilas((actuales) => actuales.map((fila, posicion) => (posicion === indice ? { ...fila, ...cambios } : fila)));
  };

  const validar = (): Record<string, string> => {
    const nuevos: Record<string, string> = {};

    if (!formulario.rutaNormativa) {
      nuevos.rutaNormativa = 'Seleccione la ruta normativa aplicable.';
    }
    if (!formulario.carreraOrigenCodigo) {
      nuevos.carreraOrigenCodigo = 'Seleccione la carrera de origen.';
    }
    if (!formulario.carreraDestinoCodigo) {
      nuevos.carreraDestinoCodigo = 'Seleccione la carrera receptora (destino).';
    }
    if (
      formulario.carreraOrigenCodigo &&
      formulario.carreraOrigenCodigo === formulario.carreraDestinoCodigo &&
      !esExterno
    ) {
      nuevos.carreraDestinoCodigo = 'En un trámite interno, la carrera receptora debe ser distinta de la de origen.';
    }
    if (esExterno && !formulario.institucionOrigen.trim()) {
      nuevos.institucionOrigen = 'Indique la institución de educación superior de origen.';
    }

    const filasValidas = filas.filter((fila) => fila.codigoOrigen.trim() || fila.nombreOrigen.trim());
    if (filasValidas.length === 0) {
      nuevos.asignaturas = 'Registre al menos una materia aprobada que se desea homologar.';
    } else {
      const incompleta = filasValidas.findIndex(
        (fila) => !fila.codigoOrigen.trim() || !fila.nombreOrigen.trim(),
      );
      if (incompleta >= 0) {
        nuevos.asignaturas = `La materia ${incompleta + 1} requiere código y nombre de origen.`;
      }

      const notaInvalida = filasValidas.findIndex((fila) => {
        if (fila.nota.trim() === '') {
          return false;
        }
        const nota = aNumero(fila.nota);
        return Number.isNaN(nota) || nota < 0 || nota > 20;
      });
      if (notaInvalida >= 0) {
        nuevos.asignaturas = `La nota de la materia ${notaInvalida + 1} debe estar entre 0 y 20.`;
      }
    }

    if (archivos.length === 0) {
      nuevos.documentos = 'Adjunte al menos un plan analítico en PDF que respalde la solicitud.';
    }

    return nuevos;
  };

  const marcarArchivo = (id: string, cambios: Partial<ArchivoAdjunto>): void => {
    setArchivos((actuales) => actuales.map((archivo) => (archivo.id === id ? { ...archivo, ...cambios } : archivo)));
  };

  /** Carga en el servidor los PDF que aún no están validados. Devuelve cuántos fallaron. */
  const cargarDocumentos = async (expedienteId: string): Promise<number> => {
    let fallidos = 0;

    for (const adjunto of archivos) {
      if (adjunto.estado === 'cargado') {
        continue;
      }

      setPaso(`Cargando ${adjunto.nombreArchivo}…`);
      marcarArchivo(adjunto.id, { estado: 'subiendo', error: undefined });

      try {
        const documento = await expedientesApi.subirDocumento(expedienteId, adjunto.archivo);

        // El hash válido es el del servidor; si difiere del calculado en el navegador, el archivo
        // se alteró en tránsito y se retira para volver a cargarlo.
        if (adjunto.hashSha256 && documento.hashSha256 !== adjunto.hashSha256) {
          await expedientesApi.retirarDocumento(expedienteId, documento.id);
          throw new Error('El SHA-256 calculado por el servidor no coincide con el del navegador. Vuelva a cargarlo.');
        }

        marcarArchivo(adjunto.id, { estado: 'cargado', documentoId: documento.id, error: undefined });
      } catch (fallo) {
        fallidos += 1;
        marcarArchivo(adjunto.id, { estado: 'error', error: mensajeDeError(fallo) });
      }
    }

    return fallidos;
  };

  const registrar = async (): Promise<void> => {
    limpiar();
    setAceptado(null);

    const nuevos = validar();
    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) {
      reportarError('Revise los campos marcados antes de registrar la solicitud.');
      return;
    }

    const filasValidas = filas.filter((fila) => fila.codigoOrigen.trim() && fila.nombreOrigen.trim());

    setEnviando(true);
    try {
      // 1) Borrador (solo la primera vez; los reintentos reutilizan el mismo expediente).
      let expediente = borrador;
      if (!expediente) {
        setPaso('Registrando el expediente…');
        expediente = await expedientesApi.registrar({
          tipoTramite: formulario.tipoTramite,
          rutaNormativa: formulario.rutaNormativa,
          carreraOrigenCodigo: formulario.carreraOrigenCodigo.trim(),
          carreraDestinoCodigo: formulario.carreraDestinoCodigo.trim(),
          institucionOrigen: formulario.institucionOrigen.trim() || null,
          asignaturas: filasValidas.map((fila) => ({
            codigoOrigen: fila.codigoOrigen.trim(),
            nombreOrigen: fila.nombreOrigen.trim(),
            codigoDestino: fila.codigoDestino || null,
            nota: fila.nota.trim() === '' ? null : aNumero(fila.nota),
          })),
        });
        setBorrador(expediente);
      }

      // 2) Carga real de los PDF.
      const fallidos = await cargarDocumentos(expediente.id);
      if (fallidos > 0) {
        reportarError(
          `El expediente ${expediente.codigo} quedó guardado como borrador, pero ${fallidos} documento(s) no se cargaron. ` +
            'Retire o reemplace los marcados como "No cargado" y pulse "Reintentar y enviar". No se publicó ningún evento.',
        );
        return;
      }

      // 3) Envío: el servidor verifica los PDF en disco y publica.
      setPaso('Verificando documentos y enviando…');
      const respuesta = await expedientesApi.enviar(expediente.id);
      setAceptado(respuesta);
    } catch (fallo) {
      reportarError(mensajeDeError(fallo), detallesDeError(fallo));
    } finally {
      setPaso('');
      setEnviando(false);
    }
  };

  /** Retira en el servidor un PDF ya cargado en el borrador antes de quitarlo de la lista. */
  const retirarDelServidor = async (adjunto: ArchivoAdjunto): Promise<boolean> => {
    if (!borrador || !adjunto.documentoId) {
      return true;
    }

    try {
      await expedientesApi.retirarDocumento(borrador.id, adjunto.documentoId);
      return true;
    } catch (fallo) {
      reportarError(mensajeDeError(fallo));
      return false;
    }
  };

  const reiniciar = (): void => {
    setFormulario(FORMULARIO_INICIAL);
    setFilas([FILA_VACIA()]);
    setArchivos([]);
    setErrores({});
    setInstitucionPersonalizada(false);
    setAceptado(null);
    setBorrador(null);
    limpiar();
  };

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Expedientes"
        titulo="Registrar solicitud de homologación"
        descripcion="Alta de una solicitud interna (cambio de carrera dentro de la UTA) o externa (movilidad desde otra institución). El expediente se registra, se cargan y validan sus PDF en el servidor y solo entonces se publica en el bus de eventos."
        acciones={
          usuario ? <Etiqueta tono="marca">Operador: {usuario.nombreCompleto}</Etiqueta> : null
        }
      />

      {mensaje ? (
        <Alerta tono="error" titulo="No se pudo registrar la solicitud" detalles={mensaje.detalles}>
          {mensaje.texto}
        </Alerta>
      ) : null}

      {aceptado ? (
        <Tarjeta
          titulo={aceptado.publicado ? 'Solicitud enviada (HTTP 202 Accepted)' : 'Publicación fallida — expediente recuperable'}
          descripcion={
            aceptado.publicado
              ? 'Los documentos se verificaron en el servidor y el expediente se publicó para su procesamiento asíncrono.'
              : 'Los documentos se verificaron, pero la publicación en el bus falló. El expediente quedó en estado recuperable y se reintentará automáticamente conservando el CorrelationId.'
          }
        >
          <div className="space-y-4">
            <Alerta tono={aceptado.publicado ? 'exito' : 'advertencia'} titulo={`Expediente ${aceptado.codigo}`}>
              {aceptado.mensaje}
            </Alerta>

            <dl className="grid gap-4 sm:grid-cols-2">
              <div>
                <dt className="text-xs uppercase tracking-wide text-slate-500">Identificador (GUID)</dt>
                <dd className="mt-1 font-mono text-xs text-slate-800">{aceptado.id}</dd>
              </div>
              <div>
                <dt className="text-xs uppercase tracking-wide text-slate-500">Código del expediente</dt>
                <dd className="mt-1 font-mono text-sm font-semibold text-slate-800">{aceptado.codigo}</dd>
              </div>
              <div>
                <dt className="text-xs uppercase tracking-wide text-slate-500">Estado</dt>
                <dd className="mt-1">
                  <Etiqueta tono={aceptado.publicado ? 'exito' : 'advertencia'}>
                    {ETIQUETA_ESTADO[aceptado.estado] ?? aceptado.estado}
                  </Etiqueta>
                </dd>
              </div>
              <div>
                <dt className="text-xs uppercase tracking-wide text-slate-500">Correlación</dt>
                <dd className="mt-1 font-mono text-xs text-slate-800">{aceptado.correlationId}</dd>
              </div>
            </dl>

            <div className="flex flex-wrap gap-2">
              <Link
                to={`/expedientes/${aceptado.id}`}
                className="rounded-md bg-institucional-700 px-4 py-2 text-sm font-semibold text-white hover:bg-institucional-800"
              >
                Ver el estado del expediente
              </Link>
              <Link
                to={RUTAS.expedientes}
                className="rounded-md border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
              >
                Ir a la bandeja
              </Link>
              <Boton variante="secundario" onClick={reiniciar}>
                Registrar otra solicitud
              </Boton>
            </div>
          </div>
        </Tarjeta>
      ) : null}

      <form
        className="space-y-4"
        noValidate
        onSubmit={(evento) => {
          evento.preventDefault();
          void registrar();
        }}
      >
        <SeccionFormulario
          titulo="1. Datos del trámite"
          descripcion="El tipo de trámite determina la ruta normativa sugerida (CAU-P-011 o CAU-P-122)."
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <Campo htmlFor="tipo-tramite" etiqueta="Tipo de trámite" obligatorio error={errores.tipoTramite}>
              <Selector
                id="tipo-tramite"
                value={formulario.tipoTramite}
                disabled={bloqueado}
                onChange={(evento) => cambiarTipoTramite(evento.target.value)}
              >
                {TIPOS_TRAMITE_TODOS.map((tipo) => (
                  <option key={tipo} value={tipo}>
                    {ETIQUETA_TIPO_TRAMITE[tipo]}
                  </option>
                ))}
              </Selector>
            </Campo>

            <Campo
              htmlFor="ruta-normativa-solicitud"
              etiqueta="Ruta normativa"
              obligatorio
              ayuda={DESCRIPCION_RUTA[formulario.rutaNormativa] ?? undefined}
              error={errores.rutaNormativa}
            >
              <Selector
                id="ruta-normativa-solicitud"
                value={formulario.rutaNormativa}
                disabled={bloqueado}
                invalido={Boolean(errores.rutaNormativa)}
                onChange={(evento) => setFormulario({ ...formulario, rutaNormativa: evento.target.value })}
              >
                <option value={RUTAS_NORMATIVAS.CAMBIO_CARRERA_OU}>CAU-P-011 — Cambio de carrera (UTA)</option>
                <option value={RUTAS_NORMATIVAS.MOVILIDAD}>CAU-P-122 — Movilidad externa</option>
              </Selector>
            </Campo>
          </div>

          <Campo
            htmlFor="institucion-origen"
            etiqueta="Institución de origen"
            obligatorio={esExterno}
            ayuda={
              esExterno
                ? 'Seleccione la institución de educación superior de la que proviene el estudiante.'
                : 'Trámite interno: la institución de origen es la Universidad Técnica de Ambato.'
            }
            error={institucionPersonalizada ? undefined : errores.institucionOrigen}
          >
            <Selector
              id="institucion-origen"
              value={institucionPersonalizada ? INSTITUCION_OTRA : formulario.institucionOrigen}
              disabled={enviando || !esExterno}
              invalido={!institucionPersonalizada && Boolean(errores.institucionOrigen)}
              onChange={(evento) => {
                const valor = evento.target.value;
                if (valor === INSTITUCION_OTRA) {
                  setInstitucionPersonalizada(true);
                  setFormulario((actual) => ({ ...actual, institucionOrigen: '' }));
                  return;
                }
                setInstitucionPersonalizada(false);
                setFormulario((actual) => ({ ...actual, institucionOrigen: valor }));
              }}
            >
              <option value="">Seleccione la institución de origen…</option>
              {INSTITUCIONES_ORIGEN.map((institucion) => (
                <option key={institucion} value={institucion}>
                  {institucion}
                </option>
              ))}
              <option value={INSTITUCION_OTRA}>Otra institución (especificar)</option>
            </Selector>
          </Campo>

          {institucionPersonalizada ? (
            <Campo
              htmlFor="institucion-origen-otra"
              etiqueta="Nombre de la institución de origen"
              obligatorio
              ayuda="Escriba el nombre oficial de la institución de educación superior."
              error={errores.institucionOrigen}
            >
              <EntradaTexto
                id="institucion-origen-otra"
                value={formulario.institucionOrigen}
                maxLength={150}
                disabled={bloqueado}
                invalido={Boolean(errores.institucionOrigen)}
                placeholder="Ej.: Universidad Estatal Amazónica"
                onChange={(evento) => setFormulario({ ...formulario, institucionOrigen: evento.target.value })}
              />
            </Campo>
          ) : null}

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo
              htmlFor="carrera-origen"
              etiqueta="Carrera de origen"
              obligatorio
              error={errores.carreraOrigenCodigo}
            >
              <Selector
                id="carrera-origen"
                value={formulario.carreraOrigenCodigo}
                disabled={bloqueado}
                invalido={Boolean(errores.carreraOrigenCodigo)}
                onChange={(evento) => setFormulario({ ...formulario, carreraOrigenCodigo: evento.target.value })}
              >
                <option value="">Seleccione la carrera de origen…</option>
                {(carreras.datos ?? []).map((carrera) => (
                  <option key={carrera.id} value={carrera.codigo}>
                    {carrera.codigo} — {carrera.nombre}
                  </option>
                ))}
              </Selector>
            </Campo>

            <Campo
              htmlFor="carrera-destino"
              etiqueta="Carrera receptora (destino)"
              obligatorio
              ayuda={mallaDestino ? `Catálogo sugerido: malla ${mallaDestino.version} (${mallaDestino.anio}).` : undefined}
              error={errores.carreraDestinoCodigo}
            >
              <Selector
                id="carrera-destino"
                value={formulario.carreraDestinoCodigo}
                disabled={bloqueado}
                invalido={Boolean(errores.carreraDestinoCodigo)}
                onChange={(evento) => setFormulario({ ...formulario, carreraDestinoCodigo: evento.target.value })}
              >
                <option value="">Seleccione la carrera receptora…</option>
                {(carreras.datos ?? []).map((carrera) => (
                  <option key={carrera.id} value={carrera.codigo}>
                    {carrera.codigo} — {carrera.nombre}
                  </option>
                ))}
              </Selector>
            </Campo>
          </div>
        </SeccionFormulario>

        <SeccionFormulario
          titulo="2. Materias a homologar"
          descripcion="Indique las materias aprobadas en la institución de origen y, si corresponde, la asignatura equivalente del catálogo de la UTA."
        >
          {errores.asignaturas ? <Alerta tono="error">{errores.asignaturas}</Alerta> : null}

          {catalogoOrigen.datos && catalogoOrigen.datos.length > 0 ? (
            <datalist id="sugerencias-origen">
              {catalogoOrigen.datos.map((asignatura) => (
                <option key={asignatura.id} value={asignatura.codigo}>
                  {asignatura.nombre}
                </option>
              ))}
            </datalist>
          ) : null}

          <div className="space-y-3">
            {filas.map((fila, indice) => {
              const idCodigo = `materia-origen-${indice}`;
              const idNombre = `materia-nombre-${indice}`;
              const idDestino = `materia-destino-${indice}`;
              const idNota = `materia-nota-${indice}`;

              return (
                <div key={fila.id} className="rounded-md border border-slate-200 bg-slate-50/60 p-3">
                  <div className="mb-2 flex items-center justify-between">
                    <p className="text-sm font-semibold text-slate-700">Materia {indice + 1}</p>
                    <Boton
                      variante="fantasma"
                      tamano="sm"
                      className="text-rose-600 hover:bg-rose-50"
                      disabled={enviando || filas.length === 1}
                      onClick={() => setFilas((actuales) => actuales.filter((_, posicion) => posicion !== indice))}
                      aria-label={`Eliminar la materia ${indice + 1}`}
                    >
                      Eliminar
                    </Boton>
                  </div>

                  <div className="grid gap-3 lg:grid-cols-4">
                    <Campo htmlFor={idCodigo} etiqueta="Código de origen" obligatorio>
                      <EntradaTexto
                        id={idCodigo}
                        list="sugerencias-origen"
                        value={fila.codigoOrigen}
                        maxLength={30}
                        disabled={bloqueado}
                        placeholder="Ej.: PRG-101"
                        onChange={(evento) => actualizarFila(indice, { codigoOrigen: evento.target.value })}
                      />
                    </Campo>

                    <Campo htmlFor={idNombre} etiqueta="Nombre de origen" obligatorio>
                      <EntradaTexto
                        id={idNombre}
                        value={fila.nombreOrigen}
                        maxLength={200}
                        disabled={bloqueado}
                        placeholder="Ej.: Programación I"
                        onChange={(evento) => actualizarFila(indice, { nombreOrigen: evento.target.value })}
                      />
                    </Campo>

                    <Campo htmlFor={idDestino} etiqueta="Asignatura destino (catálogo UTA)">
                      <Selector
                        id={idDestino}
                        value={fila.codigoDestino}
                        disabled={enviando || !mallaDestino}
                        onChange={(evento) => actualizarFila(indice, { codigoDestino: evento.target.value })}
                      >
                        <option value="">
                          {mallaDestino ? 'Por evaluar (sin equivalencia propuesta)' : 'Seleccione primero la carrera destino'}
                        </option>
                        {(catalogoDestino.datos ?? []).map((asignatura) => (
                          <option key={asignatura.id} value={asignatura.codigo}>
                            {asignatura.codigo} — {asignatura.nombre}
                          </option>
                        ))}
                      </Selector>
                    </Campo>

                    <Campo htmlFor={idNota} etiqueta="Nota obtenida" ayuda="Escala 0-20 (opcional).">
                      <EntradaTexto
                        id={idNota}
                        type="number"
                        min={0}
                        max={20}
                        step="0.01"
                        value={fila.nota}
                        disabled={bloqueado}
                        placeholder="18.50"
                        onChange={(evento) => actualizarFila(indice, { nota: evento.target.value })}
                      />
                    </Campo>
                  </div>
                </div>
              );
            })}
          </div>

          <Boton
            variante="secundario"
            tamano="sm"
            disabled={bloqueado}
            onClick={() => setFilas((actuales) => [...actuales, FILA_VACIA()])}
          >
            Agregar materia
          </Boton>
        </SeccionFormulario>

        <SeccionFormulario
          titulo="3. Documentos de respaldo (PDF)"
          descripcion="Validación local previa; cada PDF se carga y valida en el servidor, que calcula su ruta y SHA-256 definitivos."
        >
          {errores.documentos ? <Alerta tono="error">{errores.documentos}</Alerta> : null}
          {borrador && !aceptado ? (
            <Alerta tono="advertencia" titulo={`Borrador ${borrador.codigo}`}>
              El expediente ya está registrado. Puede retirar o agregar documentos y reintentar el envío.
            </Alerta>
          ) : null}
          <CargadorArchivosPdf
            archivos={archivos}
            onCambiar={setArchivos}
            deshabilitado={enviando || aceptado !== null}
            onRetirar={retirarDelServidor}
          />
        </SeccionFormulario>

        <div className="flex flex-wrap items-center justify-end gap-2">
          <Boton variante="secundario" onClick={reiniciar} disabled={enviando}>
            {borrador ? 'Nueva solicitud' : 'Limpiar formulario'}
          </Boton>
          <Boton
            type="submit"
            cargando={enviando}
            textoCargando={paso || 'Procesando…'}
            disabled={aceptado !== null}
          >
            {borrador ? 'Reintentar y enviar' : 'Registrar y enviar solicitud'}
          </Boton>
        </div>
      </form>

      <Tarjeta titulo="Alcance del registro" descripcion="Consideraciones del Sprint 1.">
        <ul className="list-inside list-disc space-y-1 text-xs text-slate-600">
          <li>El expediente se persiste como borrador antes de cargar los PDF, por lo que nunca se pierde una solicitud válida.</li>
          <li>El evento solo se publica cuando todos los PDF están almacenados y verificados por el servidor (SHA-256 recalculado).</li>
          <li>La publicación es asíncrona (MassTransit / RabbitMQ); si el bus falla se reintenta automáticamente con la misma correlación.</li>
          <li>
            Roles autorizados para registrar expedientes: {(ROLES_EXPEDIENTES as string[]).join(', ')}.
          </li>
        </ul>
      </Tarjeta>
    </div>
  );
}
