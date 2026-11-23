import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UsuariosPage } from './UsuariosPage';

const api = vi.hoisted(() => ({ listar: vi.fn(), roles: vi.fn(), crear: vi.fn(), reset: vi.fn() }));

vi.mock('../api', async (importOriginal) => {
  const real = await importOriginal<typeof import('../api')>();
  return {
    ...real,
    usuariosApi: { ...real.usuariosApi, listar: api.listar, crear: api.crear, restablecerPassword: api.reset },
    rolesApi: { ...real.rolesApi, listar: api.roles },
  };
});

const usuario = { id: 15, correoInstitucional: 'ana@uta.edu.ec', nombreCompleto: 'Ana Pérez', rol: 'DOCENTE', activo: true, ultimoAccesoUtc: null, fechaCreacionUtc: '2026-01-01T00:00:00Z' };
const abrir = () => render(<MemoryRouter><UsuariosPage /></MemoryRouter>);

describe('UsuariosPage contraseñas', () => {
  beforeEach(() => {
    api.listar.mockResolvedValue([usuario]);
    api.roles.mockResolvedValue([{ id: 1, nombre: 'ADMINISTRADOR', descripcion: 'Admin' }, { id: 2, nombre: 'OPERADOR', descripcion: 'Operador' }, { id: 3, nombre: 'DOCENTE', descripcion: 'Docente' }]);
    api.crear.mockResolvedValue({});
    api.reset.mockResolvedValue(undefined);
  });

  it('rechaza contraseñas distintas sin llamar a crear', async () => {
    const user = userEvent.setup();
    abrir();
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    await user.type(await screen.findByLabelText(/^Correo institucional/), 'nueva@uta.edu.ec');
    await user.type(await screen.findByLabelText(/^Nombre completo/), 'Nueva Cuenta');
    await user.type(await screen.findByLabelText(/^Contraseña inicial/), 'Clave1234');
    await user.type(await screen.findByLabelText(/^Confirmar contraseña/), 'OtraClave123');
    await user.click(screen.getAllByRole('button', { name: 'Crear cuenta' }).at(-1)!);
    expect(await screen.findByText('Las contraseñas no coinciden.')).toBeInTheDocument();
    expect(api.crear).not.toHaveBeenCalled();
  });

  it('preselecciona OPERADOR al crear cuenta', async () => {
    const user = userEvent.setup();
    abrir();
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    expect(await screen.findByRole('combobox', { name: 'Rol' })).toHaveValue('OPERADOR');
  });

  it('Mostrar cambia el campo a texto', async () => {
    const user = userEvent.setup();
    abrir();
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }));
    const password = await screen.findByLabelText(/^Contraseña inicial/);
    await user.click(screen.getAllByRole('button', { name: 'Mostrar' })[0]);
    expect(password).toHaveAttribute('type', 'text');
  });

  it('restablece contraseña con PUT y muestra éxito', async () => {
    const user = userEvent.setup();
    abrir();
    await user.click(await screen.findByRole('button', { name: 'Restablecer contraseña' }));
    await user.type(await screen.findByLabelText(/^Nueva contraseña/), 'OtraClave123');
    await user.type(await screen.findByLabelText(/^Confirmar contraseña/), 'OtraClave123');
    await user.click(screen.getByRole('button', { name: 'Guardar contraseña' }));
    await waitFor(() => expect(api.reset).toHaveBeenCalledWith(15, 'OtraClave123'));
    expect(await screen.findByText('Contraseña actualizada. Comuníquela a la persona por un medio seguro.')).toBeInTheDocument();
  });
});
