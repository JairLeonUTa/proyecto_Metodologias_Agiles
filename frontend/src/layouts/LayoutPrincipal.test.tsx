import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect } from 'vitest';
import { LayoutPrincipal } from './LayoutPrincipal';
import { ContextoAuth } from '../context/AuthContext';
import type { ContextoAutenticacion } from '../context/AuthContext';
import { ROLES } from '../types/comunes';
import type { Usuario } from '../types/seguridad';

/** Monta el shell con un usuario del rol indicado, sin tocar el backend. */
function renderConRol(rol: string, ruta = '/'): void {
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
    expiraUtc: null,
    renovarSesion: async () => undefined,
  };

  render(
    <ContextoAuth.Provider value={contexto}>
      <MemoryRouter initialEntries={[ruta]}>
        <LayoutPrincipal />
      </MemoryRouter>
    </ContextoAuth.Provider>,
  );
}

describe('LayoutPrincipal — navegación simple según rol', () => {
  it('el administrador ve las opciones principales sin abrir un menú', () => {
    renderConRol(ROLES.ADMINISTRADOR);

    const navegacion = screen.getByRole('navigation', { name: 'Navegación principal' });
    expect(navegacion).toHaveTextContent('Registrar solicitud');
    expect(navegacion).toHaveTextContent('Solicitudes');
    expect(navegacion).toHaveTextContent('Registro manual');
    expect(navegacion).not.toHaveTextContent('Reglas de homologación');
    expect(navegacion).toHaveTextContent('Usuarios y roles');
  });

  it('el docente no ve Administración ni Usuarios y roles', () => {
    renderConRol(ROLES.DOCENTE);

    const navegacion = screen.getByRole('navigation', { name: 'Navegación principal' });
    expect(navegacion).not.toHaveTextContent('Administración');
    expect(navegacion).not.toHaveTextContent('Usuarios y roles');
  });

  it('marca la ruta actual en el enlace de navegación', () => {
    renderConRol(ROLES.ADMINISTRADOR, '/solicitudes');
    expect(screen.getAllByRole('link', { name: /^Solicitudes/ }).some((enlace) => enlace.getAttribute('aria-current') === 'page')).toBe(true);
  });
});
