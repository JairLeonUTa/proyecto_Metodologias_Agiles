import { Boton, Campo, EntradaTexto, AreaTexto } from './ui';
import type { UnidadTematica } from '../types/catalogo';

interface PropsEditorUnidades {
  unidades: UnidadTematica[];
  onCambiar: (unidades: UnidadTematica[]) => void;
  deshabilitado?: boolean;
}

const UNIDAD_VACIA: UnidadTematica = { numero: 1, nombre: '', horas: 0, contenidos: '' };

/** Renumera secuencialmente las unidades temáticas. */
function renumerar(unidades: UnidadTematica[]): UnidadTematica[] {
  return unidades.map((unidad, indice) => ({ ...unidad, numero: indice + 1 }));
}

/**
 * Editor de unidades temáticas de un plan analítico (S1-A05).
 * Permite agregar, editar, reordenar y eliminar unidades manteniendo la numeración.
 */
export function EditorUnidades({ unidades, onCambiar, deshabilitado = false }: PropsEditorUnidades): JSX.Element {
  const actualizar = (indice: number, cambios: Partial<UnidadTematica>): void => {
    onCambiar(unidades.map((unidad, posicion) => (posicion === indice ? { ...unidad, ...cambios } : unidad)));
  };

  const agregar = (): void => {
    onCambiar(renumerar([...unidades, { ...UNIDAD_VACIA, numero: unidades.length + 1 }]));
  };

  const eliminar = (indice: number): void => {
    onCambiar(renumerar(unidades.filter((_, posicion) => posicion !== indice)));
  };

  const mover = (indice: number, direccion: -1 | 1): void => {
    const destino = indice + direccion;
    if (destino < 0 || destino >= unidades.length) {
      return;
    }
    const copia = [...unidades];
    const [movida] = copia.splice(indice, 1);
    copia.splice(destino, 0, movida);
    onCambiar(renumerar(copia));
  };

  const totalHoras = unidades.reduce((suma, unidad) => suma + (Number(unidad.horas) || 0), 0);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-slate-500">
          {unidades.length} unidad(es) temática(s) · {totalHoras} horas registradas
        </p>
        <Boton variante="secundario" tamano="sm" onClick={agregar} disabled={deshabilitado || unidades.length >= 50}>
          Agregar unidad
        </Boton>
      </div>

      {unidades.length === 0 ? (
        <p className="rounded-md border border-dashed border-slate-300 bg-slate-50 px-3 py-4 text-center text-xs text-slate-500">
          La asignatura no tiene unidades temáticas. Agregue al menos una para completar el plan analítico.
        </p>
      ) : null}

      <ol className="space-y-3">
        {unidades.map((unidad, indice) => {
          const idNombre = `unidad-${indice}-nombre`;
          const idHoras = `unidad-${indice}-horas`;
          const idContenidos = `unidad-${indice}-contenidos`;

          return (
            <li key={indice} className="rounded-md border border-slate-200 bg-slate-50/60 p-3">
              <div className="mb-2 flex items-center justify-between gap-2">
                <p className="text-sm font-semibold text-slate-700">Unidad {unidad.numero}</p>
                <div className="flex items-center gap-1">
                  <Boton
                    variante="fantasma"
                    tamano="sm"
                    onClick={() => mover(indice, -1)}
                    disabled={deshabilitado || indice === 0}
                    aria-label={`Mover la unidad ${unidad.numero} hacia arriba`}
                  >
                    ↑
                  </Boton>
                  <Boton
                    variante="fantasma"
                    tamano="sm"
                    onClick={() => mover(indice, 1)}
                    disabled={deshabilitado || indice === unidades.length - 1}
                    aria-label={`Mover la unidad ${unidad.numero} hacia abajo`}
                  >
                    ↓
                  </Boton>
                  <Boton
                    variante="fantasma"
                    tamano="sm"
                    className="text-rose-600 hover:bg-rose-50"
                    onClick={() => eliminar(indice)}
                    disabled={deshabilitado}
                    aria-label={`Eliminar la unidad ${unidad.numero}`}
                  >
                    Eliminar
                  </Boton>
                </div>
              </div>

              <div className="grid gap-3 md:grid-cols-[2fr_1fr]">
                <Campo htmlFor={idNombre} etiqueta="Nombre de la unidad" obligatorio>
                  <EntradaTexto
                    id={idNombre}
                    value={unidad.nombre}
                    maxLength={200}
                    disabled={deshabilitado}
                    placeholder="Ej.: Estructuras de control"
                    onChange={(evento) => actualizar(indice, { nombre: evento.target.value })}
                  />
                </Campo>

                <Campo htmlFor={idHoras} etiqueta="Horas" obligatorio ayuda="Entre 0 y 1000 horas.">
                  <EntradaTexto
                    id={idHoras}
                    type="number"
                    min={0}
                    max={1000}
                    value={unidad.horas}
                    disabled={deshabilitado}
                    onChange={(evento) => actualizar(indice, { horas: Number(evento.target.value) })}
                  />
                </Campo>
              </div>

              <Campo htmlFor={idContenidos} etiqueta="Contenidos" className="mt-3">
                <AreaTexto
                  id={idContenidos}
                  rows={3}
                  maxLength={4000}
                  value={unidad.contenidos}
                  disabled={deshabilitado}
                  placeholder="Temas y subtemas que cubre la unidad."
                  onChange={(evento) => actualizar(indice, { contenidos: evento.target.value })}
                />
              </Campo>
            </li>
          );
        })}
      </ol>
    </div>
  );
}
