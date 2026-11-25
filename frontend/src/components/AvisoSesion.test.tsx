import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AvisoSesion, minutosRestantes } from './AvisoSesion';

const auth = vi.hoisted(() => ({
  expiraUtc: null as string | null,
  renovarSesion: vi.fn(),
  cerrarSesion: vi.fn(),
}));

vi.mock('../hooks/useAuth', () => ({ useAuth: () => auth }));

const enMinutos = (minutos: number): string => new Date(Date.now() + minutos * 60000).toISOString();

describe('Aviso antes de que venza la sesión', () => {
  beforeEach(() => {
    auth.renovarSesion.mockReset();
    auth.cerrarSesion.mockReset();
  });

  it('calcula los minutos que faltan', () => {
    const ahora = Date.parse('2026-11-13T10:00:00Z');
    expect(minutosRestantes('2026-11-13T10:04:10Z', ahora)).toBe(5);
    expect(minutosRestantes('2026-11-13T09:59:00Z', ahora)).toBeLessThanOrEqual(0);
    expect(minutosRestantes(null, ahora)).toBeNull();
  });

  it('no molesta si falta más de 5 minutos', () => {
    auth.expiraUtc = enMinutos(40);
    render(<AvisoSesion />);

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('avisa 5 minutos antes y «Seguir conectado» renueva la sesión', async () => {
    const user = userEvent.setup();
    auth.expiraUtc = enMinutos(3);
    auth.renovarSesion.mockResolvedValue(undefined);
    render(<AvisoSesion />);

    expect(screen.getByRole('alert')).toHaveTextContent('Su sesión se cerrará en 3 minutos');
    await user.click(screen.getByRole('button', { name: 'Seguir conectado' }));

    expect(auth.renovarSesion).toHaveBeenCalledTimes(1);
  });

  it('si ya venció, cierra la sesión con un mensaje claro', () => {
    auth.expiraUtc = enMinutos(-1);
    render(<AvisoSesion />);

    expect(auth.cerrarSesion).toHaveBeenCalledWith(expect.stringContaining('venció'));
  });
});
