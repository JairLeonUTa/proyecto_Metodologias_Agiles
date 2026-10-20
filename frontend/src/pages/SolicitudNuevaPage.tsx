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
 * Integra el catálogo real (carreras, mallas y asignaturas) a través del Gateway, valida
 * localmente los documentos PDF, envía el expediente y muestra el identificador generado
 * junto con los errores devueltos por el servidor.
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
  const [expedientePendiente, setExpedientePendiente] = useState<ExpedienteAceptado | null>(null);
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

  const registrar = async (): Promise<void> => {
    limpiar();
    setAceptado(null);

    if (!expedientePendiente) {
      const nuevos = validar();
      setErrores(nuevos);
      if (Object.keys(nuevos).length > 0) {
        reportarError('Revise los campos marcados antes de registrar la solicitud.');
        return;
      }
    }

    setEnviando(true);
    try {
      let expediente = expedientePendiente;
      if (!expediente) {
        const filasValidas = filas.filter((fila) => fila.codigoOrigen.trim() && fila.nombreOrigen.trim());
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
        setExpedientePendiente(expediente);
      }

      let archivosActualizados = archivos;
      if (expedientePendiente) {
        // Una respuesta de red perdida puede ocultar una carga que sí llegó al servidor.
        const expedienteActual = await expedientesApi.obtener(expediente.id);
        archivosActualizados = archivos.map((archivo) => {
          const documentoServidor = expedienteActual.documentos.find((documento) =>
            documento.nombreArchivo === archivo.nombreArchivo &&
            documento.tamanoBytes === archivo.tamanoBytes &&
            documento.hashSha256.toLowerCase() === (archivo.hashSha256 ?? '').toLowerCase(),
          );
          return documentoServidor
            ? { ...archivo, estadoCarga: 'cargado', errorCarga: undefined, documentoServidor }
            : archivo;
        });
        setArchivos(archivosActualizados);
      }

      for (const archivo of archivosActualizados.filter((item) => !item.documentoServidor)) {
        setArchivos((actuales) => actuales.map((item) =>
          item.id === archivo.id ? { ...item, estadoCarga: 'cargando', errorCarga: undefined } : item,
        ));
        try {
          const documentoServidor = await expedientesApi.subirDocumento(expediente.id, archivo.archivo);
          archivosActualizados = archivosActualizados.map((item) =>
            item.id === archivo.id
              ? { ...item, estadoCarga: 'cargado', errorCarga: undefined, documentoServidor }
              : item,
          );
          setArchivos(archivosActualizados);
        } catch (fallo) {
          const errorCarga = mensajeDeError(fallo);
          archivosActualizados = archivosActualizados.map((item) =>
            item.id === archivo.id ? { ...item, estadoCarga: 'error', errorCarga } : item,
          );
          setArchivos(archivosActualizados);
          throw fallo;
        }
      }

      const documentosCompletos = archivosActualizados.every((archivo) => Boolean(archivo.documentoServidor));
      if (!documentosCompletos) {
        throw new Error('Hay documentos obligatorios pendientes de carga. Reintente antes de publicar el expediente.');
      }

      const respuesta = await expedientesApi.republicar(expediente.id);
      if (!respuesta.publicado) {
        throw new Error(`${respuesta.mensaje} El expediente y sus documentos están guardados; la publicación sigue pendiente.`);
      }
      setAceptado(respuesta);
      setExpedientePendiente(null);
    } catch (fallo) {
      reportarError(mensajeDeError(fallo), detallesDeError(fallo));
    } finally {
      setEnviando(false);
    }
  };

  const reiniciar = (): void => {
    setFormulario(FORMULARIO_INICIAL);
    setFilas([FILA_VACIA()]);
    setArchivos([]);
    setErrores({});
    setInstitucionPersonalizada(false);
    setAceptado(null);
    setExpedientePendiente(null);
    limpiar();
  };

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Expedientes"
        titulo="Registrar solicitud de homologación"
        descripcion="Alta de una solicitud interna (cambio de carrera dentro de la UTA) o externa (movilidad desde otra institución). El expediente se persiste y se publica de forma asíncrona en el bus de eventos."
        acciones={
          usuario ? <Etiqueta tono="marca">Operador: {usuario.nombreCompleto}</Etiqueta> : null
        }
      />

      {mensaje ? (
        <Alerta
          tono="error"
          titulo={expedientePendiente ? 'El expediente quedó pendiente' : 'No se pudo registrar la solicitud'}
          detalles={mensaje.detalles}
        >
          {mensaje.texto}
        </Alerta>
      ) : null}

      {expedientePendiente ? (
        <Alerta tono="advertencia" titulo="Envío pendiente: expediente aún no publicado">
          <p>Expediente {expedientePendiente.codigo} ({expedientePendiente.id}).</p>
          <p>
            Documentos recibidos: {archivos.filter((archivo) => Boolean(archivo.documentoServidor)).length} de {archivos.length}.
            {archivos.every((archivo) => Boolean(archivo.documentoServidor))
              ? ' Todos los planes están completos; falta publicar el expediente.'
              : ' Corrija o reintente los documentos pendientes antes de publicar.'}
          </p>
        </Alerta>
      ) : null}

      {aceptado ? (
        <Tarjeta
          titulo="Solicitud registrada (HTTP 202 Accepted)"
          descripcion="El expediente y sus documentos fueron guardados antes de su publicación asíncrona."
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
        <fieldset disabled={Boolean(expedientePendiente || aceptado)} className="contents">
        <SeccionFormulario
          titulo="1. Datos del trámite"
          descripcion="El tipo de trámite determina la ruta normativa sugerida (CAU-P-011 o CAU-P-122)."
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <Campo htmlFor="tipo-tramite" etiqueta="Tipo de trámite" obligatorio error={errores.tipoTramite}>
              <Selector
                id="tipo-tramite"
                value={formulario.tipoTramite}
                disabled={enviando}
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
                disabled={enviando}
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
                disabled={enviando}
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
                disabled={enviando}
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
                disabled={enviando}
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
                        disabled={enviando}
                        placeholder="Ej.: PRG-101"
                        onChange={(evento) => actualizarFila(indice, { codigoOrigen: evento.target.value })}
                      />
                    </Campo>

                    <Campo htmlFor={idNombre} etiqueta="Nombre de origen" obligatorio>
                      <EntradaTexto
                        id={idNombre}
                        value={fila.nombreOrigen}
                        maxLength={200}
                        disabled={enviando}
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
                        disabled={enviando}
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
            disabled={enviando}
            onClick={() => setFilas((actuales) => [...actuales, FILA_VACIA()])}
          >
            Agregar materia
          </Boton>
        </SeccionFormulario>

        <SeccionFormulario
          titulo="3. Documentos de respaldo (PDF)"
          descripcion="Validación local de tipo y tamaño antes de enviar el expediente; el hash SHA-256 se calcula en el navegador."
        >
          {errores.documentos ? <Alerta tono="error">{errores.documentos}</Alerta> : null}
          <CargadorArchivosPdf
            archivos={archivos}
            onCambiar={setArchivos}
            deshabilitado={enviando || Boolean(expedientePendiente || aceptado)}
          />
        </SeccionFormulario>
        </fieldset>

        <div className="flex flex-wrap items-center justify-end gap-2">
          <Boton variante="secundario" onClick={reiniciar} disabled={enviando || Boolean(expedientePendiente || aceptado)}>
            Limpiar formulario
          </Boton>
          <Boton
            type="submit"
            cargando={enviando}
            disabled={Boolean(aceptado)}
            textoCargando="Registrando, cargando documentos y publicando…"
          >
            {expedientePendiente ? 'Reintentar carga y publicación' : 'Registrar solicitud'}
          </Boton>
        </div>
      </form>

      <Tarjeta titulo="Alcance del registro" descripcion="Consideraciones del Sprint 1.">
        <ul className="list-inside list-disc space-y-1 text-xs text-slate-600">
          <li>El expediente se persiste antes de publicarse, por lo que nunca se pierde una solicitud válida.</li>
          <li>La publicación del evento es asíncrona (MassTransit / RabbitMQ); la respuesta es 202 Accepted.</li>
          <li>
            Roles autorizados para registrar expedientes: {(ROLES_EXPEDIENTES as string[]).join(', ')}.
          </li>
        </ul>
      </Tarjeta>
    </div>
  );
}
