import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ErrorApi, expedientesApi, extraccionesApi, mensajeDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, AreaTexto, Boton, CargandoBloque, Campo, EntradaTexto, Etiqueta, Tarjeta } from '../components/ui';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { Extraccion } from '../types/analisis';
import { ESTADOS_EXTRACCION } from '../types/analisis';
import type { Expediente } from '../types/expedientes';
import {
  aSolicitudConfirmacion,
  borradorDesdeExtraccion,
  formatearDecimal,
  hayErrores,
  nuevaClave,
  validarConfirmacion,
} from '../utils/analisis';
import type { BorradorConfirmacion } from '../utils/analisis';

/** Clase de resaltado para campos que el extractor no encontró o leyó con baja confianza. */
const DUDOSO = 'border-amber-400 bg-[#fffbeb]';

/**
 * Confirmación de datos extraídos (S2-A02 formulario + S2-A03 integración con MS C, mockup M-02).
 * El operador revisa y corrige créditos, horas y unidades. "Confirmar y calcular" envía
 * `PUT /api/extracciones/{id}/confirmar`; el botón permanece bloqueado mientras falte algún dato
 * obligatorio, de modo que el cálculo del agente no avanza hasta confirmar todo.
 */
export function ConfirmarExtraccionPage(): JSX.Element {
  useTituloPagina('Confirmar datos extraídos');
  const { id = '' } = useParams();
  const navegar = useNavigate();
  const caso = usePeticion(async () => {
    const extraccion = await extraccionesApi.obtener(id);
    const expediente = await expedientesApi.obtener(extraccion.expedienteId).catch(() => null);
    return { extraccion, expediente };
  }, [id]);

  if (caso.cargando) {
    return <CargandoBloque />;
  }

  if (caso.error || !caso.datos) {
    return (
      <div className="space-y-4">
        <EncabezadoPagina titulo="Confirmar datos extraídos" />
        <Alerta
          tono="error"
          titulo="No se pudo cargar la extracción"
          accion={
            <Boton variante="secundario" tamano="sm" onClick={() => caso.recargar()}>
              Reintentar
            </Boton>
          }
        >
          {caso.error ?? 'La extracción no existe.'}
        </Alerta>
        <Link to="/procesamiento" className="text-sm font-semibold text-institucional-700 hover:underline">
          Volver a la bandeja
        </Link>
      </div>
    );
  }

  return (
    <FormularioConfirmacion
      extraccion={caso.datos.extraccion}
      fechaAprobacionCertificada={fechaAprobacionDe(caso.datos.expediente, caso.datos.extraccion)}
      alConfirmar={(confirmada) =>
        navegar('/procesamiento', {
          state: {
            aviso: `Datos de ${confirmada.nombreAsignaturaOrigen} confirmados. El agente ya puede calcular créditos y contenidos.`,
          },
        })
      }
    />
  );
}

interface PropsFormulario {
  extraccion: Extraccion;
  fechaAprobacionCertificada?: string | null;
  alConfirmar: (extraccion: Extraccion) => void;
}

