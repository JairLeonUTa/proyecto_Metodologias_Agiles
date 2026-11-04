import { ROLES } from '../types/comunes';
import type { NombreIcono } from '../components/ui/Icono';

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
  solicitudes: '/solicitudes',
  detalleSolicitud: '/solicitudes/:id',
  expedientes: '/expedientes',
  consultaExpediente: '/expedientes/consulta',
  detalleExpediente: '/expedientes/:id',
  // Sprint 2 — Agente analizador (MS C)
  bandejaProcesamiento: '/procesamiento',
  confirmarExtraccion: '/procesamiento/:id/confirmar',
  estadoExtraccion: '/procesamiento/:id/estado',
  bandejaAnalisis: '/evaluacion',
  analisisExpediente: '/evaluacion/:id',
} as const;

/** Roles con acceso a todo el sistema (solo para lectura de navegación). */
export const TODOS_LOS_ROLES: string[] = [ROLES.ADMINISTRADOR, ROLES.DOCENTE, ROLES.OPERADOR];

/** Roles que pueden registrar y consultar expedientes. */
export const ROLES_EXPEDIENTES: string[] = [ROLES.OPERADOR, ROLES.ADMINISTRADOR];

/** Roles que evalúan expedientes con el agente analizador (Sprint 2, US-09 a US-11). */
export const ROLES_EVALUACION: string[] = [ROLES.DOCENTE, ROLES.ADMINISTRADOR];

export interface ElementoNavegacion {
  etiqueta: string;
  descripcion: string;
  ruta: string;
  icono: NombreIcono;
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
    titulo: 'Solicitudes',
    elementos: [
      {
        etiqueta: 'Registrar solicitud',
        descripcion: 'Subir el certificado SIHA (PDF); el agente lo analiza y lo asigna a los dos docentes',
        ruta: RUTAS.nuevaSolicitud,
        icono: 'nueva-solicitud',
        rolesPermitidos: ROLES_EXPEDIENTES,
      },
      {
        etiqueta: 'Solicitudes',
        descripcion: 'Estimación del agente y respuesta de los docentes revisores',
        ruta: RUTAS.solicitudes,
        icono: 'bandeja',
        rolesPermitidos: TODOS_LOS_ROLES,
      },
    ],
  },
  {
    titulo: 'Administración',
    elementos: [
      {
        etiqueta: 'Reglas',
        descripcion: 'Contenido mínimo para homologar (80 %)',
        ruta: RUTAS.reglas,
        icono: 'reglas',
        rolesPermitidos: [ROLES.ADMINISTRADOR],
      },
      {
        etiqueta: 'Asignaturas y contenidos',
        descripcion: 'Mallas de cada carrera que usa el agente para comparar',
        ruta: RUTAS.asignaturas,
        icono: 'asignaturas',
        rolesPermitidos: [ROLES.ADMINISTRADOR],
      },
      {
        etiqueta: 'Carreras y mallas',
        descripcion: 'Carreras de la FISEI y sus versiones de malla',
        ruta: RUTAS.mallas,
        icono: 'mallas',
        rolesPermitidos: [ROLES.ADMINISTRADOR],
      },
      {
        etiqueta: 'Usuarios y roles',
        descripcion: 'Operadores y docentes revisores',
        ruta: RUTAS.usuarios,
        icono: 'usuarios',
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
