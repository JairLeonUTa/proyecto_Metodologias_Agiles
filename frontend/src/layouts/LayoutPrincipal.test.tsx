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

describe('LayoutPrincipal — navegación simple según rol', () => {
  it('el administrador ve Solicitudes y la administración (reglas, contenidos y usuarios) en su tarjeta', () => {
    renderConRol(ROLES.ADMINISTRADOR);

    expect(screen.getByRole('button', { name: /Solicitudes/ })).toBeInTheDocument();
    expect(screen.getByText('Usuarios y roles')).toBeInTheDocument();
    expect(screen.getByText('Configurar mínimo (80 %)')).toBeInTheDocument();
    expect(screen.getByText('Asignaturas y contenidos')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Catálogo académico|Evaluación|Expedientes/ })).toBeNull();
  });

  it('el docente solo ve Solicitudes (las que tiene asignadas), sin administración', () => {
    renderConRol(ROLES.DOCENTE);

    expect(screen.getByRole('button', { name: /Solicitudes/ })).toBeInTheDocument();
    expect(screen.queryByText('Usuarios y roles')).toBeNull();
    expect(screen.queryByText('Configurar mínimo (80 %)')).toBeNull();
  });

  it('el operador solo ve Solicitudes (registrar y listar), sin administración', () => {
    renderConRol(ROLES.OPERADOR);

    expect(screen.getByRole('button', { name: /Solicitudes/ })).toBeInTheDocument();
    expect(screen.queryByText('Usuarios y roles')).toBeNull();
  });
});
