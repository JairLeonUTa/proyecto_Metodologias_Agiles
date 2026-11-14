import { describe, expect, it } from 'vitest';
import type { Expediente } from '../types/expedientes';
import { esFormatoActual, guardarVistas, leerVistas, solicitudesNuevas } from './solicitudes';

describe('Solicitudes nuevas para el docente', () => {
  it('la primera vez son nuevas las que esperan su decisión', () => {
    expect([...solicitudesNuevas(['a', 'b', 'c'], null, new Set(['b']))]).toEqual(['b']);
  });

  it('después, son nuevas las que no estaban en la última visita', () => {
    expect([...solicitudesNuevas(['a', 'b', 'c'], new Set(['a', 'b']), new Set())]).toEqual(['c']);
  });

  it('recuerda las vistas por docente en este navegador', () => {
    guardarVistas('HNaranjo@uta.edu.ec', ['x', 'y', 'x']);
    expect([...(leerVistas('hnaranjo@uta.edu.ec') ?? [])]).toEqual(['x', 'y']);
    expect(leerVistas('otro@uta.edu.ec')).toBeNull();
  });
});

const con = (asignaturas: Array<{ periodoAcademico?: string | null }>, numeroCertificado: string | null = 'SIHA-1') =>
  ({ numeroCertificado, asignaturas }) as unknown as Expediente;

describe('Solicitudes visibles', () => {
  it('muestra las del certificado actual (con período académico)', () => {
    expect(esFormatoActual(con([{ periodoAcademico: 'Abr-Sep 2025' }]))).toBe(true);
  });

  it('oculta las del formato anterior y las que no vienen de un certificado', () => {
    expect(esFormatoActual(con([{ periodoAcademico: null }]))).toBe(false);
    expect(esFormatoActual(con([]))).toBe(false);
    expect(esFormatoActual(con([{ periodoAcademico: 'Abr-Sep 2025' }], null))).toBe(false);
  });
});
