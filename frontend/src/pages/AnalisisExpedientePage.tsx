import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { asignaturasApi, expedientesApi, extraccionesApi, mensajeDeError, detallesDeError, reglasApi } from '../api';
import { ConsultaPrecedentes } from '../components/analisis/ConsultaPrecedentes';
import { PanelCreditos } from '../components/analisis/PanelCreditos';
import { Pestanas } from '../components/analisis/Pestanas';
import { TablaCobertura } from '../components/analisis/TablaCobertura';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Boton, CargandoBloque, Campo, Casilla, EntradaTexto, Selector, Tarjeta } from '../components/ui';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { AnalisisCompleto, Extraccion, Precedente } from '../types/analisis';
import { ESTADO_FINAL, ESTADOS_EXTRACCION } from '../types/analisis';
import type { Asignatura } from '../types/catalogo';
import { RUTAS_NORMATIVAS_TODAS } from '../types/comunes';
import type { Expediente } from '../types/expedientes';
import type { ParametroEvaluacion } from '../types/reglas';
import { contextoExpediente, presentarEstadoFinal } from '../utils/analisis';

/** Vistas del resultado: coinciden con los mockups M-03, M-05 y M-06. */
const VISTAS = ['creditos', 'cobertura', 'precedentes'] as const;
type Vista = (typeof VISTAS)[number];

interface Parametros {
  codigoDestino: string;
  rutaNormativa: string;
  institucionOrigen: string;
  anosTranscurridos: string;
  numeroMatricula: string;
  esCreditoExterno: boolean;
}

interface Resultado {
  analisis: AnalisisCompleto;
  asignaturaDestino: Asignatura;
  parametrosReglas: ParametroEvaluacion | null;
  /** `null` si la consulta de precedentes falló: aviso no bloqueante (S2-A09). */
  precedentes: Precedente[] | null;
  errorPrecedentes: string | null;
  datosControl: Parametros;
}

/** Carga la extracción y, si el rol lo permite, su expediente de MS B. */
async function cargarCaso(id: string): Promise<{ extraccion: Extraccion; expediente: Expediente | null }> {
  const extraccion = await extraccionesApi.obtener(id);
  const expediente = await expedientesApi.obtener(extraccion.expedienteId).catch(() => null);
  return { extraccion, expediente };
}

/**
 * Análisis del expediente por el docente (S2-A08: integración de las vistas del Sprint 2 con MS C real).
 * Ejecuta `POST /api/extracciones/{id}/analizar` (gRPC reglas → Pcred → Pcont → precedentes) y muestra
 * el resultado en tres pestañas: créditos y controles (S2-A04), cobertura (S2-A06) y precedentes (S2-A07).
 */
