import { useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import { detallesDeError, expedientesApi, mensajeDeError } from '../api';
import { validarArchivoPdf } from '../utils/validaciones';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, CargandoBloque, Etiqueta, MetricaResumen, Tarjeta } from '../components/ui';
import { RUTAS } from '../layouts/navegacion';
import { useMensajes } from '../hooks/useMensajes';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { ETIQUETA_ESTADO, ETIQUETA_ESTADO_DOCUMENTO, ETIQUETA_TIPO_TRAMITE } from '../types/comunes';
import { abreviarHash, formatearBytes, formatearFechaHora } from '../utils/formato';

/**
 * Detalle completo de un expediente (S1-A09). Para un borrador (PENDIENTE_DOCUMENTOS) permite cargar
 * PDF adicionales y enviarlo; para un expediente con publicación fallida permite reintentarla.
 */
export function ExpedienteDetallePage(): JSX.Element {
  const { id = '' } = useParams<{ id: string }>();
  useTituloPagina('Detalle del expediente');

  const expediente = usePeticion(() => expedientesApi.obtener(id), [id]);
  const { mensaje, exito, error: reportarError, limpiar } = useMensajes();
  const [republicando, setRepublicando] = useState(false);
  const [cargando, setCargando] = useState(false);
  const entrada = useRef<HTMLInputElement>(null);

  const ejecutar = async (accion: () => Promise<{ codigo: string; mensaje: string }>): Promise<void> => {
    limpiar();
    setRepublicando(true);
    try {
      const respuesta = await accion();
      exito(`Expediente ${respuesta.codigo}: ${respuesta.mensaje}`);
    } catch (fallo) {
      reportarError(mensajeDeError(fallo), detallesDeError(fallo));
    } finally {
      setRepublicando(false);
      expediente.recargar();
    }
  };

  const cargarPdf = async (lista: FileList | null): Promise<void> => {
    const archivo = lista?.[0];
    if (!archivo) {
      return;
    }

    limpiar();
    const errorLocal = validarArchivoPdf(archivo);
    if (errorLocal) {
      reportarError(errorLocal);
      return;
    }

    setCargando(true);
    try {
      const documento = await expedientesApi.subirDocumento(id, archivo);
      exito(`Documento ${documento.nombreArchivo} validado y almacenado en el servidor.`);
    } catch (fallo) {
      reportarError(mensajeDeError(fallo), detallesDeError(fallo));
    } finally {
      setCargando(false);
      if (entrada.current) {
        entrada.current.value = '';
      }
      expediente.recargar();
    }
  };

  const datos = expediente.datos;
  const esBorrador = datos?.estado === 'PENDIENTE_DOCUMENTOS';
  const puedeReintentar =
    datos !== null &&
    datos !== undefined &&
    !datos.eventoPublicado &&
    (datos.estado === 'REGISTRADO_PENDIENTE_PUBLICACION' || datos.estado === 'PUBLICACION_EN_CURSO');

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Expedientes"
        titulo={datos ? `Expediente ${datos.codigo}` : 'Detalle del expediente'}
        descripcion="Estado de recepción, publicación en el bus de eventos y contenido registrado."
        acciones={
          <>
            <Link
              to={RUTAS.expedientes}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Volver a la bandeja
            </Link>
            {esBorrador ? (
              <>
                <input
                  ref={entrada}
                  type="file"
                  accept=".pdf,application/pdf"
                  className="sr-only"
                  onChange={(evento) => void cargarPdf(evento.target.files)}
                />
                <Boton
                  variante="secundario"
                  cargando={cargando}
                  textoCargando="Cargando PDF…"
                  onClick={() => entrada.current?.click()}
                >
                  Cargar PDF
                </Boton>
                <Boton
                  cargando={republicando}
                  textoCargando="Verificando y enviando…"
                  onClick={() => void ejecutar(() => expedientesApi.enviar(id))}
                >
                  Enviar a procesamiento
                </Boton>
              </>
            ) : null}
            {puedeReintentar ? (
              <Boton
                cargando={republicando}
                textoCargando="Republicando…"
                onClick={() => void ejecutar(() => expedientesApi.republicar(id))}
              >
                Reintentar publicación
              </Boton>
            ) : null}
          </>
        }
      />

      {mensaje ? (
        <Alerta tono={mensaje.tipo === 'exito' ? 'exito' : 'error'} detalles={mensaje.detalles}>
          {mensaje.texto}
        </Alerta>
      ) : null}

      {expediente.cargando ? (
        <CargandoBloque texto="Consultando el expediente…" />
      ) : expediente.error ? (
        <Alerta tono="error" titulo="No se pudo consultar el expediente">
          {expediente.error}
        </Alerta>
      ) : datos ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <MetricaResumen
              etiqueta="Estado"
              valor={ETIQUETA_ESTADO[datos.estado] ?? datos.estado}
              tono={datos.estado === 'REGISTRADO_PENDIENTE_PUBLICACION' ? 'error' : 'advertencia'}
            />
            <MetricaResumen
              etiqueta="Publicado en el bus"
              valor={datos.eventoPublicado ? 'Sí' : 'No'}
              tono={datos.eventoPublicado ? 'exito' : 'error'}
              descripcion={`Intentos: ${datos.intentosPublicacion}`}
            />
            <MetricaResumen
              etiqueta="Documentos validados"
              valor={datos.documentos.filter((documento) => documento.estadoRecepcion === 'VALIDADO').length}
              descripcion={`Cargas registradas: ${datos.documentos.length}`}
            />
            <MetricaResumen etiqueta="Materias" valor={datos.asignaturas.length} />
          </div>

          {!datos.eventoPublicado && datos.ultimoErrorPublicacion ? (
            <Alerta tono="advertencia" titulo="Último error de publicación">
              {datos.ultimoErrorPublicacion}
            </Alerta>
          ) : null}

          <Tarjeta titulo="Datos del trámite">
            <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <Dato etiqueta="Identificador" valor={<span className="font-mono text-xs">{datos.id}</span>} />
              <Dato etiqueta="Correlación" valor={<span className="font-mono text-xs">{datos.correlationId}</span>} />
              <Dato
                etiqueta="Tipo de trámite"
                valor={<Etiqueta tono="informacion">{ETIQUETA_TIPO_TRAMITE[datos.tipoTramite] ?? datos.tipoTramite}</Etiqueta>}
              />
              <Dato etiqueta="Ruta normativa" valor={<Etiqueta tono="marca">{datos.rutaNormativa}</Etiqueta>} />
              <Dato etiqueta="Carrera de origen" valor={datos.carreraOrigenCodigo} />
              <Dato etiqueta="Carrera receptora" valor={datos.carreraDestinoCodigo} />
              <Dato etiqueta="Fecha de recepción" valor={formatearFechaHora(datos.fechaCreacionUtc)} />
              <Dato etiqueta="Fecha de publicación" valor={formatearFechaHora(datos.fechaPublicacionUtc)} />
            </dl>
          </Tarjeta>

          <Tarjeta titulo={`Documentos (${datos.documentos.length})`} compacta>
            {datos.documentos.length === 0 ? (
              <p className="p-4 text-sm text-slate-500">El expediente no tiene documentos asociados.</p>
            ) : (
              <div className="desplazable overflow-x-auto">
                <table className="min-w-full text-left text-sm">
                  <caption className="sr-only">Documentos del expediente</caption>
                  <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                    <tr>
                      <th scope="col" className="px-4 py-2.5 font-semibold">Archivo</th>
                      <th scope="col" className="px-4 py-2.5 font-semibold">Estado</th>
                      <th scope="col" className="px-4 py-2.5 font-semibold">Tamaño</th>
                      <th scope="col" className="px-4 py-2.5 font-semibold">SHA-256</th>
                      <th scope="col" className="px-4 py-2.5 font-semibold">Ruta de almacenamiento</th>
                      <th scope="col" className="px-4 py-2.5 font-semibold">Cargado</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {datos.documentos.map((documento) => (
                      <tr key={documento.id}>
                        <td className="px-4 py-2.5 text-slate-700">{documento.nombreArchivo}</td>
                        <td className="px-4 py-2.5">
                          <Etiqueta
                            tono={
                              documento.estadoRecepcion === 'VALIDADO'
                                ? 'exito'
                                : documento.estadoRecepcion === 'RETIRADO'
                                  ? 'neutro'
                                  : 'error'
                            }
                            titulo={documento.observacionValidacion ?? undefined}
                          >
                            {ETIQUETA_ESTADO_DOCUMENTO[documento.estadoRecepcion] ?? documento.estadoRecepcion}
                          </Etiqueta>
                          {documento.observacionValidacion ? (
                            <p className="mt-1 max-w-xs text-xs text-slate-500">{documento.observacionValidacion}</p>
                          ) : null}
                        </td>
                        <td className="px-4 py-2.5 text-slate-600">{formatearBytes(documento.tamanoBytes)}</td>
                        <td className="px-4 py-2.5 font-mono text-xs text-slate-500" title={documento.hashSha256}>
                          {documento.hashSha256 ? abreviarHash(documento.hashSha256, 8) : '—'}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-slate-500">{documento.rutaAlmacenamiento}</td>
                        <td className="px-4 py-2.5 text-xs text-slate-500">{formatearFechaHora(documento.fechaCargaUtc)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Tarjeta>

          <Tarjeta titulo={`Materias declaradas (${datos.asignaturas.length})`} compacta>
            {datos.asignaturas.length === 0 ? (
              <p className="p-4 text-sm text-slate-500">El expediente no tiene materias registradas.</p>
            ) : (
              <div className="desplazable overflow-x-auto">
                <table className="min-w-full text-left text-sm">
                  <caption className="sr-only">Materias declaradas en el expediente</caption>
                  <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                    <tr>
                      <th scope="col" className="px-4 py-2.5 font-semibold">Código de origen</th>
                      <th scope="col" className="px-4 py-2.5 font-semibold">Materia de origen</th>
                      <th scope="col" className="px-4 py-2.5 font-semibold">Asignatura destino</th>
                      <th scope="col" className="px-4 py-2.5 font-semibold">Nota</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {datos.asignaturas.map((materia, indice) => (
                      <tr key={`${materia.codigoOrigen}-${indice}`}>
                        <td className="px-4 py-2.5 font-mono text-xs text-slate-700">{materia.codigoOrigen}</td>
                        <td className="px-4 py-2.5 text-slate-700">{materia.nombreOrigen}</td>
                        <td className="px-4 py-2.5 text-slate-600">
                          {materia.codigoDestino ? (
                            <span className="font-mono text-xs">{materia.codigoDestino}</span>
                          ) : (
                            <Etiqueta tono="advertencia">por evaluar</Etiqueta>
                          )}
                        </td>
                        <td className="px-4 py-2.5 text-slate-600">{materia.nota ?? '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Tarjeta>
        </>
      ) : (
        <Alerta tono="advertencia">El expediente solicitado no existe o no está disponible.</Alerta>
      )}
    </div>
  );
}

function Dato({ etiqueta, valor }: { etiqueta: string; valor: ReactNode }): JSX.Element {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-slate-500">{etiqueta}</dt>
      <dd className="mt-1 text-sm text-slate-800">{valor}</dd>
    </div>
  );
}
