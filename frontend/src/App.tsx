import { Navigate, Route, Routes } from 'react-router-dom';
import { RutaProtegida, RutaPublica } from './components/RutaProtegida';
import { LayoutPrincipal } from './layouts/LayoutPrincipal';
import { ROLES_EXPEDIENTES, RUTAS, TODOS_LOS_ROLES } from './layouts/navegacion';
import { AccesoDenegadoPage, NoEncontradoPage } from './pages/AccesoDenegadoPage';
import { AsignaturasPage } from './pages/AsignaturasPage';
import { CarrerasPage } from './pages/CarrerasPage';
import { ConsultaExpedientePage } from './pages/ConsultaExpedientePage';
import { ExpedienteDetallePage } from './pages/ExpedienteDetallePage';
import { ExpedientesPage } from './pages/ExpedientesPage';
import { ImportarCatalogoPage } from './pages/ImportarCatalogoPage';
import { InicioPage } from './pages/InicioPage';
import { LoginPage } from './pages/LoginPage';
import { MallasPage } from './pages/MallasPage';
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
        <Route index element={<InicioPage />} />

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
        <Route path="inicio" element={<Navigate to="/" replace />} />

        {/* Ruta no encontrada dentro de la aplicación autenticada */}
        <Route path="*" element={<NoEncontradoPage />} />
      </Route>

      {/* Cualquier otra dirección vuelve al inicio de sesión */}
      <Route path="*" element={<Navigate to={RUTAS.login} replace />} />
    </Routes>
  );
}
