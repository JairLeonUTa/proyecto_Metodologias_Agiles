import { useRef, useState } from 'react';
import { asignaturasApi, carrerasApi, mallasApi, mensajeDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import {
  Alerta,
  AreaTexto,
  Boton,
  Campo,
  Etiqueta,
  MetricaResumen,
  Selector,
  Tarjeta,
} from '../components/ui';
import { useMensajes } from '../hooks/useMensajes';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { AsignaturaImportacion, ResultadoImportacion, UnidadTematica } from '../types/catalogo';
import { HORAS_POR_CREDITO } from '../types/comunes';

/** Resultado de la validación local previa al envío. */
interface ResumenValidacion {
  total: number;
  validas: number;
  rechazadas: number;
  errores: string[];
  asignaturas: AsignaturaImportacion[];
}

const PLANTILLA: AsignaturaImportacion[] = [
  {
    codigo: 'SW101',
    nombre: 'Fundamentos de Programación',
    creditos: 4,
    horas: 4 * HORAS_POR_CREDITO,
    semestre: 1,
    unidades: [
      { numero: 1, nombre: 'Introducción y entorno', horas: 12, contenidos: 'Estructura de un programa y depuración.' },
      { numero: 2, nombre: 'Estructuras de control', horas: 20, contenidos: 'Condicionales, bucles y funciones.' },
    ],
  },
];

const PLANTILLA_TEXTO = JSON.stringify({ asignaturas: PLANTILLA }, null, 2);

function esObjeto(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor);
}

/**
 * Validación local del JSON del catálogo estructurado.
 * Devuelve las filas válidas y el detalle de los rechazos antes de contactar el servidor.
 */
function validarCatalogo(texto: string): ResumenValidacion {
  const errores: string[] = [];

  let crudo: unknown;
  try {
    crudo = JSON.parse(texto) as unknown;
  } catch (fallo) {
    const detalle = fallo instanceof Error ? fallo.message : 'JSON mal formado.';
    return { total: 0, validas: 0, rechazadas: 0, errores: [`El texto no es un JSON válido: ${detalle}`], asignaturas: [] };
  }

  const lista = Array.isArray(crudo) ? crudo : esObjeto(crudo) && Array.isArray(crudo.asignaturas) ? crudo.asignaturas : null;

  if (!lista) {
    return {
      total: 0,
      validas: 0,
      rechazadas: 0,
      errores: ['La estructura debe ser un arreglo de asignaturas o un objeto con la propiedad "asignaturas".'],
      asignaturas: [],
    };
  }

  const validas: AsignaturaImportacion[] = [];
  const codigosVistos = new Set<string>();

  lista.forEach((elemento, indice) => {
    const posicion = `Fila ${indice + 1}`;

    if (!esObjeto(elemento)) {
      errores.push(`${posicion}: no es un objeto de asignatura.`);
      return;
    }

    const codigo = typeof elemento.codigo === 'string' ? elemento.codigo.trim() : '';
    const nombre = typeof elemento.nombre === 'string' ? elemento.nombre.trim() : '';
    const creditos = Number(elemento.creditos);
    const horas = Number(elemento.horas);
    const semestre = Number(elemento.semestre);

    const fallos: string[] = [];
    if (!codigo) {
      fallos.push('el código es obligatorio');
    } else if (codigo.length > 30) {
      fallos.push('el código supera 30 caracteres');
    } else if (codigosVistos.has(codigo.toUpperCase())) {
      fallos.push(`el código ${codigo} está duplicado en el archivo`);
    }

    if (!nombre) {
      fallos.push('el nombre es obligatorio');
    } else if (nombre.length > 200) {
      fallos.push('el nombre supera 200 caracteres');
    }

    if (!Number.isInteger(creditos) || creditos < 0 || creditos > 30) {
      fallos.push('los créditos deben estar entre 0 y 30');
    }
    if (!Number.isInteger(horas) || horas < 0 || horas > 2000) {
      fallos.push('las horas deben estar entre 0 y 2000');
    }
    if (!Number.isInteger(semestre) || semestre < 1 || semestre > 20) {
      fallos.push('el semestre debe estar entre 1 y 20');
    }

    const unidades: UnidadTematica[] = [];
    if (elemento.unidades !== undefined) {
      if (!Array.isArray(elemento.unidades)) {
        fallos.push('"unidades" debe ser un arreglo');
      } else {
        elemento.unidades.forEach((unidad, indiceUnidad) => {
          if (!esObjeto(unidad)) {
            fallos.push(`la unidad ${indiceUnidad + 1} no es un objeto`);
            return;
          }
          const nombreUnidad = typeof unidad.nombre === 'string' ? unidad.nombre.trim() : '';
          const horasUnidad = Number(unidad.horas);
          const contenidos = typeof unidad.contenidos === 'string' ? unidad.contenidos : '';

          if (!nombreUnidad) {
            fallos.push(`la unidad ${indiceUnidad + 1} requiere nombre`);
          }
          if (!Number.isInteger(horasUnidad) || horasUnidad < 0 || horasUnidad > 1000) {
            fallos.push(`las horas de la unidad ${indiceUnidad + 1} deben estar entre 0 y 1000`);
          }

          unidades.push({
            numero: indiceUnidad + 1,
            nombre: nombreUnidad,
            horas: Number.isFinite(horasUnidad) ? horasUnidad : 0,
            contenidos,
          });
        });
      }
    }

    if (fallos.length > 0) {
      errores.push(`${posicion}${codigo ? ` (${codigo})` : ''}: ${fallos.join('; ')}.`);
      return;
    }

    codigosVistos.add(codigo.toUpperCase());
    validas.push({ codigo, nombre, creditos, horas, semestre, unidades });
  });

  return {
    total: lista.length,
    validas: validas.length,
    rechazadas: lista.length - validas.length,
    errores,
    asignaturas: validas,
  };
}

