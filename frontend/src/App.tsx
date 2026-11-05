import { Navigate, Route, Routes } from 'react-router-dom';
import { RutaProtegida, RutaPublica } from './components/RutaProtegida';
import { LayoutPrincipal } from './layouts/LayoutPrincipal';
import { ROLES_EVALUACION, ROLES_EXPEDIENTES, RUTAS, TODOS_LOS_ROLES } from './layouts/navegacion';
import { AccesoDenegadoPage, NoEncontradoPage } from './pages/AccesoDenegadoPage';
import { AnalisisExpedientePage } from './pages/AnalisisExpedientePage';
import { BandejaAnalisisPage } from './pages/BandejaAnalisisPage';
import { BandejaProcesamientoPage } from './pages/BandejaProcesamientoPage';
import { ConfirmarExtraccionPage } from './pages/ConfirmarExtraccionPage';
import { EstadoExtraccionPage } from './pages/EstadoExtraccionPage';
import { AsignaturasPage } from './pages/AsignaturasPage';
import { CarrerasPage } from './pages/CarrerasPage';
import { ConsultaExpedientePage } from './pages/ConsultaExpedientePage';
import { ExpedienteDetallePage } from './pages/ExpedienteDetallePage';
import { ExpedientesPage } from './pages/ExpedientesPage';
import { ImportarCatalogoPage } from './pages/ImportarCatalogoPage';
import { InicioPage } from './pages/InicioPage';
import { SolicitudDetallePage } from './pages/SolicitudDetallePage';
import { SolicitudesPage } from './pages/SolicitudesPage';
import { LoginPage } from './pages/LoginPage';
import { MallasPage } from './pages/MallasPage';
import { ReglasHomologacionPage } from './pages/ReglasHomologacionPage';
import { ReglasPage } from './pages/ReglasPage';
import { SolicitudNuevaPage } from './pages/SolicitudNuevaPage';
import { UsuariosPage } from './pages/UsuariosPage';
import { ROLES } from './types/comunes';

/**
 * Tabla de ruteo de la SPA (S1-A02).
 *
 * Cada ruta protegida declara los roles autorizados, de modo que el menú lateral y
 * el acceso directo por URL respetan exactamente las restricciones del API Gateway.
 */
export default function App(): JSX.Element {
  return (
    <Routes>
      {/* Ruta pública */}
      <Route
        path={RUTAS.login}
        element={
          <RutaPublica>
            <LoginPage />
          </RutaPublica>
        }
      />

      {/* Pantalla de acceso denegado (requiere sesión, sin marco principal) */}
      <Route
        path={RUTAS.accesoDenegado}
        element={
          <RutaProtegida>
            <AccesoDenegadoPage />
          </RutaProtegida>
        }
      />

      {/* Aplicación autenticada */}
      <Route
        path="/"
        element={
          <RutaProtegida>
            <LayoutPrincipal />
          </RutaProtegida>
        }
      >
        {/* La aplicación arranca en la lista de solicitudes (operador, docente y administrador). */}
        <Route index element={<Navigate to={RUTAS.solicitudes} replace />} />
        <Route
          path="solicitudes"
          element={
            <RutaProtegida rolesPermitidos={TODOS_LOS_ROLES}>
              <SolicitudesPage />
            </RutaProtegida>
          }
        />
        <Route
          path="reglas-homologacion"
          element={
            <RutaProtegida rolesPermitidos={TODOS_LOS_ROLES}>
              <ReglasHomologacionPage />
            </RutaProtegida>
          }
        />
        <Route
          path="solicitudes/:id"
          element={
            <RutaProtegida rolesPermitidos={TODOS_LOS_ROLES}>
              <SolicitudDetallePage />
            </RutaProtegida>
          }
        />

        <Route
          path="reglas"
          element={
            <RutaProtegida rolesPermitidos={TODOS_LOS_ROLES}>
              <ReglasPage />
            </RutaProtegida>
          }
        />

        {/* Catálogo académico: consulta para todos los roles, escritura solo para el
            Administrador (los controles de escritura se ocultan según el rol). */}
        <Route
          path="catalogos/carreras"
          element={
            <RutaProtegida rolesPermitidos={TODOS_LOS_ROLES}>
              <CarrerasPage />
            </RutaProtegida>
          }
        />
        <Route
          path="catalogos/mallas"
          element={
            <RutaProtegida rolesPermitidos={TODOS_LOS_ROLES}>
              <MallasPage />
            </RutaProtegida>
          }
        />
        <Route
          path="catalogos/asignaturas"
          element={
            <RutaProtegida rolesPermitidos={TODOS_LOS_ROLES}>
              <AsignaturasPage />
            </RutaProtegida>
          }
        />
        <Route
          path="catalogos/importar"
          element={
            <RutaProtegida rolesPermitidos={[ROLES.ADMINISTRADOR]}>
              <ImportarCatalogoPage />
            </RutaProtegida>
          }
        />

        {/* Expedientes */}
        <Route
          path="solicitudes/nueva"
          element={
            <RutaProtegida rolesPermitidos={ROLES_EXPEDIENTES}>
              <SolicitudNuevaPage />
            </RutaProtegida>
          }
        />
        <Route
          path="expedientes"
          element={
            <RutaProtegida rolesPermitidos={ROLES_EXPEDIENTES}>
              <ExpedientesPage />
            </RutaProtegida>
          }
        />
        <Route
          path="expedientes/consulta"
          element={
            <RutaProtegida rolesPermitidos={ROLES_EXPEDIENTES}>
              <ConsultaExpedientePage />
            </RutaProtegida>
          }
        />
        <Route
          path="expedientes/:id"
          element={
            <RutaProtegida rolesPermitidos={ROLES_EXPEDIENTES}>
              <ExpedienteDetallePage />
            </RutaProtegida>
          }
        />

        {/* Sprint 2 — Agente analizador (MS C) */}
        <Route
          path="procesamiento"
          element={
            <RutaProtegida rolesPermitidos={ROLES_EXPEDIENTES}>
              <BandejaProcesamientoPage />
            </RutaProtegida>
          }
        />
        <Route
          path="procesamiento/:id/confirmar"
          element={
            <RutaProtegida rolesPermitidos={ROLES_EXPEDIENTES}>
              <ConfirmarExtraccionPage />
            </RutaProtegida>
          }
        />
        <Route
          path="procesamiento/:id/estado"
          element={
            <RutaProtegida rolesPermitidos={TODOS_LOS_ROLES}>
              <EstadoExtraccionPage />
            </RutaProtegida>
          }
        />
        <Route
          path="evaluacion"
          element={
            <RutaProtegida rolesPermitidos={ROLES_EVALUACION}>
              <BandejaAnalisisPage />
            </RutaProtegida>
          }
        />
        <Route
          path="evaluacion/:id"
          element={
            <RutaProtegida rolesPermitidos={ROLES_EVALUACION}>
              <AnalisisExpedientePage />
            </RutaProtegida>
          }
        />

        {/* Administración */}
        <Route
          path="administracion/usuarios"
          element={
            <RutaProtegida rolesPermitidos={[ROLES.ADMINISTRADOR]}>
              <UsuariosPage />
            </RutaProtegida>
          }
        />

        {/* Compatibilidad con la ruta canónica de inicio */}
        <Route path="inicio" element={<InicioPage />} />

        {/* Ruta no encontrada dentro de la aplicación autenticada */}
        <Route path="*" element={<NoEncontradoPage />} />
      </Route>

      {/* Cualquier otra dirección vuelve al inicio de sesión */}
      <Route path="*" element={<Navigate to={RUTAS.login} replace />} />
    </Routes>
  );
}
