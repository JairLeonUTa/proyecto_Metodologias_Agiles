import { describe, expect, it } from 'vitest';
import type { Expediente } from '../types/expedientes';
import { esFormatoActual } from './solicitudes';

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
