import { describe, expect, it } from 'vitest';
import type { SolicitudAnalisis } from '../types/analisis';
import { crearResolucionPdf, nombreArchivoResolucion } from './resolucionPdf';

const solicitud = {
  expedienteId: 'e1',
  codigoExpediente: 'EXP-20261125-ABC123',
  numeroCertificado: 'SIHA-2026-0500',
  estudianteNombre: 'Estudiante Caso Profesor',
  tipoTramite: 'Cambio de carrera',
  rutaNormativa: 'CAU-P-011',
  institucionOrigen: 'Universidad Técnica de Ambato',
  carreraOrigen: 'Tecnologías de la Información',
  carreraDestino: 'Software',
  historial: [],
  revisores: [
    { nombre: 'Ing. Carlos Paredes', correo: 'cparedes@uta.edu.ec', respondio: true },
    { nombre: 'Ing. María Vaca', correo: 'mvaca@uta.edu.ec', respondio: true },
  ],
  estado: 'ANALIZADO',
  mensaje: null,
  analizadoUtc: null,
  resultado: null,
  dictamenes: [
    { correoDocente: 'cparedes@uta.edu.ec', nombreDocente: 'Ing. Carlos Paredes', comentario: null, fechaUtc: '', decisiones: [
      { codigoDestino: 'FIS', aprobada: true, observacion: null }, { codigoDestino: 'ALG', aprobada: true, observacion: null }] },
    { correoDocente: 'mvaca@uta.edu.ec', nombreDocente: 'Ing. María Vaca', comentario: null, fechaUtc: '', decisiones: [
      { codigoDestino: 'FIS', aprobada: true, observacion: null }, { codigoDestino: 'ALG', aprobada: false, observacion: null }] },
  ],
  resultadoFinal: [
    { codigoDestino: 'FIS', nombreDestino: 'Física', estimacion: 'CUMPLE', aprobaciones: 2, rechazos: 0, resultado: 'HOMOLOGADA' },
    { codigoDestino: 'ALG', nombreDestino: 'Álgebra Lineal', estimacion: 'CUMPLE', aprobaciones: 1, rechazos: 1, resultado: 'NO_HOMOLOGADA',
      decisionFinal: { aprobada: false, comentario: 'Falta la unidad de espacios vectoriales.', nombreAdministrador: 'Administrador SIHA', fechaUtc: '' } },
    { codigoDestino: 'CAL', nombreDestino: 'Cálculo Integral', estimacion: 'NO_CUMPLE', aprobaciones: 0, rechazos: 0, resultado: 'NO_HOMOLOGADA', bloqueadaPorCreditos: true },
  ],
  estadoRevision: 'RESUELTA',
  puedeResponder: false,
} as unknown as SolicitudAnalisis;

describe('Resolución de homologación en PDF', () => {
  it('arma un PDF con los datos, la decisión de cada materia y las firmas', () => {
    const doc = crearResolucionPdf(solicitud, new Date('2026-11-25T15:00:00'));
    const pdf = doc.output();

    expect(pdf.startsWith('%PDF-')).toBe(true);
    expect(doc.getNumberOfPages()).toBeGreaterThanOrEqual(1);
    expect(nombreArchivoResolucion(solicitud)).toBe('Resolucion-homologacion-EXP-20261125-ABC123.pdf');
  });
});
