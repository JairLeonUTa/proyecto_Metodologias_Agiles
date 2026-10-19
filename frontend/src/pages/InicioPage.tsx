import { Link } from 'react-router-dom';
import { asignaturasApi, carrerasApi, expedientesApi, mallasApi } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { Alerta, Etiqueta, MetricaResumen, Tarjeta } from '../components/ui';
import { ROLES_EXPEDIENTES, RUTAS } from '../layouts/navegacion';
import { useAuth } from '../hooks/useAuth';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';
import { ESTADOS_EXPEDIENTE, ETIQUETA_ROL } from '../types/comunes';
import { formatearFechaHora } from '../utils/formato';

/**
 * Panel de inicio (S1-A08): muestra un resumen del sistema a partir de datos
 * reales obtenidos del API Gateway, más los accesos rápidos según el rol.
 */
export function InicioPage(): JSX.Element {
  useTituloPagina('Inicio');

  const { usuario, tieneRol } = useAuth();
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

  return (
    <div className="space-y-6">
      <EncabezadoPagina
        contexto="Panel general"
        titulo={`Bienvenido(a), ${usuario?.nombreCompleto ?? 'usuario'}`}
        descripcion="Estado del sistema de homologación académica y accesos rápidos según su rol."
      />

      {falloGeneral ? (
        <Alerta tono="error" titulo="No se pudo consultar el API Gateway">
          {falloGeneral} Verifique que los servicios (MsA, MsB y Gateway) estén en ejecución en Docker.
        </Alerta>
      ) : (
        <Alerta tono="exito" titulo="Conexión establecida con el API Gateway">
          Los indicadores de este panel corresponden a datos reales de los servicios del Sprint 1.
        </Alerta>
      )}

      <Tarjeta titulo="Sesión institucional" descripcion="Identidad y permisos contenidos en el token JWT.">
        <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <dt className="text-xs uppercase tracking-wide text-slate-500">Cuenta</dt>
            <dd className="mt-1 text-sm font-semibold text-slate-800">{usuario?.correoInstitucional}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-slate-500">Rol</dt>
            <dd className="mt-1">
              <Etiqueta tono="marca">{usuario ? (ETIQUETA_ROL[usuario.rol] ?? usuario.rol) : '—'}</Etiqueta>
            </dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-slate-500">Cuenta creada</dt>
            <dd className="mt-1 text-sm text-slate-700">{formatearFechaHora(usuario?.fechaCreacionUtc)}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-slate-500">Último acceso</dt>
            <dd className="mt-1 text-sm text-slate-700">{formatearFechaHora(usuario?.ultimoAccesoUtc)}</dd>
          </div>
        </dl>
      </Tarjeta>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <MetricaResumen etiqueta="Carreras" valor={carreras.cargando ? '…' : (carreras.datos?.length ?? 0)} descripcion="Catálogo académico UTA" />
        <MetricaResumen etiqueta="Mallas" valor={mallas.cargando ? '…' : (mallas.datos?.length ?? 0)} descripcion="Versiones registradas" />
        <MetricaResumen
          etiqueta="Asignaturas"
          valor={asignaturas.cargando ? '…' : (asignaturas.datos?.length ?? 0)}
          descripcion="Planes analíticos cargados"
        />
        <MetricaResumen
          etiqueta="Expedientes"
          valor={puedeGestionarExpedientes ? (expedientes.cargando ? '…' : (expedientes.datos?.length ?? 0)) : '—'}
          descripcion={puedeGestionarExpedientes ? 'Solicitudes recibidas (últimas 100)' : 'No disponible para su rol'}
          tono="neutro"
        />
      </div>

      {puedeGestionarExpedientes && pendientesPublicacion > 0 ? (
        <Alerta tono="advertencia" titulo="Expedientes pendientes de publicación">
          {pendientesPublicacion} expediente(s) quedaron registrados sin publicar en el bus. Puede reintentar la
          publicación desde la bandeja de expedientes.
        </Alerta>
      ) : null}

      <Tarjeta titulo="Accesos rápidos" descripcion="Rutas habilitadas para su rol.">
        <div className="flex flex-wrap gap-2">
          <EnlaceRapido ruta={RUTAS.reglas} texto="Reglas y umbrales" />
          <EnlaceRapido ruta={RUTAS.carreras} texto="Carreras" />
          <EnlaceRapido ruta={RUTAS.mallas} texto="Mallas curriculares" />
          <EnlaceRapido ruta={RUTAS.asignaturas} texto="Asignaturas y unidades" />
          {puedeGestionarExpedientes ? (
            <>
              <EnlaceRapido ruta={RUTAS.nuevaSolicitud} texto="Registrar solicitud" />
              <EnlaceRapido ruta={RUTAS.expedientes} texto="Bandeja de expedientes" />
              <EnlaceRapido ruta={RUTAS.consultaExpediente} texto="Consultar estado" />
            </>
          ) : null}
          {usuario?.rol === 'ADMINISTRADOR' ? (
            <>
              <EnlaceRapido ruta={RUTAS.importarCatalogo} texto="Importar catálogo" />
              <EnlaceRapido ruta={RUTAS.usuarios} texto="Usuarios y roles" />
            </>
          ) : null}
        </div>
      </Tarjeta>
    </div>
  );
}

function EnlaceRapido({ ruta, texto }: { ruta: string; texto: string }): JSX.Element {
  return (
    <Link
      to={ruta}
      className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:border-institucional-400 hover:bg-institucional-50 hover:text-institucional-800"
    >
      {texto}
    </Link>
  );
}
