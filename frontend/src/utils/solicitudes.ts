import type { Expediente } from '../types/expedientes';

/**
 * Solo las solicitudes del certificado SIHA actual (con período académico en el historial). Las del
 * formato anterior no se pueden analizar ni decidir y se ocultan para no confundir.
 */
export function esFormatoActual(e: Expediente): boolean {
  return Boolean(e.numeroCertificado) && e.asignaturas.length > 0 && e.asignaturas.every((a) => Boolean(a.periodoAcademico));
}

const claveVistas = (correo: string): string => `siha.solicitudes-vistas.${correo.trim().toLowerCase()}`;

/**
 * Solicitudes que el docente ya vio en la lista (se guardan en este navegador). Sirve para avisarle de
 * las que se le asignaron desde su última visita. Si el navegador no permite guardar, todo cuenta como visto
 * después de la primera carga de la página.
 */
export function leerVistas(correo: string): Set<string> | null {
  try {
    const crudo = window.localStorage.getItem(claveVistas(correo));
    if (crudo === null) return null;
    const lista = JSON.parse(crudo) as unknown;
    return new Set(Array.isArray(lista) ? lista.map(String) : []);
  } catch {
    return null;
  }
}

export function guardarVistas(correo: string, ids: Iterable<string>): void {
  try {
    window.localStorage.setItem(claveVistas(correo), JSON.stringify([...new Set(ids)]));
  } catch {
    /* Almacenamiento no disponible: el aviso de nuevas solo dura esta visita. */
  }
}

/**
 * Ids de las solicitudes nuevas para el docente: las que no estaban en su última visita. La primera vez
 * (sin registro previo) son nuevas todas las que todavía esperan su decisión.
 */
export function solicitudesNuevas(
  ids: string[],
  vistas: Set<string> | null,
  pendientesDeDecision: Set<string>,
): Set<string> {
  if (vistas === null) return new Set(ids.filter((id) => pendientesDeDecision.has(id)));
  return new Set(ids.filter((id) => !vistas.has(id)));
}
