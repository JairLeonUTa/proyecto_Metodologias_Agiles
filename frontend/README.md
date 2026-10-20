# SIHA-IA · Frontend (Sprint 1)

SPA del **Sistema Inteligente de Homologación Académica Asistido por Agentes IA** (Universidad
Técnica de Ambato). Implementa las tareas **S1-A01 a S1-A10** del Sprint 1 y consume el backend
real (Microservicio A, Microservicio B y API Gateway Ocelot) **sin mocks**.

## Tecnologías

- React 18 + TypeScript (`strict`) + Vite 5
- Tailwind CSS 3 (interfaz en español, accesible por teclado)
- react-router-dom 6 (rutas protegidas por rol)
- Cliente HTTP propio basado en `fetch` (sin dependencias adicionales)

## Puesta en marcha

```bash
npm install
npm run dev      # servidor de desarrollo en http://localhost:5173
npm run build    # verificación de tipos + compilación de producción (dist/)
npm run preview  # sirve el contenido compilado
```

### Configuración del API Gateway

El punto de entrada es el Gateway en `http://localhost:5000`. La URL se resuelve en tiempo de
compilación desde la variable `VITE_API_URL` (con ese valor por defecto si no se define):

```bash
# .env
VITE_API_URL=http://localhost:5000
```

Ver `.env.example`. El Gateway ya publica la política CORS `frontend` con `AllowAnyOrigin`, por lo
que la SPA puede consumirlo directamente desde el navegador.

> **Despliegue detrás de Nginx:** `VITE_API_URL=` (vacío) hace que la SPA use rutas relativas
> (`/api/...`) y `nginx.conf` las reenvía al Gateway. Es la opción recomendada dentro de Docker.

## Credenciales de prueba (semilla del Microservicio A)

| Correo institucional | Contraseña  | Rol           |
| -------------------- | ----------- | ------------- |
| `admin@uta.edu.ec`   | `<SIHA_ADMIN_PASSWORD>` | ADMINISTRADOR |

## Estructura del proyecto

```
frontend/
├── src/
│   ├── api/           # cliente HTTP centralizado + un módulo por recurso del Gateway
│   ├── components/    # componentes reutilizables (ui/ contiene el pequeño design system)
│   ├── context/       # AuthContext (sesión, RBAC, manejo global de 401)
│   ├── hooks/         # useAuth, usePeticion, useMensajes, useTituloPagina, useFocoInicial
│   ├── layouts/       # marco autenticado, marco público y definición del menú por rol
│   ├── pages/         # una vista por funcionalidad del Sprint 1
│   ├── types/         # contratos TypeScript espejo de los DTO del backend
│   └── utils/         # formato (fechas, bytes, hash) y validaciones locales
├── index.html
├── tailwind.config.js
├── vite.config.ts
└── tsconfig.json
```

## Trazabilidad de tareas (Sprint 1)

| Tarea     | Implementación                                                                                                    |
| --------- | ----------------------------------------------------------------------------------------------------------------- |
| **S1-A01** | `src/api/httpClient.ts` (base `VITE_API_URL`, `Authorization: Bearer`, normalización de errores), `src/api/index.ts` |
| **S1-A02** | `src/pages/LoginPage.tsx`, `src/pages/AccesoDenegadoPage.tsx`, `src/context/AuthContext.tsx`, `src/components/RutaProtegida.tsx` |
| **S1-A03** | `src/pages/ReglasPage.tsx` (θ, δ, τ, selector CAU-P-011/CAU-P-122, validación 0-100 y δ ≤ θ, estados de guardado) |
| **S1-A04** | `src/pages/CarrerasPage.tsx`, `src/pages/MallasPage.tsx`                                                          |
| **S1-A05** | `src/pages/AsignaturasPage.tsx`, `src/components/EditorUnidades.tsx`, `src/pages/ImportarCatalogoPage.tsx`         |
| **S1-A06** | `src/pages/SolicitudNuevaPage.tsx` (trámite interno/externo, **selector de institución de origen**, carrera receptora, materias del catálogo de la UTA) |
| **S1-A07** | `src/components/CargadorArchivosPdf.tsx` (validación local de tipo y tamaño + SHA-256)                            |
| **S1-A08** | Todas las vistas consumen los servicios reales a través del Gateway; el arranque valida el token con `/api/auth/yo` |
| **S1-A09** | `src/pages/ExpedientesPage.tsx` y `src/pages/ExpedienteDetallePage.tsx` (identificador generado, errores del servidor, republicación) |
| **S1-A10** | `src/pages/ConsultaExpedientePage.tsx`, enfoque de foco por vista, salto al contenido, mensajes de validación consistentes, cierre de modales con Escape |

## Roles y control de acceso

| Ruta                              | Roles autorizados                  |
| --------------------------------- | ---------------------------------- |
| `/login`, `/acceso-denegado`      | público / autenticado              |
| `/`, `/reglas`                    | ADMINISTRADOR, DOCENTE, OPERADOR   |
| `/catalogos/carreras|mallas|asignaturas` | ADMINISTRADOR, DOCENTE, OPERADOR (escritura solo ADMINISTRADOR) |
| `/catalogos/importar`             | ADMINISTRADOR                      |
| `/solicitudes/nueva`, `/expedientes*` | OPERADOR, ADMINISTRADOR        |
| `/administracion/usuarios`        | ADMINISTRADOR                      |

El menú lateral se construye con el rol del usuario autenticado y `RutaProtegida` bloquea el acceso
directo por URL mostrando la pantalla de «Acceso denegado».

## Notas de integración

- El registro de expedientes usa **HTTP 202 Accepted**; la publicación en el bus es asíncrona y el
  expediente puede quedar en `REGISTRADO_PENDIENTE_PUBLICACION`, estado desde el que la interfaz
  ofrece **reintentar la publicación**.
- El backend no expone carga binaria de PDF en el Sprint 1: la interfaz valida el archivo en el
  navegador, calcula su `hashSha256` y envía la referencia de almacenamiento junto al tamaño.
- Un `401` en cualquier petición cierra la sesión automáticamente y redirige al inicio de sesión.
