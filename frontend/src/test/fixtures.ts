import type { Extraccion } from '../types/analisis';

/** Extracción base del mockup M-02 (silabo-bases-datos.pdf, confianza 0,68). */
export function extraccionDePrueba(cambios: Partial<Extraccion> = {}): Extraccion {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    expedienteId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
    documentoId: 'doc-1',
    codigoAsignaturaOrigen: 'BD101',
    nombreAsignaturaOrigen: 'Bases de Datos I',
    creditos: null,
    horas: 64,
    confianza: 0.68,
    estado: 'PENDIENTE_CONFIRMACION',
    requiereConfirmacion: true,
    unidades: [
      { numero: 1, nombre: 'Introducción a las bases de datos', horas: 8, contenidos: 'Conceptos' },
      { numero: 2, nombre: 'Modelo entidad-relación', horas: 12, contenidos: 'ER' },
    ],
    creadoUtc: '2026-10-22T14:42:00Z',
    confirmadoUtc: null,
    confirmadoPor: null,
    observacion: null,
    ...cambios,
  };
}