export function AnalisisExpedientePage(): JSX.Element {
  useTituloPagina('Análisis del expediente');
  const { id = '' } = useParams();
  const [busqueda, setBusqueda] = useSearchParams();
  const vista: Vista = VISTAS.includes(busqueda.get('vista') as Vista) ? (busqueda.get('vista') as Vista) : 'creditos';
  const caso = usePeticion(() => cargarCaso(id), [id]);

  const [parametros, setParametros] = useState<Parametros>({
    codigoDestino: '',
    rutaNormativa: RUTAS_NORMATIVAS_TODAS[0],
    institucionOrigen: 'Universidad Técnica de Ambato',
    anosTranscurridos: '1',
    numeroMatricula: '1',
    esCreditoExterno: false,
  });
  const [analizando, setAnalizando] = useState(false);
  const [error, setError] = useState<{ texto: string; detalles: string[] } | null>(null);
  const [resultado, setResultado] = useState<Resultado | null>(null);

  // Prellena con los datos del expediente (MS B) cuando están disponibles.
  useEffect(() => {
    if (!caso.datos) return;
    const contexto = contextoExpediente(caso.datos.extraccion, caso.datos.expediente);
    const externo = contexto.tipoTramite === 'EXTERNO';
    setParametros((previo) => ({
      ...previo,
      codigoDestino: contexto.codigoDestino ?? previo.codigoDestino,
      rutaNormativa: contexto.rutaNormativa ?? previo.rutaNormativa,
      esCreditoExterno: externo,
      institucionOrigen: externo ? '' : previo.institucionOrigen,
    }));
  }, [caso.datos]);

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

  const { extraccion, expediente } = caso.datos;
  const contexto = contextoExpediente(extraccion, expediente);
  const confirmada = extraccion.estado === ESTADOS_EXTRACCION.CONFIRMADA;
  const anos = Number(parametros.anosTranscurridos);
  const matricula = Number(parametros.numeroMatricula);
  const formularioValido =
    parametros.codigoDestino.trim() !== '' &&
    parametros.institucionOrigen.trim() !== '' &&
    Number.isInteger(anos) &&
    anos >= 0 &&
    Number.isInteger(matricula) &&
    matricula >= 1;

  const analizar = async (evento: FormEvent<HTMLFormElement>): Promise<void> => {
    evento.preventDefault();
    setError(null);
    if (!formularioValido || !confirmada) return;

    setAnalizando(true);
    try {
      const codigo = parametros.codigoDestino.trim().toUpperCase();
      const asignaturas = await asignaturasApi.listar(undefined, codigo);
      const destino = asignaturas.find((a) => a.codigo.toUpperCase() === codigo) ?? asignaturas[0];
      if (!destino) {
        throw new Error(`La asignatura de destino ${codigo} no existe en el catálogo.`);
      }
      if (destino.unidades.length === 0) {
        throw new Error(`La asignatura ${destino.codigo} no tiene unidades temáticas con horas en el catálogo.`);
      }

      const [analisis, reglas, precedentes] = await Promise.allSettled([
        extraccionesApi.analizar(extraccion.id, {
          anosTranscurridos: anos,
          numeroMatriculaDestino: matricula,
          esCreditoExterno: parametros.esCreditoExterno,
          rutaNormativa: parametros.rutaNormativa,
          codigoAsignaturaDestino: destino.codigo,
          institucionOrigen: parametros.institucionOrigen.trim(),
          creditosDestino: destino.creditos,
          unidadesDestino: destino.unidades.map((u) => ({ ...u, contenidos: u.contenidos ?? '', embedding: [] })),
          validarRN19: true,
        }),
        reglasApi.obtenerParametros(parametros.rutaNormativa),
        extraccionesApi.buscarPrecedentes({
          codigosAsignaturasOrigen: [extraccion.codigoAsignaturaOrigen],
          codigoAsignaturaDestino: destino.codigo,
          institucionOrigen: parametros.institucionOrigen.trim(),
        }),
      ]);

      if (analisis.status === 'rejected') throw analisis.reason;

      setResultado({
        analisis: analisis.value,
        asignaturaDestino: destino,
        parametrosReglas: reglas.status === 'fulfilled' ? reglas.value : null,
        precedentes: precedentes.status === 'fulfilled' ? precedentes.value : null,
        errorPrecedentes: precedentes.status === 'rejected' ? mensajeDeError(precedentes.reason) : null,
        datosControl: parametros,
      });
    } catch (fallo) {
      setResultado(null);
      setError({ texto: mensajeDeError(fallo), detalles: detallesDeError(fallo) });
    } finally {
      setAnalizando(false);
    }
  };

  const final = resultado ? presentarEstadoFinal(resultado.analisis.estadoFinal) : null;
  const sinCalculo =
    resultado?.analisis.estadoFinal === ESTADO_FINAL.ERROR_REGLAS_GRPC ||
    resultado?.analisis.estadoFinal === ESTADO_FINAL.EXTRACCION_NO_CONFIRMADA;

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        titulo="Análisis del expediente"
        descripcion={[contexto.codigo, extraccion.nombreAsignaturaOrigen, contexto.rutaAcademica, contexto.rutaNormativa]
          .filter(Boolean)
          .join(' · ')}
        acciones={
          <Link to="/evaluacion" className="text-sm font-semibold text-institucional-700 hover:underline">
            Volver a la bandeja
          </Link>
        }
      />

      {!confirmada ? (
        <Alerta tono="advertencia" titulo="Datos sin confirmar">
          El operador aún no confirma los datos extraídos. El cálculo permanece bloqueado hasta entonces.
        </Alerta>
      ) : null}

      <Tarjeta titulo="Parámetros del caso" descripcion="Se combinan con las reglas vigentes que MS C consulta por gRPC.">
        <form className="space-y-4" noValidate onSubmit={(evento) => void analizar(evento)}>
          <div className="grid gap-4 md:grid-cols-3">
            <Campo htmlFor="codigo-destino" etiqueta="Asignatura de destino (código)" obligatorio>
              <EntradaTexto
                id="codigo-destino"
                value={parametros.codigoDestino}
                placeholder="SW101"
                onChange={(e) => setParametros({ ...parametros, codigoDestino: e.target.value })}
              />
            </Campo>
            <Campo htmlFor="ruta-normativa" etiqueta="Ruta normativa" obligatorio>
              <Selector
                id="ruta-normativa"
                value={parametros.rutaNormativa}
                onChange={(e) => setParametros({ ...parametros, rutaNormativa: e.target.value })}
              >
                {RUTAS_NORMATIVAS_TODAS.map((ruta) => (
                  <option key={ruta} value={ruta}>
                    {ruta}
                  </option>
                ))}
              </Selector>
            </Campo>
            <Campo htmlFor="institucion-origen" etiqueta="Institución de origen" obligatorio>
              <EntradaTexto
                id="institucion-origen"
                value={parametros.institucionOrigen}
                onChange={(e) => setParametros({ ...parametros, institucionOrigen: e.target.value })}
              />
            </Campo>
            <Campo htmlFor="anos" etiqueta="Años desde que aprobó la asignatura" obligatorio>
              <EntradaTexto
                id="anos"
                type="number"
                min={0}
                value={parametros.anosTranscurridos}
                onChange={(e) => setParametros({ ...parametros, anosTranscurridos: e.target.value })}
              />
            </Campo>
            <Campo htmlFor="matricula" etiqueta="Matrícula en la malla destino" obligatorio>
              <Selector
                id="matricula"
                value={parametros.numeroMatricula}
                onChange={(e) => setParametros({ ...parametros, numeroMatricula: e.target.value })}
              >
                <option value="1">Primera</option>
                <option value="2">Segunda</option>
                <option value="3">Tercera</option>
              </Selector>
            </Campo>
            <div className="flex items-end pb-2">
              <Casilla
                id="credito-externo"
                etiqueta="Créditos de otra institución"
                ayuda="No se aplica 48 h/crédito automático (RN-15)."
                checked={parametros.esCreditoExterno}
                onChange={(e) => setParametros({ ...parametros, esCreditoExterno: e.target.checked })}
              />
            </div>
          </div>
          <Boton type="submit" cargando={analizando} textoCargando="El agente está analizando…" disabled={!confirmada || !formularioValido}>
            Ejecutar análisis
          </Boton>
        </form>
      </Tarjeta>

      {error ? (
        <Alerta tono="error" titulo="El análisis no pudo completarse" detalles={error.detalles}>
          {error.texto}
        </Alerta>
      ) : null}

      {resultado && final ? (
        <section aria-label="Resultado del análisis" className="space-y-4">
          <Alerta tono={final.tono} titulo={final.etiqueta} detalles={resultado.analisis.alertasGlobales}>
            {final.descripcion}
          </Alerta>

          {sinCalculo ? null : (
            <Pestanas
              etiqueta="Resultado del análisis"
              activa={vista}
              alCambiar={(nueva) => setBusqueda({ vista: nueva }, { replace: true })}
              pestanas={[
                {
                  id: 'creditos',
                  etiqueta: 'Créditos y controles',
                  contenido: (
                    <PanelCreditos
                      creditos={resultado.analisis.creditos}
                      parametros={resultado.parametrosReglas}
                      datos={{
                        asignaturaOrigen: extraccion.nombreAsignaturaOrigen,
                        asignaturaDestino: `${resultado.asignaturaDestino.nombre} (${resultado.asignaturaDestino.codigo})`,
                        creditosOrigen: extraccion.creditos,
                        creditosDestino: resultado.asignaturaDestino.creditos,
                        anosTranscurridos: Number(resultado.datosControl.anosTranscurridos),
                        numeroMatricula: Number(resultado.datosControl.numeroMatricula),
                        esCreditoExterno: resultado.datosControl.esCreditoExterno,
                      }}
                    />
                  ),
                },
                {
                  id: 'cobertura',
                  etiqueta: 'Cobertura de contenidos',
                  contenido: (
                    <TablaCobertura
                      contenido={resultado.analisis.contenido}
                      unidadesOrigen={extraccion.unidades}
                      versionReglas={resultado.parametrosReglas?.version ?? null}
                    />
                  ),
                },
                {
                  id: 'precedentes',
                  etiqueta: 'Precedentes',
                  contenido: (
                    <ConsultaPrecedentes
                      titulo={`${contexto.codigo} · ${extraccion.nombreAsignaturaOrigen} → ${resultado.asignaturaDestino.nombre} (${resultado.asignaturaDestino.codigo})`}
                      precedentes={resultado.precedentes ?? (resultado.errorPrecedentes ? null : resultado.analisis.precedentes)}
                      error={resultado.errorPrecedentes}
                    />
                  ),
                },
              ]}
            />
          )}
        </section>
      ) : null}
    </div>
  );
}
