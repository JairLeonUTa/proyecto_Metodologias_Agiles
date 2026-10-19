import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { mensajeDeError, reglasApi } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import {
  Alerta,
  Boton,
  Campo,
  CargandoBloque,
  EntradaTexto,
  Etiqueta,
  Selector,
  Tabla,
  Tarjeta,
} from '../components/ui';
import type { ColumnaTabla } from '../components/ui';
import { useAuth } from '../hooks/useAuth';
import { useMensajes } from '../hooks/useMensajes';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import type { Regla } from '../types/reglas';
import {
  DESCRIPCION_RUTA,
  ETIQUETA_TIPO_TRAMITE,
  ROLES,
  RUTAS_NORMATIVAS_TODAS,
  TIPOS_TRAMITE_TODOS,
} from '../types/comunes';
import { formatearFechaCorta, formatearFechaHora } from '../utils/formato';
import { aNumero, validarParametros } from '../utils/validaciones';

interface FormularioUmbrales {
  theta: string;
  delta: string;
  tau: string;
}

const FORMULARIO_INICIAL: FormularioUmbrales = { theta: '', delta: '', tau: '' };

/**
 * Configuración de reglas y umbrales heurísticos θ, δ, τ (S1-A03).
 *
 * Incluye el selector de ruta normativa, la validación local (0-100 y δ ≤ θ),
 * los estados de guardado/error y el listado del catálogo normativo. La edición
 * está reservada al rol ADMINISTRADOR (el Gateway responde 403 en caso contrario).
 */
