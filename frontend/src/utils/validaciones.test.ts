import { describe, expect, it } from 'vitest';
import { CONTENIDO_MINIMO_REGLAMENTO, validarParametros } from './validaciones';

describe('Contenido mínimo para homologar', () => {
  it('el reglamento fija 80 %', () => {
    expect(CONTENIDO_MINIMO_REGLAMENTO).toBe(80);
  });

  it('acepta 80 % o más', () => {
    expect(validarParametros(80, 15, 75)).toBeNull();
    expect(validarParametros(90, 15, 75)).toBeNull();
    expect(validarParametros(100, 15, 75)).toBeNull();
  });

  it('no deja bajar del 80 % y lo explica sin símbolos', () => {
    const error = validarParametros(79.99, 15, 75);
    expect(error).toContain('no puede ser menor que 80 %');
    expect(error).toContain('reglamento');
    expect(validarParametros(50, 15, 75)).not.toBeNull();
  });

  it('sigue rechazando valores fuera de 0 a 100', () => {
    expect(validarParametros(120, 15, 75)).toContain('entre 0 y 100');
  });
});
