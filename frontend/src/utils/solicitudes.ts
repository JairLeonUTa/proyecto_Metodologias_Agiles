import type { Expediente } from '../types/expedientes';

/**
 * Solo las solicitudes del certificado SIHA actual (con período académico en el historial). Las del
 * formato anterior no se pueden analizar ni decidir y se ocultan para no confundir.
 */
export function esFormatoActual(e: Expediente): boolean {
  return Boolean(e.numeroCertificado) && e.asignaturas.length > 0 && e.asignaturas.every((a) => Boolean(a.periodoAcademico));
}