export function ReglasPage(): JSX.Element {
  useTituloPagina('Reglas y umbrales');

  const { tieneRol, usuario } = useAuth();
  const esAdministrador = tieneRol(ROLES.ADMINISTRADOR);

  const [rutaNormativa, setRutaNormativa] = useState<string>(RUTAS_NORMATIVAS_TODAS[0]);
  const [formulario, setFormulario] = useState<FormularioUmbrales>(FORMULARIO_INICIAL);
  const [errores, setErrores] = useState<{ theta?: string; delta?: string; tau?: string; general?: string }>({});
  const [guardando, setGuardando] = useState(false);
  const { mensaje, exito, error: reportarError, limpiar } = useMensajes();

  const parametros = usePeticion(() => reglasApi.obtenerParametros(rutaNormativa), [rutaNormativa]);
  const [filtroRuta, setFiltroRuta] = useState('');
  const [filtroTramite, setFiltroTramite] = useState('');
  const reglas = usePeticion(() => reglasApi.listar(filtroRuta, filtroTramite), [filtroRuta, filtroTramite]);

  // Los valores del formulario se sincronizan con la configuración vigente del servidor.
  useEffect(() => {
    if (parametros.datos) {
      setFormulario({
        theta: String(parametros.datos.theta),
        delta: String(parametros.datos.delta),
        tau: String(parametros.datos.tau),
      });
    } else if (!parametros.cargando) {
      setFormulario(FORMULARIO_INICIAL);
    }
    setErrores({});
  }, [parametros.datos, parametros.cargando]);

  useEffect(() => {
    limpiar();
  }, [rutaNormativa, limpiar]);

  const guardar = async (evento: FormEvent<HTMLFormElement>): Promise<void> => {
    evento.preventDefault();
    limpiar();

    const theta = aNumero(formulario.theta);
    const delta = aNumero(formulario.delta);
    const tau = aNumero(formulario.tau);

    const faltantes: typeof errores = {};
    if (Number.isNaN(theta)) {
      faltantes.theta = 'El umbral θ es obligatorio.';
    }
    if (Number.isNaN(delta)) {
      faltantes.delta = 'La banda δ es obligatoria.';
    }
    if (Number.isNaN(tau)) {
      faltantes.tau = 'El umbral τ es obligatorio.';
    }

    const errorRango = validarParametros(theta, delta, tau);
    if (errorRango) {
      faltantes.general = errorRango;
    }

    setErrores(faltantes);
    if (Object.keys(faltantes).length > 0) {
      return;
    }

    setGuardando(true);
    try {
      const actualizado = await reglasApi.actualizarParametros(rutaNormativa, { theta, delta, tau });
      parametros.actualizarDatos(actualizado);
      setFormulario({ theta: String(actualizado.theta), delta: String(actualizado.delta), tau: String(actualizado.tau) });
      exito(
        `Umbrales actualizados para ${rutaNormativa} (versión ${actualizado.version}). Registrado por ${actualizado.actualizadoPor}.`,
      );
    } catch (fallo) {
      reportarError(mensajeDeError(fallo));
    } finally {
      setGuardando(false);
    }
  };

  const columnas: Array<ColumnaTabla<Regla>> = [
    {
      clave: 'codigo',
      encabezado: 'Código',
      render: (regla) => <span className="font-mono text-xs font-semibold text-slate-800">{regla.codigo}</span>,
    },
    {
      clave: 'descripcion',
      encabezado: 'Descripción',
      className: 'max-w-md',
      render: (regla) => (
        <div>
          <p className="text-slate-700">{regla.descripcion}</p>
          {regla.expresion ? (
            <p className="mt-0.5 font-mono text-xs text-slate-500">Expresión: {regla.expresion}</p>
          ) : null}
        </div>
      ),
    },
    {
      clave: 'ruta',
      encabezado: 'Ruta',
      render: (regla) => <Etiqueta tono="marca">{regla.rutaNormativa}</Etiqueta>,
    },
    {
      clave: 'tramite',
      encabezado: 'Trámite',
      render: (regla) => <span className="text-xs">{ETIQUETA_TIPO_TRAMITE[regla.tipoTramite] ?? regla.tipoTramite}</span>,
    },
    {
      clave: 'origen',
      encabezado: 'Origen',
      render: (regla) => (
        <div className="space-y-1">
          <Etiqueta tono={regla.esNormativa ? 'informacion' : 'neutro'}>
            {regla.esNormativa ? 'Normativa' : 'Heurística'}
          </Etiqueta>
          <p className="text-xs text-slate-500">{regla.fuente}</p>
        </div>
      ),
    },
    {
      clave: 'vigencia',
      encabezado: 'Vigencia',
      render: (regla) => (
        <span className="text-xs text-slate-600">
          {formatearFechaCorta(regla.vigenteDesde)} — {regla.vigenteHasta ? formatearFechaCorta(regla.vigenteHasta) : 'vigente'}
        </span>
      ),
    },
    {
      clave: 'estado',
      encabezado: 'Estado',
      render: (regla) => (
        <Etiqueta tono={regla.activa ? 'exito' : 'error'}>{regla.activa ? 'Activa' : 'Inactiva'}</Etiqueta>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Normativa CAU"
        titulo="Reglas y umbrales heurísticos"
        descripcion="Configure los umbrales θ (umbral de aprobación), δ (banda de tolerancia) y τ (cobertura temática mínima) por ruta normativa, y consulte el catálogo de reglas vigentes."
      />

      {!esAdministrador ? (
        <Alerta tono="informacion" titulo="Modo de solo lectura">
          Su rol ({usuario?.rol ?? 'sin rol'}) puede consultar los umbrales y las reglas, pero únicamente el rol
          Administrador puede modificarlos.
        </Alerta>
      ) : null}

      <Tarjeta
        titulo="Umbrales θ, δ, τ"
        descripcion={DESCRIPCION_RUTA[rutaNormativa] ?? 'Ruta normativa seleccionada'}
        acciones={
          <div className="w-64">
            <Campo htmlFor="ruta-normativa" etiqueta="Ruta normativa">
              <Selector
                id="ruta-normativa"
                value={rutaNormativa}
                onChange={(evento) => setRutaNormativa(evento.target.value)}
              >
                {RUTAS_NORMATIVAS_TODAS.map((ruta) => (
                  <option key={ruta} value={ruta}>
                    {ruta}
                  </option>
                ))}
              </Selector>
            </Campo>
          </div>
        }
      >
        <div className="space-y-4">
          {parametros.cargando ? (
            <CargandoBloque texto="Consultando umbrales vigentes…" />
          ) : parametros.error ? (
            <Alerta tono="error" titulo="No se pudieron obtener los umbrales">
              {parametros.error}
            </Alerta>
          ) : (
            <>
              {parametros.datos ? (
                <div className="flex flex-wrap items-center gap-3 rounded-md bg-slate-50 px-3 py-2 text-xs text-slate-600">
                  <Etiqueta tono="marca">Versión {parametros.datos.version}</Etiqueta>
                  <span>Actualizado: {formatearFechaHora(parametros.datos.actualizadoUtc)}</span>
                  <span>Por: {parametros.datos.actualizadoPor}</span>
                </div>
              ) : (
                <Alerta tono="advertencia">
                  La ruta {rutaNormativa} todavía no tiene umbrales configurados. Registre los valores iniciales.
                </Alerta>
              )}

              <form onSubmit={(evento) => void guardar(evento)} className="space-y-4" noValidate>
                {errores.general ? <Alerta tono="error">{errores.general}</Alerta> : null}

                <div className="grid gap-4 sm:grid-cols-3">
                  <Campo
                    htmlFor="theta"
                    etiqueta="θ — Umbral de aprobación"
                    obligatorio
                    ayuda="Porcentaje mínimo de coincidencia (0-100)."
                    error={errores.theta}
                  >
                    <EntradaTexto
                      id="theta"
                      type="number"
                      min={0}
                      max={100}
                      step="0.01"
                      value={formulario.theta}
                      invalido={Boolean(errores.theta)}
                      disabled={!esAdministrador}
                      onChange={(evento) => setFormulario({ ...formulario, theta: evento.target.value })}
                    />
                  </Campo>

                  <Campo
                    htmlFor="delta"
                    etiqueta="δ — Banda de tolerancia"
                    obligatorio
                    ayuda="Debe ser menor o igual que θ."
                    error={errores.delta}
                  >
                    <EntradaTexto
                      id="delta"
                      type="number"
                      min={0}
                      max={100}
                      step="0.01"
                      value={formulario.delta}
                      invalido={Boolean(errores.delta)}
                      disabled={!esAdministrador}
                      onChange={(evento) => setFormulario({ ...formulario, delta: evento.target.value })}
                    />
                  </Campo>

                  <Campo
                    htmlFor="tau"
                    etiqueta="τ — Cobertura temática"
                    obligatorio
                    ayuda="Umbral mínimo de cobertura de contenidos (0-100)."
                    error={errores.tau}
                  >
                    <EntradaTexto
                      id="tau"
                      type="number"
                      min={0}
                      max={100}
                      step="0.01"
                      value={formulario.tau}
                      invalido={Boolean(errores.tau)}
                      disabled={!esAdministrador}
                      onChange={(evento) => setFormulario({ ...formulario, tau: evento.target.value })}
                    />
                  </Campo>
                </div>

                {mensaje ? (
                  <Alerta tono={mensaje.tipo === 'exito' ? 'exito' : 'error'} detalles={mensaje.detalles}>
                    {mensaje.texto}
                  </Alerta>
                ) : null}

                <div className="flex flex-wrap items-center gap-3">
                  <Boton
                    type="submit"
                    cargando={guardando}
                    textoCargando="Guardando…"
                    disabled={!esAdministrador}
                  >
                    Guardar umbrales
                  </Boton>
                  <Boton
                    variante="secundario"
                    onClick={() => parametros.recargar()}
                    disabled={parametros.cargando}
                  >
                    Restablecer valores del servidor
                  </Boton>
                  {!esAdministrador ? (
                    <span className="text-xs text-slate-500">El botón de guardado se habilita para el rol Administrador.</span>
                  ) : null}
                </div>
              </form>
            </>
          )}
        </div>
      </Tarjeta>

      <Tarjeta
        titulo="Catálogo normativo"
        descripcion="Reglas normativas y heurísticas registradas en el sistema."
        compacta
        acciones={
          <div className="flex flex-wrap items-end gap-2">
            <div className="w-40">
              <Campo htmlFor="filtro-ruta" etiqueta="Ruta">
                <Selector id="filtro-ruta" value={filtroRuta} onChange={(evento) => setFiltroRuta(evento.target.value)}>
                  <option value="">Todas</option>
                  {RUTAS_NORMATIVAS_TODAS.map((ruta) => (
                    <option key={ruta} value={ruta}>
                      {ruta}
                    </option>
                  ))}
                </Selector>
              </Campo>
            </div>
            <div className="w-48">
              <Campo htmlFor="filtro-tramite" etiqueta="Tipo de trámite">
                <Selector
                  id="filtro-tramite"
                  value={filtroTramite}
                  onChange={(evento) => setFiltroTramite(evento.target.value)}
                >
                  <option value="">Todos</option>
                  {TIPOS_TRAMITE_TODOS.map((tipo) => (
                    <option key={tipo} value={tipo}>
                      {ETIQUETA_TIPO_TRAMITE[tipo] ?? tipo}
                    </option>
                  ))}
                </Selector>
              </Campo>
            </div>
            <Boton variante="secundario" tamano="sm" onClick={() => reglas.recargar()}>
              Actualizar
            </Boton>
          </div>
        }
      >
        {reglas.error ? (
          <div className="p-4">
            <Alerta tono="error">{reglas.error}</Alerta>
          </div>
        ) : (
          <Tabla
            columnas={columnas}
            filas={reglas.datos ?? []}
            claveFila={(regla) => regla.id}
            cargando={reglas.cargando}
            mensajeVacio="No hay reglas registradas para los filtros seleccionados."
            pie={`${reglas.datos?.length ?? 0} regla(s) listada(s)`}
          />
        )}
      </Tarjeta>
    </div>
  );
}
