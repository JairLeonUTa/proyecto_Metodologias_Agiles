import { useState } from 'react';
import type { FormEvent } from 'react';
import { asignaturasApi, carrerasApi, mallasApi, mensajeDeError } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { EditorUnidades } from '../components/EditorUnidades';
import {
  Alerta,
  Boton,
  Campo,
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
import type { Asignatura, UnidadTematica } from '../types/catalogo';
import { HORAS_POR_CREDITO, ROLES } from '../types/comunes';
import { validarHoras } from '../utils/validaciones';

interface FormularioAsignatura {
  mallaId: string;
  codigo: string;
  nombre: string;
  creditos: string;
  horas: string;
  semestre: string;
  unidades: UnidadTematica[];
}

const FORMULARIO_VACIO: FormularioAsignatura = {
  mallaId: '',
  codigo: '',
  nombre: '',
  creditos: '4',
  horas: String(4 * HORAS_POR_CREDITO),
  semestre: '1',
  unidades: [],
};

/**
 * Gestión de asignaturas y unidades temáticas (S1-A05).
 * Permite filtrar por malla o código, consultar el plan analítico completo y
 * registrar o actualizar asignaturas con sus unidades.
 */
export function AsignaturasPage(): JSX.Element {
  useTituloPagina('Asignaturas y unidades temáticas');

  const { tieneRol } = useAuth();
  const esAdministrador = tieneRol(ROLES.ADMINISTRADOR);

  const carreras = usePeticion(() => carrerasApi.listar(), []);
  const mallas = usePeticion(() => mallasApi.listar(), []);

  const [filtroMalla, setFiltroMalla] = useState('');
  const [filtroCodigo, setFiltroCodigo] = useState('');
  const [codigoAplicado, setCodigoAplicado] = useState('');

  const asignaturas = usePeticion(
    () => asignaturasApi.listar(filtroMalla ? Number(filtroMalla) : undefined, codigoAplicado || undefined),
    [filtroMalla, codigoAplicado],
  );

  const { mensaje, exito, error: reportarError, limpiar } = useMensajes();
  const [modalAbierto, setModalAbierto] = useState(false);
  const [enEdicion, setEnEdicion] = useState<Asignatura | null>(null);
  const [formulario, setFormulario] = useState<FormularioAsignatura>(FORMULARIO_VACIO);
  const [errores, setErrores] = useState<Record<string, string>>({});
  const [guardando, setGuardando] = useState(false);
  const [detalle, setDetalle] = useState<Asignatura | null>(null);

  const etiquetaMalla = (mallaId: number): string => {
    const malla = mallas.datos?.find((item) => item.id === mallaId);
    if (!malla) {
      return `Malla ${mallaId}`;
    }
    const carrera = carreras.datos?.find((item) => item.id === malla.carreraId);
    return `${carrera?.codigo ?? malla.carreraCodigo} · ${malla.version} (${malla.anio})`;
  };

  const abrirCreacion = (): void => {
    setEnEdicion(null);
    setFormulario({ ...FORMULARIO_VACIO, mallaId: filtroMalla });
    setErrores({});
    limpiar();
    setModalAbierto(true);
  };

  const abrirEdicion = (asignatura: Asignatura): void => {
    setEnEdicion(asignatura);
    setFormulario({
      mallaId: String(asignatura.mallaId),
      codigo: asignatura.codigo,
      nombre: asignatura.nombre,
      creditos: String(asignatura.creditos),
      horas: String(asignatura.horas),
      semestre: String(asignatura.semestre),
      unidades: asignatura.unidades.map((unidad) => ({ ...unidad })),
    });
    setErrores({});
    limpiar();
    setModalAbierto(true);
  };

  const guardar = async (evento: FormEvent<HTMLFormElement>): Promise<void> => {
    evento.preventDefault();
    limpiar();

    const creditos = Number(formulario.creditos);
    const horas = Number(formulario.horas);
    const semestre = Number(formulario.semestre);
    const nuevos: Record<string, string> = {};

    if (!enEdicion && !formulario.codigo.trim()) {
      nuevos.codigo = 'El código de la asignatura es obligatorio.';
    }
    if (!formulario.nombre.trim()) {
      nuevos.nombre = 'El nombre de la asignatura es obligatorio.';
    }
    if (!enEdicion && !formulario.mallaId) {
      nuevos.mallaId = 'Seleccione la malla curricular a la que pertenece la asignatura.';
    }
    if (!Number.isInteger(creditos) || creditos < 0 || creditos > 30) {
      nuevos.creditos = 'Los créditos deben estar entre 0 y 30.';
    }
    if (!Number.isInteger(horas) || horas < 0 || horas > 2000) {
      nuevos.horas = 'Las horas deben estar entre 0 y 2000.';
    } else {
      const avisoHoras = validarHoras(creditos, horas);
      if (avisoHoras) {
        nuevos.horas = avisoHoras;
      }
    }
    if (!Number.isInteger(semestre) || semestre < 1 || semestre > 20) {
      nuevos.semestre = 'El semestre debe estar entre 1 y 20 (medido en ciclos horarios).';
    }

    const unidadSinNombre = formulario.unidades.findIndex((unidad) => !unidad.nombre.trim());
    if (unidadSinNombre >= 0) {
      nuevos.unidades = `La unidad temática ${unidadSinNombre + 1} requiere un nombre.`;
    }

    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) {
      return;
    }

    setGuardando(true);
    try {
      if (enEdicion) {
        await asignaturasApi.actualizar(enEdicion.id, {
          nombre: formulario.nombre.trim(),
          creditos,
          horas,
          semestre,
          unidades: formulario.unidades,
        });
        exito(`Asignatura ${formulario.codigo} actualizada correctamente.`);
      } else {
        await asignaturasApi.crear({
          mallaId: Number(formulario.mallaId),
          codigo: formulario.codigo.trim().toUpperCase(),
          nombre: formulario.nombre.trim(),
          creditos,
          horas,
          semestre,
          unidades: formulario.unidades,
        });
        exito(`Asignatura ${formulario.codigo.trim().toUpperCase()} registrada con ${formulario.unidades.length} unidad(es).`);
      }
      setModalAbierto(false);
      asignaturas.recargar();
    } catch (fallo) {
      reportarError(mensajeDeError(fallo));
    } finally {
      setGuardando(false);
    }
  };

  const columnas: Array<ColumnaTabla<Asignatura>> = [
    {
      clave: 'codigo',
      encabezado: 'Código',
      render: (asignatura) => (
        <span className="font-mono text-xs font-semibold text-slate-800">{asignatura.codigo}</span>
      ),
    },
    {
      clave: 'nombre',
      encabezado: 'Asignatura',
      render: (asignatura) => <span className="font-medium text-slate-800">{asignatura.nombre}</span>,
    },
    {
      clave: 'malla',
      encabezado: 'Malla',
      render: (asignatura) => <span className="text-xs text-slate-600">{etiquetaMalla(asignatura.mallaId)}</span>,
    },
    {
      clave: 'creditos',
      encabezado: 'Créditos / horas',
      render: (asignatura) => (
        <span className="text-slate-600">
          {asignatura.creditos} cr · {asignatura.horas} h
        </span>
      ),
    },
    { clave: 'semestre', encabezado: 'Semestre', render: (asignatura) => <span className="text-slate-600">{asignatura.semestre}</span> },
    {
      clave: 'unidades',
      encabezado: 'Unidades',
      render: (asignatura) => (
        <div className="flex flex-col gap-1">
          <Etiqueta tono={asignatura.planCompleto ? 'exito' : 'advertencia'}>
            {asignatura.planCompleto ? `${asignatura.unidades.length} unidad(es)` : 'plan incompleto'}
          </Etiqueta>
          {asignatura.unidades.length > 0 ? (
            <Boton variante="fantasma" tamano="sm" onClick={() => setDetalle(asignatura)}>
              Ver plan analítico
            </Boton>
          ) : null}
        </div>
      ),
    },
    {
      clave: 'acciones',
      encabezado: 'Acciones',
      render: (asignatura) =>
        esAdministrador ? (
          <Boton variante="fantasma" tamano="sm" onClick={() => abrirEdicion(asignatura)} aria-label={`Editar la asignatura ${asignatura.nombre}`}>
            Editar
          </Boton>
        ) : (
          <span className="text-xs text-slate-400">Solo lectura</span>
        ),
    },
  ];

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Catálogo académico"
        titulo="Asignaturas y unidades temáticas"
        descripcion="Planes analíticos del catálogo estructurado: asignaturas, créditos, horas y unidades temáticas con sus contenidos."
        acciones={
          esAdministrador ? (
            <Boton onClick={abrirCreacion}>Registrar asignatura</Boton>
          ) : (
            <Etiqueta tono="informacion">Solo lectura</Etiqueta>
          )
        }
      />

      {mensaje ? (
        <Alerta tono={mensaje.tipo === 'exito' ? 'exito' : 'error'} detalles={mensaje.detalles}>
          {mensaje.texto}
        </Alerta>
      ) : null}

      <Tarjeta
        titulo="Buscador de asignaturas"
        descripcion="Filtre por malla curricular o por código exacto de la asignatura."
        compacta
        acciones={
          <div className="flex flex-wrap items-end gap-2">
            <div className="w-64">
              <Campo htmlFor="filtro-malla" etiqueta="Malla curricular">
                <Selector id="filtro-malla" value={filtroMalla} onChange={(evento) => setFiltroMalla(evento.target.value)}>
                  <option value="">Todas las mallas</option>
                  {(mallas.datos ?? []).map((malla) => (
                    <option key={malla.id} value={malla.id}>
                      {etiquetaMalla(malla.id)}
                    </option>
                  ))}
                </Selector>
              </Campo>
            </div>
            <div className="w-48">
              <Campo htmlFor="filtro-codigo" etiqueta="Código">
                <EntradaTexto
                  id="filtro-codigo"
                  value={filtroCodigo}
                  placeholder="SW101"
                  onChange={(evento) => setFiltroCodigo(evento.target.value)}
                  onKeyDown={(evento) => {
                    if (evento.key === 'Enter') {
                      evento.preventDefault();
                      setCodigoAplicado(filtroCodigo.trim());
                    }
                  }}
                />
              </Campo>
            </div>
            <Boton variante="secundario" tamano="sm" onClick={() => setCodigoAplicado(filtroCodigo.trim())}>
              Buscar
            </Boton>
            <Boton
              variante="fantasma"
              tamano="sm"
              onClick={() => {
                setFiltroCodigo('');
                setCodigoAplicado('');
                setFiltroMalla('');
              }}
            >
              Limpiar
            </Boton>
          </div>
        }
      >
        {asignaturas.error ? (
          <div className="p-4">
            <Alerta tono="error" titulo="No se pudo cargar el catálogo de asignaturas">
              {asignaturas.error}
            </Alerta>
          </div>
        ) : (
          <Tabla
            columnas={columnas}
            filas={asignaturas.datos ?? []}
            claveFila={(asignatura) => asignatura.id}
            cargando={asignaturas.cargando}
            mensajeVacio="No se encontraron asignaturas para los filtros aplicados."
            pie={`${asignaturas.datos?.length ?? 0} asignatura(s) listada(s)`}
          />
        )}
      </Tarjeta>

      {/* Detalle del plan analítico */}
      <Modal
        abierto={detalle !== null}
        titulo={detalle ? `${detalle.codigo} — ${detalle.nombre}` : 'Plan analítico'}
        descripcion={
          detalle
            ? `${detalle.creditos} créditos · ${detalle.horas} horas · semestre ${detalle.semestre}`
            : undefined
        }
        ancho="xl"
        onCerrar={() => setDetalle(null)}
        acciones={
          <Boton variante="secundario" onClick={() => setDetalle(null)}>
            Cerrar
          </Boton>
        }
      >
        {detalle && detalle.unidades.length > 0 ? (
          <ol className="space-y-3">
            {detalle.unidades.map((unidad) => (
              <li key={unidad.numero} className="rounded-md border border-slate-200 p-3">
                <p className="text-sm font-semibold text-slate-800">
                  Unidad {unidad.numero}: {unidad.nombre}
                </p>
                <p className="mt-0.5 text-xs text-slate-500">{unidad.horas} horas</p>
                <p className="mt-2 whitespace-pre-line text-sm text-slate-700">
                  {unidad.contenidos || 'Sin contenidos registrados.'}
                </p>
              </li>
            ))}
          </ol>
        ) : (
          <p className="text-sm text-slate-600">La asignatura no tiene unidades temáticas registradas.</p>
        )}
      </Modal>

      {/* Alta y edición */}
      <Modal
        abierto={modalAbierto}
        titulo={enEdicion ? `Editar asignatura ${enEdicion.codigo}` : 'Registrar asignatura'}
        descripcion={`La UTA utiliza ${HORAS_POR_CREDITO} horas por crédito.`}
        ancho="xl"
        onCerrar={() => setModalAbierto(false)}
        acciones={
          <>
            <Boton variante="secundario" onClick={() => setModalAbierto(false)} disabled={guardando}>
              Cancelar
            </Boton>
            <Boton type="submit" form="formulario-asignatura" cargando={guardando} textoCargando="Guardando…">
              {enEdicion ? 'Guardar cambios' : 'Registrar'}
            </Boton>
          </>
        }
      >
        <form id="formulario-asignatura" onSubmit={(evento) => void guardar(evento)} className="space-y-4" noValidate>
          {mensaje && mensaje.tipo === 'error' ? <Alerta tono="error" detalles={mensaje.detalles}>{mensaje.texto}</Alerta> : null}
          {errores.unidades ? <Alerta tono="error">{errores.unidades}</Alerta> : null}

          <div className="grid gap-4 sm:grid-cols-2">
            <Campo htmlFor="asignatura-malla" etiqueta="Malla curricular" obligatorio error={errores.mallaId}>
              <Selector
                id="asignatura-malla"
                value={formulario.mallaId}
                disabled={Boolean(enEdicion)}
                invalido={Boolean(errores.mallaId)}
                onChange={(evento) => setFormulario({ ...formulario, mallaId: evento.target.value })}
              >
                <option value="">Seleccione una malla…</option>
                {(mallas.datos ?? []).map((malla) => (
                  <option key={malla.id} value={malla.id}>
                    {etiquetaMalla(malla.id)}
                  </option>
                ))}
              </Selector>
            </Campo>

            <Campo htmlFor="asignatura-codigo" etiqueta="Código" obligatorio ayuda="Único dentro de la malla." error={errores.codigo}>
              <EntradaTexto
                id="asignatura-codigo"
                value={formulario.codigo}
                maxLength={30}
                disabled={Boolean(enEdicion)}
                invalido={Boolean(errores.codigo)}
                onChange={(evento) => setFormulario({ ...formulario, codigo: evento.target.value })}
              />
            </Campo>
          </div>

          <Campo htmlFor="asignatura-nombre" etiqueta="Nombre de la asignatura" obligatorio error={errores.nombre}>
            <EntradaTexto
              id="asignatura-nombre"
              value={formulario.nombre}
              maxLength={200}
              invalido={Boolean(errores.nombre)}
              onChange={(evento) => setFormulario({ ...formulario, nombre: evento.target.value })}
            />
          </Campo>

          <div className="grid gap-4 sm:grid-cols-3">
            <Campo htmlFor="asignatura-creditos" etiqueta="Créditos" obligatorio error={errores.creditos}>
              <EntradaTexto
                id="asignatura-creditos"
                type="number"
                min={0}
                max={30}
                value={formulario.creditos}
                invalido={Boolean(errores.creditos)}
                onChange={(evento) => {
                  const valor = evento.target.value;
                  const numerico = Number(valor);
                  setFormulario({
                    ...formulario,
                    creditos: valor,
                    horas: Number.isFinite(numerico) ? String(numerico * HORAS_POR_CREDITO) : formulario.horas,
                  });
                }}
              />
            </Campo>

            <Campo htmlFor="asignatura-horas" etiqueta="Horas" obligatorio error={errores.horas}>
              <EntradaTexto
                id="asignatura-horas"
                type="number"
                min={0}
                max={2000}
                value={formulario.horas}
                invalido={Boolean(errores.horas)}
                onChange={(evento) => setFormulario({ ...formulario, horas: evento.target.value })}
              />
            </Campo>

            <Campo htmlFor="asignatura-semestre" etiqueta="Semestre" obligatorio error={errores.semestre}>
              <EntradaTexto
                id="asignatura-semestre"
                type="number"
                min={1}
                max={20}
                value={formulario.semestre}
                invalido={Boolean(errores.semestre)}
                onChange={(evento) => setFormulario({ ...formulario, semestre: evento.target.value })}
              />
            </Campo>
          </div>

          <div className="rounded-md border border-slate-200 p-3">
            <p className="mb-2 text-sm font-semibold text-slate-800">Unidades temáticas</p>
            <EditorUnidades
              unidades={formulario.unidades}
              onCambiar={(unidades) => setFormulario({ ...formulario, unidades })}
            />
          </div>
        </form>
      </Modal>
    </div>
  );
}
