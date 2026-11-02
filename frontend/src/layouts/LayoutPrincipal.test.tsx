import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect } from 'vitest';
import { LayoutPrincipal } from './LayoutPrincipal';
import { ContextoAuth } from '../context/AuthContext';
import type { ContextoAutenticacion } from '../context/AuthContext';
import { ROLES } from '../types/comunes';
import type { Usuario } from '../types/seguridad';

/** Monta el shell con un usuario del rol indicado, sin tocar el backend. */
function renderConRol(rol: string): void {
  const usuario: Usuario = {
    id: 1,
    correoInstitucional: 'persona@uta.edu.ec',
    nombreCompleto: 'Persona Prueba',
    rol,
    rolId: 1,
    activo: true,
    fechaCreacionUtc: '2026-01-01T00:00:00Z',
    ultimoAccesoUtc: null,
  };

  const contexto: ContextoAutenticacion = {
    usuario,
    token: 'token-de-prueba',
    autenticado: true,
    restaurando: false,
    motivoCierre: null,
    iniciarSesion: async () => usuario,
    cerrarSesion: () => undefined,
    limpiarMotivoCierre: () => undefined,
    tieneRol: (...roles: string[]) => roles.includes(rol),
  };

  render(
    <ContextoAuth.Provider value={contexto}>
      <MemoryRouter>
        <LayoutPrincipal />
      </MemoryRouter>
    </ContextoAuth.Provider>,
  );
}

describe('LayoutPrincipal — navegación según rol', () => {
  it('el administrador ve administración en la tarjeta de usuario', () => {
    renderConRol(ROLES.ADMINISTRADOR);

    expect(screen.getByText('Usuarios y roles')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /General/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Expedientes/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Catálogo académico/ })).toBeInTheDocument();
  });

  it('el docente no ve administración ni expedientes', () => {
    renderConRol(ROLES.DOCENTE);

    expect(screen.queryByText('Usuarios y roles')).toBeNull();
    expect(screen.queryByRole('button', { name: /Expedientes/ })).toBeNull();
    expect(screen.getByRole('button', { name: /General/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Catálogo académico/ })).toBeInTheDocument();
  });

  it('el operador ve expedientes pero no administración', () => {
    renderConRol(ROLES.OPERADOR);

    expect(screen.queryByText('Usuarios y roles')).toBeNull();
    expect(screen.getByRole('button', { name: /Expedientes/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /General/ })).toBeInTheDocument();
  });
});
