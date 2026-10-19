import { useState } from 'react';
import type { FormEvent } from 'react';
import { carrerasApi, mallasApi, mensajeDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import {
  Alerta,
  Boton,
  Campo,
  Casilla,
  EntradaTexto,
  Etiqueta,
  Modal,
  Selector,
  Tabla,
  Tarjeta,
} from '../components/ui';
import type { ColumnaTabla } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { useMensajes } from '../hooks/useMensajes';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { Malla } from '../types/catalogo';
import { ROLES, RUTAS_NORMATIVAS_TODAS } from '../types/comunes';

interface FormularioMalla {
  carreraId: string;
  version: string;
  anio: string;
  fuente: string;
  rutaNormativa: string;
  vigente: boolean;
}

const ANIO_ACTUAL = new Date().getFullYear();

const FORMULARIO_VACIO: FormularioMalla = {
  carreraId: '',
  version: '',
  anio: String(ANIO_ACTUAL),
  fuente: '',
  rutaNormativa: '',
  vigente: true,
};

/**
 * Listado y registro de versiones de mallas curriculares (S1-A04).
 * Permite filtrar las mallas por carrera y registrar nuevas versiones.
 */
export function MallasPage(): JSX.Element {
  useTituloPagina('Mallas curriculares');

  const { tieneRol } = useAuth();
  const esAdministrador = tieneRol(ROLES.ADMINISTRADOR);

  const [filtroCarrera, setFiltroCarrera] = useState('');
  const carreras = usePeticion(() => carrerasApi.listar(), []);
  const mallas = usePeticion(
    () => mallasApi.listar(filtroCarrera ? Number(filtroCarrera) : undefined),
    [filtroCarrera],
  );

  const { mensaje, exito, error: reportarError, limpiar } = useMensajes();
  const [modalAbierto, setModalAbierto] = useState(false);
  const [formulario, setFormulario] = useState<FormularioMalla>(FORMULARIO_VACIO);
  const [errores, setErrores] = useState<{ carreraId?: string; version?: string; anio?: string; fuente?: string }>({});
  const [guardando, setGuardando] = useState(false);

  const abrirCreacion = (): void => {
    setFormulario({ ...FORMULARIO_VACIO, carreraId: filtroCarrera });
    setErrores({});
    limpiar();
    setModalAbierto(true);
  };

  const guardar = async (evento: FormEvent<HTMLFormElement>): Promise<void> => {
    evento.preventDefault();
    limpiar();

    const nuevos: typeof errores = {};
    const anio = Number(formulario.anio);

    if (!formulario.carreraId) {
      nuevos.carreraId = 'Seleccione la carrera a la que pertenece la malla.';
    }
    if (!formulario.version.trim()) {
      nuevos.version = 'La versión de la malla es obligatoria (por ejemplo, 2024-A).';
    }
    if (!Number.isInteger(anio) || anio < 1990 || anio > 2100) {
      nuevos.anio = 'El año debe estar entre 1990 y 2100.';
    }
    if (!formulario.fuente.trim()) {
      nuevos.fuente = 'Indique la fuente o resolución que respalda la malla.';
    }

    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) {
      return;
    }

    setGuardando(true);
    try {
      await mallasApi.crear({
        carreraId: Number(formulario.carreraId),
        version: formulario.version.trim(),
        anio,
        fuente: formulario.fuente.trim(),
        rutaNormativa: formulario.rutaNormativa || null,
        vigente: formulario.vigente,
      });
      exito(`Malla ${formulario.version.trim()} registrada correctamente.`);
      setModalAbierto(false);
      mallas.recargar();
    } catch (fallo) {
      reportarError(mensajeDeError(fallo));
    } finally {
      setGuardando(false);
    }
  };

  const nombreCarrera = (carreraId: number): string => {
    const carrera = carreras.datos?.find((item) => item.id === carreraId);
    return carrera ? `${carrera.codigo} — ${carrera.nombre}` : `Carrera ${carreraId}`;
  };

  const columnas: Array<ColumnaTabla<Malla>> = [
    {
      clave: 'carrera',
      encabezado: 'Carrera',
      render: (malla) => (
        <div>
          <p className="font-medium text-slate-800">{malla.carreraCodigo}</p>
          <p className="text-xs text-slate-500">{nombreCarrera(malla.carreraId)}</p>
        </div>
      ),
    },
    {
      clave: 'version',
      encabezado: 'Versión',
      render: (malla) => <span className="font-mono text-xs font-semibold text-slate-800">{malla.version}</span>,
    },
    { clave: 'anio', encabezado: 'Año', render: (malla) => <span className="text-slate-600">{malla.anio}</span> },
    {
      clave: 'fuente',
      encabezado: 'Fuente',
      className: 'max-w-xs',
      render: (malla) => <span className="text-xs text-slate-600">{malla.fuente}</span>,
    },
    {
      clave: 'ruta',
      encabezado: 'Ruta normativa',
      render: (malla) =>
        malla.rutaNormativa ? <Etiqueta tono="marca">{malla.rutaNormativa}</Etiqueta> : <span className="text-xs text-slate-400">—</span>,
    },
    {
      clave: 'asignaturas',
      encabezado: 'Asignaturas',
      render: (malla) => <span className="text-slate-600">{malla.totalAsignaturas}</span>,
    },
    {
      clave: 'vigente',
      encabezado: 'Vigencia',
      render: (malla) => <Etiqueta tono={malla.vigente ? 'exito' : 'neutro'}>{malla.vigente ? 'Vigente' : 'Histórica'}</Etiqueta>,
    },
  ];

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Catálogo académico"
        titulo="Mallas curriculares"
        descripcion="Versiones de malla por carrera. Cada versión agrupa las asignaturas y sus planes analíticos."
        acciones={
          esAdministrador ? <Boton onClick={abrirCreacion}>Registrar malla</Boton> : <Etiqueta tono="informacion">Solo lectura</Etiqueta>
        }
      />

      {mensaje ? (
        <Alerta tono={mensaje.tipo === 'exito' ? 'exito' : 'error'} detalles={mensaje.detalles}>
          {mensaje.texto}
        </Alerta>
      ) : null}

      <Tarjeta
        titulo="Versiones registradas"
        descripcion={`${mallas.datos?.length ?? 0} versión(es) de malla.`}
        compacta
        acciones={
          <div className="flex flex-wrap items-end gap-2">
            <div className="w-64">
              <Campo htmlFor="filtro-carrera" etiqueta="Filtrar por carrera">
                <Selector id="filtro-carrera" value={filtroCarrera} onChange={(evento) => setFiltroCarrera(evento.target.value)}>
                  <option value="">Todas las carreras</option>
                  {(carreras.datos ?? []).map((carrera) => (
                    <option key={carrera.id} value={carrera.id}>
                      {carrera.codigo} — {carrera.nombre}
                    </option>
                  ))}
                </Selector>
              </Campo>
            </div>
            <Boton variante="secundario" tamano="sm" onClick={() => mallas.recargar()}>
              Actualizar
            </Boton>
          </div>
        }
      >
        {mallas.error ? (
          <div className="p-4">
            <Alerta tono="error" titulo="No se pudieron cargar las mallas">
              {mallas.error}
            </Alerta>
          </div>
        ) : (
          <Tabla
            columnas={columnas}
            filas={mallas.datos ?? []}
            claveFila={(malla) => malla.id}
            cargando={mallas.cargando}
            mensajeVacio="No hay mallas curriculares registradas para el filtro seleccionado."
          />
        )}
      </Tarjeta>

      <Modal
        abierto={modalAbierto}
        titulo="Registrar versión de malla"
        descripcion="Una carrera no puede tener dos mallas con la misma versión."
        onCerrar={() => setModalAbierto(false)}
        acciones={
          <>
            <Boton variante="secundario" onClick={() => setModalAbierto(false)} disabled={guardando}>
              Cancelar
            </Boton>
            <Boton type="submit" form="formulario-malla" cargando={guardando} textoCargando="Guardando…">
              Registrar
            </Boton>
          </>
        }
      >
        <form id="formulario-malla" onSubmit={(evento) => void guardar(evento)} className="space-y-4" noValidate>
          {mensaje && mensaje.tipo === 'error' ? <Alerta tono="error" detalles={mensaje.detalles}>{mensaje.texto}</Alerta> : null}

          <Campo htmlFor="malla-carrera" etiqueta="Carrera" obligatorio error={errores.carreraId}>
            <Selector
              id="malla-carrera"
              value={formulario.carreraId}
              invalido={Boolean(errores.carreraId)}
              onChange={(evento) => setFormulario({ ...formulario, carreraId: evento.target.value })}
            >
              <option value="">Seleccione una carrera…</option>
              {(carreras.datos ?? []).map((carrera) => (
                <option key={carrera.id} value={carrera.id}>
                  {carrera.codigo} — {carrera.nombre}
                </option>
              ))}
            </Selector>
          </Campo>

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo htmlFor="malla-version" etiqueta="Versión" obligatorio ayuda="Ejemplo: 2024-A." error={errores.version}>
              <EntradaTexto
                id="malla-version"
                value={formulario.version}
                maxLength={20}
                invalido={Boolean(errores.version)}
                onChange={(evento) => setFormulario({ ...formulario, version: evento.target.value })}
              />
            </Campo>

            <Campo htmlFor="malla-anio" etiqueta="Año" obligatorio error={errores.anio}>
              <EntradaTexto
                id="malla-anio"
                type="number"
                min={1990}
                max={2100}
                value={formulario.anio}
                invalido={Boolean(errores.anio)}
                onChange={(evento) => setFormulario({ ...formulario, anio: evento.target.value })}
              />
            </Campo>
          </div>

          <Campo htmlFor="malla-fuente" etiqueta="Fuente" obligatorio ayuda="Resolución o proceso que respalda la malla." error={errores.fuente}>
            <EntradaTexto
              id="malla-fuente"
              value={formulario.fuente}
              maxLength={200}
              invalido={Boolean(errores.fuente)}
              onChange={(evento) => setFormulario({ ...formulario, fuente: evento.target.value })}
            />
          </Campo>

          <Campo htmlFor="malla-ruta" etiqueta="Ruta normativa asociada" ayuda="Opcional. Vincula la malla con la resolución CAU correspondiente.">
            <Selector
              id="malla-ruta"
              value={formulario.rutaNormativa}
              onChange={(evento) => setFormulario({ ...formulario, rutaNormativa: evento.target.value })}
            >
              <option value="">Sin ruta asociada</option>
              {RUTAS_NORMATIVAS_TODAS.map((ruta) => (
                <option key={ruta} value={ruta}>
                  {ruta}
                </option>
              ))}
            </Selector>
          </Campo>

          <Casilla
            id="malla-vigente"
            etiqueta="Malla vigente"
            ayuda="Marque esta opción si la malla se encuentra en aplicación."
            checked={formulario.vigente}
            onChange={(evento) => setFormulario({ ...formulario, vigente: evento.target.checked })}
          />
        </form>
      </Modal>
    </div>
  );
}