export function FormularioConfirmacion({
  extraccion,
  fechaAprobacionCertificada,
  alConfirmar,
}: PropsFormulario): JSX.Element {
  const [borrador, setBorrador] = useState<BorradorConfirmacion>(() => borradorDesdeExtraccion(extraccion));
  const [intentoEnvio, setIntentoEnvio] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [errorServidor, setErrorServidor] = useState<{ texto: string; detalles: string[] } | null>(null);

  useEffect(() => setBorrador(borradorDesdeExtraccion(extraccion)), [extraccion]);

  const errores = useMemo(() => validarConfirmacion(borrador), [borrador]);
  const incompleto = hayErrores(errores);
  const editable = extraccion.estado === ESTADOS_EXTRACCION.PENDIENTE_CONFIRMACION;
  const confianzaBaja = extraccion.confianza < 0.8;
  const mostrar = (mensaje?: string): string | undefined => (intentoEnvio ? mensaje : undefined);

  const actualizarUnidad = (clave: string, campo: 'numero' | 'nombre' | 'horas' | 'contenidos', valor: string): void => {
    setBorrador((previo) => ({
      ...previo,
      unidades: previo.unidades.map((unidad) => (unidad.clave === clave ? { ...unidad, [campo]: valor } : unidad)),
    }));
  };

  const agregarUnidad = (): void => {
    setBorrador((previo) => {
      const siguiente = previo.unidades.reduce((maximo, u) => Math.max(maximo, Number(u.numero) || 0), 0) + 1;
      return {
        ...previo,
        unidades: [...previo.unidades, { clave: nuevaClave(), numero: String(siguiente), nombre: '', horas: '', contenidos: '' }],
      };
    });
  };

  const quitarUnidad = (clave: string): void => {
    setBorrador((previo) => ({ ...previo, unidades: previo.unidades.filter((unidad) => unidad.clave !== clave) }));
  };

  const enviar = async (evento: FormEvent<HTMLFormElement>): Promise<void> => {
    evento.preventDefault();
    setIntentoEnvio(true);
    setErrorServidor(null);
    if (incompleto || !editable) {
      return;
    }

    setEnviando(true);
    try {
      const confirmada = await extraccionesApi.confirmar(extraccion.id, aSolicitudConfirmacion(borrador));
      alConfirmar(confirmada);
    } catch (fallo) {
      const detalles = fallo instanceof ErrorApi ? fallo.detalles : [];
      const texto =
        fallo instanceof ErrorApi && fallo.estado === 409
          ? `${fallo.message} Actualice la bandeja para ver el estado actual.`
          : mensajeDeError(fallo);
      setErrorServidor({ texto, detalles });
    } finally {
      setEnviando(false);
    }
  };

  return (
    <form className="space-y-6" noValidate onSubmit={(evento) => void enviar(evento)}>
      <EncabezadoPagina
        titulo="Confirmar datos extraídos"
        descripcion={`${extraccion.codigoAsignaturaOrigen} · ${extraccion.nombreAsignaturaOrigen}`}
      />

      {!editable ? (
        <Alerta
          tono={
            extraccion.estado === ESTADOS_EXTRACCION.CONFIRMADA
              ? 'exito'
              : extraccion.estado === ESTADOS_EXTRACCION.CANCELADA
                ? 'error'
                : 'informacion'
          }
          titulo={extraccion.estado === ESTADOS_EXTRACCION.CANCELADA ? 'Extracción cancelada' : undefined}
        >
          {extraccion.estado === ESTADOS_EXTRACCION.CONFIRMADA
            ? `Esta extracción ya fue confirmada por ${extraccion.confirmadoPor ?? 'un operador'}.`
            : extraccion.estado === ESTADOS_EXTRACCION.CANCELADA
              ? (extraccion.observacion ??
                'El PDF no tiene capa de texto y el análisis OCR/LLM no está disponible. Cargue un PDF con texto seleccionable.')
              : 'Esta extracción está pendiente de OCR/LLM; podrá confirmarse cuando termine la lectura del PDF.'}
        </Alerta>
      ) : (
        <Alerta tono={confianzaBaja ? 'advertencia' : 'informacion'}>
          <strong>Confianza de extracción: {formatearDecimal(extraccion.confianza)}.</strong> Revise los campos marcados
          antes de continuar. El cálculo no avanza hasta confirmar.
        </Alerta>
      )}

      {errorServidor ? (
        <Alerta tono="error" titulo="El Microservicio C rechazó la confirmación" detalles={errorServidor.detalles}>
          {errorServidor.texto}
        </Alerta>
      ) : null}

      <Tarjeta titulo="Datos de la asignatura de origen" descripcion="Leídos del PDF">
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <Campo htmlFor="nombre-origen" etiqueta="Nombre de la asignatura">
            <EntradaTexto id="nombre-origen" value={extraccion.nombreAsignaturaOrigen} readOnly disabled />
          </Campo>
          <Campo
            htmlFor="creditos"
            etiqueta="Créditos"
            obligatorio
            ayuda={extraccion.creditos === null ? 'No encontrado en el PDF: ingréselo manualmente.' : undefined}
            error={mostrar(errores.creditos)}
          >
            <EntradaTexto
              id="creditos"
              inputMode="numeric"
              value={borrador.creditos}
              disabled={!editable}
              invalido={Boolean(mostrar(errores.creditos))}
              className={extraccion.creditos === null ? DUDOSO : undefined}
              placeholder="— (no encontrado)"
              onChange={(evento) => setBorrador({ ...borrador, creditos: evento.target.value })}
            />
          </Campo>
          <Campo
            htmlFor="horas"
            etiqueta="Horas totales"
            obligatorio
            ayuda={extraccion.horas === null ? 'No encontradas en el PDF: ingréselas manualmente.' : undefined}
            error={mostrar(errores.horas)}
          >
            <EntradaTexto
              id="horas"
              inputMode="numeric"
              value={borrador.horas}
              disabled={!editable}
              invalido={Boolean(mostrar(errores.horas))}
              className={extraccion.horas === null ? DUDOSO : undefined}
              placeholder="— (no encontrado)"
              onChange={(evento) => setBorrador({ ...borrador, horas: evento.target.value })}
            />
          </Campo>
          <Campo htmlFor="fecha-aprobacion" etiqueta="Fecha de aprobación certificada">
            <EntradaTexto
              id="fecha-aprobacion"
              value={fechaAprobacionCertificada ?? ''}
              placeholder="No registrada"
              readOnly
              disabled
            />
          </Campo>
        </div>
      </Tarjeta>

      <Tarjeta
        titulo="Unidades temáticas"
        descripcion={`${borrador.unidades.length} unidad(es) leídas`}
        compacta
        acciones={
          editable ? (
            <Boton type="button" variante="secundario" tamano="sm" onClick={agregarUnidad}>
              Agregar unidad
            </Boton>
          ) : undefined
        }
      >
        {mostrar(errores.unidades) ? (
          <div className="p-4">
            <Alerta tono="error">{errores.unidades}</Alerta>
          </div>
        ) : null}
        <div className="overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <caption className="sr-only">Unidades temáticas extraídas, editables por el operador</caption>
            <thead>
              <tr className="text-xs text-faint">
                <th scope="col" className="w-20 px-4 py-2 font-normal">#</th>
                <th scope="col" className="px-4 py-2 font-normal">Unidad</th>
                <th scope="col" className="w-28 px-4 py-2 font-normal">Horas</th>
                <th scope="col" className="px-4 py-2 font-normal">Contenidos</th>
                <th scope="col" className="w-24 px-4 py-2 font-normal">
                  <span className="sr-only">Acciones</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-stone-100">
              {borrador.unidades.map((unidad) => {
                const error = mostrar(errores.porUnidad[unidad.clave]);
                const sinHoras = !unidad.horas;
                return (
                  <tr key={unidad.clave} className="align-top">
                    <td className="px-4 py-2">
                      <EntradaTexto
                        aria-label="Número de unidad"
                        inputMode="numeric"
                        value={unidad.numero}
                        disabled={!editable}
                        onChange={(evento) => actualizarUnidad(unidad.clave, 'numero', evento.target.value)}
                      />
                    </td>
                    <td className="px-4 py-2">
                      <EntradaTexto
                        aria-label={`Nombre de la unidad ${unidad.numero}`}
                        value={unidad.nombre}
                        disabled={!editable}
                        invalido={Boolean(error)}
                        className={!unidad.nombre.trim() ? DUDOSO : undefined}
                        onChange={(evento) => actualizarUnidad(unidad.clave, 'nombre', evento.target.value)}
                      />
                      {error ? (
                        <p role="alert" className="mt-1 text-xs font-medium text-negative">
                          {error}
                        </p>
                      ) : null}
                    </td>
                    <td className="px-4 py-2">
                      <EntradaTexto
                        aria-label={`Horas de la unidad ${unidad.numero}`}
                        inputMode="numeric"
                        value={unidad.horas}
                        disabled={!editable}
                        className={sinHoras ? DUDOSO : undefined}
                        placeholder="vacío"
                        onChange={(evento) => actualizarUnidad(unidad.clave, 'horas', evento.target.value)}
                      />
                    </td>
                    <td className="px-4 py-2">
                      <AreaTexto
                        aria-label={`Contenidos de la unidad ${unidad.numero}`}
                        rows={2}
                        value={unidad.contenidos}
                        disabled={!editable}
                        onChange={(evento) => actualizarUnidad(unidad.clave, 'contenidos', evento.target.value)}
                      />
                    </td>
                    <td className="px-4 py-2">
                      {editable ? (
                        <Boton
                          type="button"
                          variante="fantasma"
                          tamano="sm"
                          aria-label={`Quitar unidad ${unidad.numero}`}
                          onClick={() => quitarUnidad(unidad.clave)}
                        >
                          Quitar
                        </Boton>
                      ) : null}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </Tarjeta>

      {editable ? (
        <Campo htmlFor="observacion" etiqueta="Observación (opcional)">
          <AreaTexto
            id="observacion"
            rows={2}
            value={borrador.observacion}
            onChange={(evento) => setBorrador({ ...borrador, observacion: evento.target.value })}
          />
        </Campo>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        <Link
          to="/procesamiento"
          className="rounded-md border border-stone-300 bg-white px-4 py-2 text-sm font-medium text-ink hover:bg-stone-50 focus-visible:ring-2 focus-visible:ring-institucional-500"
        >
          Volver
        </Link>
        {editable ? (
          <Boton type="submit" cargando={enviando} textoCargando="Confirmando…" aria-describedby="estado-confirmacion">
            Confirmar y calcular
          </Boton>
        ) : null}
        {editable ? (
          <span id="estado-confirmacion" className="text-xs text-muted">
            {incompleto ? (
              <Etiqueta tono="advertencia">Faltan datos obligatorios: el cálculo sigue bloqueado</Etiqueta>
            ) : (
              <Etiqueta tono="exito">Datos completos</Etiqueta>
            )}
          </span>
        ) : null}
      </div>
    </form>
  );
}

function fechaAprobacionDe(expediente: Expediente | null, extraccion: Extraccion): string | null {
  return expediente?.asignaturas.find((asignatura) =>
    asignatura.codigoOrigen === extraccion.codigoAsignaturaOrigen,
  )?.fechaAprobacionCertificada ?? null;
}