/**
 * Importación masiva del catálogo estructurado (S1-A05).
 * Presenta un resumen visual de la validación local (importadas, rechazadas y errores)
 * y, tras el envío, el resumen devuelto por el Microservicio A.
 */
export function ImportarCatalogoPage(): JSX.Element {
  useTituloPagina('Importar catálogo');

  const carreras = usePeticion(() => carrerasApi.listar(), []);
  const mallas = usePeticion(() => mallasApi.listar(), []);
  const archivo = useRef<HTMLInputElement>(null);

  const [mallaId, setMallaId] = useState('');
  const [texto, setTexto] = useState(PLANTILLA_TEXTO);
  const [validacion, setValidacion] = useState<ResumenValidacion | null>(null);
  const [resultado, setResultado] = useState<ResultadoImportacion | null>(null);
  const [importando, setImportando] = useState(false);
  const { mensaje, exito, error: reportarError, limpiar } = useMensajes();

  const etiquetaMalla = (carreraId: number, version: string, anio: number): string => {
    const carrera = carreras.datos?.find((item) => item.id === carreraId);
    return `${carrera?.codigo ?? ''} · ${version} (${anio})`.trim();
  };

  const cargarArchivo = async (seleccionado: File | undefined): Promise<void> => {
    if (!seleccionado) {
      return;
    }
    if (!seleccionado.name.toLowerCase().endsWith('.json')) {
      reportarError('El archivo de importación debe tener extensión .json.');
      return;
    }
    limpiar();
    setResultado(null);
    setTexto(await seleccionado.text());
    setValidacion(null);
  };

  const validar = (): void => {
    limpiar();
    setResultado(null);
    const resumen = validarCatalogo(texto);
    setValidacion(resumen);

    if (resumen.total === 0) {
      reportarError('No se encontraron asignaturas para importar.');
    } else if (resumen.rechazadas > 0) {
      reportarError(
        `La validación local rechazó ${resumen.rechazadas} de ${resumen.total} fila(s). Corrija los errores antes de importar.`,
      );
    } else {
      exito(`Validación local correcta: ${resumen.validas} asignatura(s) listas para importar.`);
    }
  };

  const importar = async (): Promise<void> => {
    limpiar();

    if (!mallaId) {
      reportarError('Seleccione la malla curricular destino antes de importar.');
      return;
    }

    const resumen = validacion ?? validarCatalogo(texto);
    setValidacion(resumen);

    if (resumen.validas === 0) {
      reportarError('No hay asignaturas válidas para enviar al servidor.');
      return;
    }

    setImportando(true);
    try {
      const respuesta = await asignaturasApi.importar({ mallaId: Number(mallaId), asignaturas: resumen.asignaturas });
      setResultado(respuesta);
      if (respuesta.rechazadas === 0) {
        exito(`Importación completada: ${respuesta.importadas} asignatura(s) registradas.`);
      } else {
        reportarError(
          `Importación parcial: ${respuesta.importadas} de ${respuesta.total} asignatura(s) registradas; ${respuesta.rechazadas} rechazada(s).`,
          respuesta.errores,
        );
      }
    } catch (fallo) {
      reportarError(mensajeDeError(fallo));
    } finally {
      setImportando(false);
    }
  };

  const descargarPlantilla = (): void => {
    const blob = new Blob([PLANTILLA_TEXTO], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const enlace = document.createElement('a');
    enlace.href = url;
    enlace.download = 'plantilla-catalogo-siha-ia.json';
    enlace.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Catálogo académico"
        titulo="Importar catálogo estructurado"
        descripcion="Carga masiva de planes analíticos (asignaturas y unidades temáticas) desde un archivo JSON validado localmente antes de su envío."
        acciones={
          <>
            <Boton variante="secundario" onClick={descargarPlantilla}>
              Descargar plantilla
            </Boton>
            <Boton
              variante="secundario"
              onClick={() => archivo.current?.click()}
              disabled={importando}
            >
              Cargar archivo JSON
            </Boton>
          </>
        }
      />

      <input
        ref={archivo}
        id="archivo-catalogo"
        type="file"
        accept=".json,application/json"
        className="sr-only"
        onChange={(evento) => void cargarArchivo(evento.target.files?.[0])}
      />

      {mensaje ? (
        <Alerta tono={mensaje.tipo === 'exito' ? 'exito' : 'error'} detalles={mensaje.detalles}>
          {mensaje.texto}
        </Alerta>
      ) : null}

      <Tarjeta titulo="1. Malla destino" descripcion="Las asignaturas se incorporan al catálogo de la malla seleccionada.">
        <Campo
          htmlFor="malla-destino"
          etiqueta="Malla curricular"
          obligatorio
          ayuda="Los códigos duplicados dentro de la misma malla serán rechazados por el servidor."
        >
          <Selector
            id="malla-destino"
            value={mallaId}
            disabled={importando}
            onChange={(evento) => {
              setMallaId(evento.target.value);
              setResultado(null);
            }}
          >
            <option value="">Seleccione una malla…</option>
            {(mallas.datos ?? []).map((malla) => (
              <option key={malla.id} value={malla.id}>
                {etiquetaMalla(malla.carreraId, malla.version, malla.anio)}
              </option>
            ))}
          </Selector>
        </Campo>
      </Tarjeta>

      <Tarjeta
        titulo="2. Contenido del catálogo (JSON)"
        descripcion='Estructura admitida: arreglo de asignaturas o { "asignaturas": [ … ] }.'
        acciones={
          <>
            <Boton variante="secundario" tamano="sm" onClick={validar} disabled={importando}>
              Validar localmente
            </Boton>
            <Boton tamano="sm" onClick={() => void importar()} cargando={importando} textoCargando="Importando…">
              Importar catálogo
            </Boton>
          </>
        }
      >
        <Campo htmlFor="contenido-catalogo" etiqueta="Documento JSON">
          <AreaTexto
            id="contenido-catalogo"
            rows={16}
            value={texto}
            disabled={importando}
            spellCheck={false}
            onChange={(evento) => {
              setTexto(evento.target.value);
              setValidacion(null);
              setResultado(null);
            }}
          />
        </Campo>
      </Tarjeta>

      {validacion ? (
        <Tarjeta
          titulo="3. Resumen de la validación local"
          descripcion="Verificación previa ejecutada en el navegador (estructura, rangos, longitudes y códigos duplicados)."
        >
          <div className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-3">
              <MetricaResumen etiqueta="Filas encontradas" valor={validacion.total} />
              <MetricaResumen etiqueta="Válidas" valor={validacion.validas} tono="exito" descripcion="Listas para importar" />
              <MetricaResumen
                etiqueta="Rechazadas"
                valor={validacion.rechazadas}
                tono={validacion.rechazadas > 0 ? 'error' : 'neutro'}
                descripcion="No se enviarán al servidor"
              />
            </div>

            {validacion.errores.length > 0 ? (
              <div>
                <p className="mb-1 text-sm font-semibold text-slate-800">Errores detectados ({validacion.errores.length})</p>
                <ul className="desplazable max-h-56 space-y-1 overflow-y-auto rounded-md border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700">
                  {validacion.errores.map((error, indice) => (
                    <li key={`${indice}-${error}`}>• {error}</li>
                  ))}
                </ul>
              </div>
            ) : (
              <Alerta tono="exito">Sin errores de validación local. Puede proceder con la importación.</Alerta>
            )}

            {validacion.asignaturas.length > 0 ? (
              <div className="desplazable max-h-64 overflow-y-auto rounded-md border border-slate-200">
                <table className="min-w-full text-left text-sm">
                  <caption className="sr-only">Asignaturas válidas detectadas en el archivo</caption>
                  <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                    <tr>
                      <th scope="col" className="px-3 py-2 font-semibold">Código</th>
                      <th scope="col" className="px-3 py-2 font-semibold">Asignatura</th>
                      <th scope="col" className="px-3 py-2 font-semibold">Créditos</th>
                      <th scope="col" className="px-3 py-2 font-semibold">Horas</th>
                      <th scope="col" className="px-3 py-2 font-semibold">Semestre</th>
                      <th scope="col" className="px-3 py-2 font-semibold">Unidades</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {validacion.asignaturas.map((asignatura) => (
                      <tr key={asignatura.codigo}>
                        <td className="px-3 py-2 font-mono text-xs text-slate-700">{asignatura.codigo}</td>
                        <td className="px-3 py-2 text-slate-700">{asignatura.nombre}</td>
                        <td className="px-3 py-2 text-slate-600">{asignatura.creditos}</td>
                        <td className="px-3 py-2 text-slate-600">{asignatura.horas}</td>
                        <td className="px-3 py-2 text-slate-600">{asignatura.semestre}</td>
                        <td className="px-3 py-2 text-slate-600">
                          {asignatura.unidades.length > 0 ? (
                            <Etiqueta tono="exito">{asignatura.unidades.length}</Etiqueta>
                          ) : (
                            <Etiqueta tono="advertencia">plan incompleto</Etiqueta>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : null}
          </div>
        </Tarjeta>
      ) : null}

      {resultado ? (
        <Tarjeta titulo="4. Resultado de la importación" descripcion="Respuesta del Microservicio A (catálogo académico).">
          <div className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-3">
              <MetricaResumen etiqueta="Total enviado" valor={resultado.total} />
              <MetricaResumen etiqueta="Importadas" valor={resultado.importadas} tono="exito" />
              <MetricaResumen
                etiqueta="Rechazadas"
                valor={resultado.rechazadas}
                tono={resultado.rechazadas > 0 ? 'error' : 'neutro'}
              />
            </div>

            {resultado.errores.length > 0 ? (
              <ul className="desplazable max-h-56 space-y-1 overflow-y-auto rounded-md border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700">
                {resultado.errores.map((error, indice) => (
                  <li key={`${indice}-${error}`}>• {error}</li>
                ))}
              </ul>
            ) : (
              <Alerta tono="exito">El servidor aceptó la totalidad de las asignaturas enviadas.</Alerta>
            )}
          </div>
        </Tarjeta>
      ) : null}
    </div>
  );
}
