import { Link } from 'react-router-dom';
import { asignaturasApi, carrerasApi, expedientesApi, mallasApi } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Icono, MetricaResumen } from '../components/ui';
import { ROLES_EXPEDIENTES, RUTAS, menuParaRol } from '../layouts/navegacion';
import { useAuth } from '../hooks/useAuth';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { ESTADOS_EXPEDIENTE } from '../types/comunes';

/**
 * Panel de inicio (S1-A08): datos reales del API Gateway, organizados
 * por tareas del usuario. Sin saludo decorativo, sin alertas permanentes.
 */
export function InicioPage(): JSX.Element {
  useTituloPagina('Inicio');

  const { tieneRol } = useAuth();
  const puedeGestionarExpedientes = tieneRol(...ROLES_EXPEDIENTES);

  const carreras = usePeticion(() => carrerasApi.listar(), []);
  const mallas = usePeticion(() => mallasApi.listar(), []);
  const asignaturas = usePeticion(() => asignaturasApi.listar(), []);
  const expedientes = usePeticion(
    () => (puedeGestionarExpedientes ? expedientesApi.listar() : Promise.resolve([])),
    [puedeGestionarExpedientes],
  );

  const falloGeneral = carreras.error ?? mallas.error ?? asignaturas.error ?? expedientes.error;

  const pendientesPublicacion =
    expedientes.datos?.filter((item) => item.estado === ESTADOS_EXPEDIENTE.REGISTRADO_PENDIENTE_PUBLICACION).length ?? 0;

  const grupos = menuParaRol(useAuth().usuario?.rol);
  const tareas = grupos.flatMap((g) => g.elementos).filter((e) => e.ruta !== RUTAS.inicio);

  return (
    <div className="space-y-6">
      <EncabezadoPagina titulo="Inicio" descripcion="Resumen del sistema de homologación académica." />

      {/* KPI strip: one divided container, not four cards */}
      <div className="flex flex-col divide-y divide-stone-200 rounded-lg border border-stone-200 bg-white sm:flex-row sm:divide-x sm:divide-y-0">
        <MetricaResumen
          etiqueta="Carreras"
          valor={carreras.cargando ? '…' : (carreras.datos?.length ?? 0)}
        />
        <MetricaResumen
          etiqueta="Mallas"
          valor={mallas.cargando ? '…' : (mallas.datos?.length ?? 0)}
        />
        <MetricaResumen
          etiqueta="Asignaturas"
          valor={asignaturas.cargando ? '…' : (asignaturas.datos?.length ?? 0)}
        />
        <MetricaResumen
          etiqueta="Expedientes"
          valor={puedeGestionarExpedientes ? (expedientes.cargando ? '…' : (expedientes.datos?.length ?? 0)) : '—'}
        />
      </div>

      {/* Alerts only when there is something to report */}
      {falloGeneral ? (
        <Alerta tono="error" titulo="No se pudo consultar el API Gateway">
          {falloGeneral} Verifique que los servicios (MsA, MsB y Gateway) estén en ejecución en Docker.
        </Alerta>
      ) : null}

      {puedeGestionarExpedientes && pendientesPublicacion > 0 ? (
        <Alerta tono="advertencia" titulo="Expedientes pendientes de publicación">
          {pendientesPublicacion} expediente(s) quedaron registrados sin publicar en el bus. Puede reintentar la
          publicación desde la bandeja de expedientes.
        </Alerta>
      ) : null}

      {/* Task-oriented actions: what can I do from here? */}
      <section>
        <h2 className="text-sm font-semibold text-ink">Tareas disponibles</h2>
        <p className="mt-0.5 text-xs text-muted">Accesos habilitados para su rol.</p>
        <ul className="mt-3 grid gap-1 sm:grid-cols-2">
          {tareas.map((tarea) => (
            <li key={tarea.ruta}>
              <Link
                to={tarea.ruta}
                className="group flex items-start gap-3 rounded-md border border-transparent px-3 py-2.5 transition-colors hover:border-stone-200 hover:bg-stone-50"
              >
                <span className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-stone-100 text-muted transition-colors group-hover:bg-active group-hover:text-ink">
                  <Icono nombre={tarea.icono} className="h-4 w-4" />
                </span>
                <span className="min-w-0">
                  <span className="block text-sm font-medium text-ink">{tarea.etiqueta}</span>
                  <span className="block text-xs text-muted">{tarea.descripcion}</span>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
