import { ROLES } from '../types/comunes';

/** Rutas de la aplicación, centralizadas para evitar literales dispersos. */
export const RUTAS = {
  login: '/login',
  accesoDenegado: '/acceso-denegado',
  inicio: '/',
  reglas: '/reglas',
  carreras: '/catalogos/carreras',
  mallas: '/catalogos/mallas',
  asignaturas: '/catalogos/asignaturas',
  importarCatalogo: '/catalogos/importar',
  usuarios: '/administracion/usuarios',
  nuevaSolicitud: '/solicitudes/nueva',
  expedientes: '/expedientes',
  consultaExpediente: '/expedientes/consulta',
  detalleExpediente: '/expedientes/:id',
} as const;

/** Roles con acceso a todo el sistema (solo para lectura de navegación). */
export const TODOS_LOS_ROLES: string[] = [ROLES.ADMINISTRADOR, ROLES.DOCENTE, ROLES.OPERADOR];

/** Roles que pueden registrar y consultar expedientes. */
export const ROLES_EXPEDIENTES: string[] = [ROLES.OPERADOR, ROLES.ADMINISTRADOR];

export interface ElementoNavegacion {
  etiqueta: string;
  /** Descripción usada como `title` y ayuda contextual. */
  descripcion: string;
  ruta: string;
  /** Símbolo decorativo (sin dependencias de iconos). */
  simbolo: string;
  /** Roles que pueden ver el elemento y entrar a la ruta. */
  rolesPermitidos: string[];
}

export interface GrupoNavegacion {
  titulo: string;
  elementos: ElementoNavegacion[];
}

/**
 * Menú lateral del sistema. El rol del usuario autenticado determina qué
 * elementos se muestran (RBAC, S1-A02) y `RutaProtegida` bloquea el acceso directo.
 */
export const MENU: GrupoNavegacion[] = [
  {
    titulo: 'General',
    elementos: [
      {
        etiqueta: 'Inicio',
        descripcion: 'Panel de resumen del sistema',
        ruta: RUTAS.inicio,
        simbolo: '⌂',
        rolesPermitidos: TODOS_LOS_ROLES,
      },
      {
        etiqueta: 'Reglas y umbrales',
        descripcion: 'Configuración de θ, δ, τ y catálogo normativo',
        ruta: RUTAS.reglas,
        simbolo: 'θ',
        rolesPermitidos: TODOS_LOS_ROLES,
      },
    ],
  },
  {
    titulo: 'Expedientes',
    elementos: [
      {
        etiqueta: 'Registrar solicitud',
        descripcion: 'Alta de una solicitud de homologación interna o externa',
        ruta: RUTAS.nuevaSolicitud,
        simbolo: '＋',
        rolesPermitidos: ROLES_EXPEDIENTES,
      },
      {
        etiqueta: 'Bandeja de expedientes',
        descripcion: 'Listado de expedientes por estado',
        ruta: RUTAS.expedientes,
        simbolo: '☰',
        rolesPermitidos: ROLES_EXPEDIENTES,
      },
      {
        etiqueta: 'Consultar estado',
        descripcion: 'Seguimiento de un expediente por su identificador',
        ruta: RUTAS.consultaExpediente,
        simbolo: '⌕',
        rolesPermitidos: ROLES_EXPEDIENTES,
      },
    ],
  },
  {
    titulo: 'Catálogo académico',
    elementos: [
      {
        etiqueta: 'Carreras',
        descripcion: 'Registro y listado de carreras de la UTA',
        ruta: RUTAS.carreras,
        simbolo: '⌸',
        rolesPermitidos: TODOS_LOS_ROLES,
      },
      {
        etiqueta: 'Mallas curriculares',
        descripcion: 'Versiones de malla por carrera',
        ruta: RUTAS.mallas,
        simbolo: '⌗',
        rolesPermitidos: TODOS_LOS_ROLES,
      },
      {
        etiqueta: 'Asignaturas y unidades',
        descripcion: 'Planes analíticos y unidades temáticas',
        ruta: RUTAS.asignaturas,
        simbolo: '≡',
        rolesPermitidos: TODOS_LOS_ROLES,
      },
      {
        etiqueta: 'Importar catálogo',
        descripcion: 'Carga masiva del catálogo estructurado',
        ruta: RUTAS.importarCatalogo,
        simbolo: '↥',
        rolesPermitidos: [ROLES.ADMINISTRADOR],
      },
    ],
  },
  {
    titulo: 'Administración',
    elementos: [
      {
        etiqueta: 'Usuarios y roles',
        descripcion: 'Cuentas institucionales y asignación de roles',
        ruta: RUTAS.usuarios,
        simbolo: '☺',
        rolesPermitidos: [ROLES.ADMINISTRADOR],
      },
    ],
  },
];

/** Devuelve los grupos de menú visibles para el rol indicado. */
export function menuParaRol(rol: string | undefined): GrupoNavegacion[] {
  if (!rol) {
    return [];
  }

  return MENU.map((grupo) => ({
    titulo: grupo.titulo,
    elementos: grupo.elementos.filter((elemento) => elemento.rolesPermitidos.includes(rol)),
  })).filter((grupo) => grupo.elementos.length > 0);
}
