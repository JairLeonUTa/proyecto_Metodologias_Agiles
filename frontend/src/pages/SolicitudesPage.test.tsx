import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SolicitudesPage } from './SolicitudesPage';

const api = vi.hoisted(() => ({ listar: vi.fn(), obtenerSolicitud: vi.fn() }));

vi.mock('../api', async (importOriginal) => {
  const real = await importOriginal<typeof import('../api')>();
  return {
    ...real,
    expedientesApi: { ...real.expedientesApi, listar: api.listar },
    extraccionesApi: { ...real.extraccionesApi, obtenerSolicitud: api.obtenerSolicitud },
  };
});

vi.mock('../hooks/useAuth', () => ({
  useAuth: () => ({ usuario: { rol: 'DOCENTE', correoInstitucional: 'hnaranjo@uta.edu.ec', nombreCompleto: 'Ing. Naranjo' } }),
}));

const expediente = (id: string, estudiante: string) => ({
  id,
  codigo: `EXP-${id}`,
  numeroCertificado: `SIHA-${id}`,
  estudianteNombre: estudiante,
  carreraOrigenNombre: 'Tecnologías de la Información',
  carreraDestinoNombre: 'Software',
  fechaCreacionUtc: '2026-11-13T10:00:00Z',
  revisores: [{ nombre: 'Ing. Naranjo', correo: 'hnaranjo@uta.edu.ec' }],
  asignaturas: [{ periodoAcademico: 'Abr-Sep 2025', solicitudNumero: 1 }],
});

const analisis = (respondio: boolean) => ({
  estado: 'ANALIZADO',
  estadoRevision: respondio ? 'EN_REVISION' : 'PENDIENTE',
  revisores: [
    { nombre: 'Ing. Naranjo', correo: 'hnaranjo@uta.edu.ec', respondio },
    { nombre: 'Ing. Vaca', correo: 'mvaca@uta.edu.ec', respondio: false },
  ],
  resultado: { evaluaciones: [] },
});

const abrir = () => render(<MemoryRouter><SolicitudesPage /></MemoryRouter>);

describe('Solicitudes del docente revisor', () => {
  beforeEach(() => {
    window.localStorage.clear();
    api.listar.mockResolvedValue([expediente('1', 'Ana Pérez'), expediente('2', 'Luis Mora')]);
    api.obtenerSolicitud.mockImplementation(async (id: string) => analisis(id === '1'));
  });

  it('la primera vez avisa de las que esperan su decisión como nuevas', async () => {
    abrir();

    expect(await screen.findByText('Se le asignó una solicitud nueva')).toBeInTheDocument();
    expect(screen.getByText(/Luis Mora\. Ya traen el análisis/)).toBeInTheDocument();
    expect(screen.getAllByText('Nueva')).toHaveLength(1);
    expect(screen.getAllByText('Falta su decisión')).toHaveLength(1);
  });

  it('«Entendido» las marca como vistas y queda el recordatorio de las pendientes', async () => {
    const user = userEvent.setup();
    abrir();

    await user.click(await screen.findByRole('button', { name: 'Entendido' }));

    expect(screen.queryByText('Se le asignó una solicitud nueva')).not.toBeInTheDocument();
    expect(screen.getByText('Tiene 1 solicitud esperando su decisión.')).toBeInTheDocument();
  });

  it('en la siguiente visita solo avisa de las asignadas después', async () => {
    window.localStorage.setItem('siha.solicitudes-vistas.hnaranjo@uta.edu.ec', JSON.stringify(['1', '2']));
    api.listar.mockResolvedValue([expediente('1', 'Ana Pérez'), expediente('2', 'Luis Mora'), expediente('3', 'Sara Fiallos')]);
    abrir();

    expect(await screen.findByText('Se le asignó una solicitud nueva')).toBeInTheDocument();
    expect(screen.getByText(/Sara Fiallos\. Ya traen el análisis/)).toBeInTheDocument();
  });
});
