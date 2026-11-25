import { describe, expect, it } from 'vitest';
import { anioDe, periodosSugeridos } from './periodosUta';

describe('Períodos académicos UTA sugeridos', () => {
  it('toma el año más alto del nombre, como el lector del certificado', () => {
    expect(anioDe('OCTUBRE 2015 - MARZO 2016')).toBe(2016);
    expect(anioDe('ABRIL - SEPTIEMBRE 2022')).toBe(2022);
    expect(anioDe('Sin año')).toBe(0);
  });

  it('ordena del más reciente al más antiguo y no sugiere períodos que aún no empiezan', () => {
    const lista = periodosSugeridos(new Date(2026, 10, 25));
    expect(lista[0]).toBe('JULIO - DICIEMBRE 2026');
    expect(lista).toContain('AGOSTO 2025 - ENERO 2026');

    const antes = periodosSugeridos(new Date(2026, 5, 15));
    expect(antes).not.toContain('JULIO - DICIEMBRE 2026');
    expect(antes[0]).toBe('ENERO - JULIO 2026');
  });

  it('solo sugiere períodos vigentes (máximo 10 años)', () => {
    const lista = periodosSugeridos(new Date(2026, 10, 25));
    expect(lista.at(-1)).toBe('OCTUBRE 2015 - MARZO 2016');
    expect(lista).not.toContain('ABRIL - SEPTIEMBRE 2015');

    expect(periodosSugeridos(new Date(2027, 2, 1))).not.toContain('OCTUBRE 2015 - MARZO 2016');
  });
});
