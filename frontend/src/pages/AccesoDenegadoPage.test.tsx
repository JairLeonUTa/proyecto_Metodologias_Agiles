import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { AccesoDenegadoPage, nombrePluralRol } from './AccesoDenegadoPage';

vi.mock('../hooks/useAuth', () => ({
  useAuth: () => ({ usuario: { rol: 'OPERADOR' }, cerrarSesion: vi.fn() }),
}));

describe('AccesoDenegadoPage', () => {
  it('explica los roles permitidos en lenguaje sencillo', () => {
    render(<MemoryRouter initialEntries={[{ pathname: '/acceso-denegado', state: { rutaIntentada: '/administracion/usuarios', rolesRequeridos: ['ADMINISTRADOR'] } }]}><AccesoDenegadoPage /></MemoryRouter>);
    expect(screen.getByText(/solo para Administradores/)).toBeInTheDocument();
    expect(screen.getByText(/Usted entró como Operador/)).toBeInTheDocument();
    expect(screen.queryByText(/RBAC|\/administracion\/usuarios/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cambiar de cuenta' })).toBeInTheDocument();
  });
});

describe('nombrePluralRol', () => {
  it('forma bien el plural de cada rol', () => {
    expect(nombrePluralRol('ADMINISTRADOR')).toBe('Administradores');
    expect(nombrePluralRol('OPERADOR')).toBe('Operadores');
    expect(nombrePluralRol('DOCENTE')).toBe('Docentes');
  });
});
