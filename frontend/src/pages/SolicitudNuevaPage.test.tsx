import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SolicitudNuevaPage } from './SolicitudNuevaPage';

const catalogos = vi.hoisted(() => ({
  listarCarreras: vi.fn(),
  listarMallas: vi.fn(),
  listarAsignaturas: vi.fn(),
}));

vi.mock('../api', async (importOriginal) => {
  const real = await importOriginal<typeof import('../api')>();
  return {
    ...real,
    carrerasApi: { ...real.carrerasApi, listar: catalogos.listarCarreras },
    mallasApi: { ...real.mallasApi, listar: catalogos.listarMallas },
    asignaturasApi: { ...real.asignaturasApi, listar: catalogos.listarAsignaturas },
  };
});

vi.mock('../hooks/useAuth', () => ({
  useAuth: () => ({ usuario: { rol: 'OPERADOR' } }),
}));

describe('Registro de materias de origen', () => {
  beforeEach(() => {
    catalogos.listarCarreras.mockResolvedValue([
      { id: 1, codigo: 'SOFT', nombre: 'Software', facultad: 'FISEI', activa: true },
      { id: 2, codigo: 'SIST', nombre: 'Sistemas', facultad: 'FISEI', activa: true },
    ]);
    catalogos.listarMallas.mockResolvedValue([
      { id: 10, carreraId: 1, carreraCodigo: 'SOFT', version: '2026', anio: 2026, fuente: '', rutaNormativa: null, vigente: true, totalAsignaturas: 1 },
      { id: 20, carreraId: 2, carreraCodigo: 'SIST', version: '2026', anio: 2026, fuente: '', rutaNormativa: null, vigente: true, totalAsignaturas: 1 },
    ]);
    catalogos.listarAsignaturas.mockImplementation((mallaId: number) => Promise.resolve([
      { id: mallaId, mallaId, codigo: mallaId === 10 ? 'SW201' : 'SI101', nombre: mallaId === 10 ? 'Estructuras de Datos' : 'Introducción a Sistemas', creditos: 4, horas: 192, semestre: 2, planCompleto: true, unidades: [] },
    ]));
  });

  it('permite al operador registrar la fecha certificada y matrícula destino sin valores asumidos', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    const fecha = await screen.findByLabelText(/Fecha de aprobación certificada/);
    const matricula = screen.getByLabelText(/Matrícula en la carrera destino/);
    expect(fecha).toHaveValue('');
    expect(matricula).toHaveProperty('value', '');
    expect(matricula.tagName).toBe('SELECT');
    expect(matricula.querySelectorAll('option')).toHaveLength(4);

    await user.type(fecha, '2025-07-15');
    await user.selectOptions(matricula, '3');
    expect(fecha).toHaveValue('2025-07-15');
    expect(matricula).toHaveProperty('value', '3');
  });

  it('al ingresar un código de origen del catálogo completa el nombre de la materia', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    expect(screen.queryByLabelText(/Nota obtenida/)).not.toBeInTheDocument();
    await user.selectOptions(await screen.findByLabelText(/Carrera de origen/), 'SOFT');
    await waitFor(() => expect(catalogos.listarAsignaturas).toHaveBeenCalledWith(10));
    const codigo = await screen.findByLabelText(/Código de origen/);
    expect(codigo.tagName).toBe('SELECT');
    expect(codigo).toHaveTextContent('SW201 — Estructuras de Datos');
    await user.selectOptions(codigo, 'SW201');

    await waitFor(() => expect(screen.getByLabelText(/Nombre de origen/)).toHaveValue('Estructuras de Datos'));
  });

  it('muestra las materias de la carrera de origen también en un trámite externo', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    await user.selectOptions(screen.getByLabelText(/Tipo de trámite/), 'EXTERNO');
    await user.selectOptions(await screen.findByLabelText(/Carrera de origen/), 'SOFT');
    await waitFor(() => expect(catalogos.listarAsignaturas).toHaveBeenCalledWith(10));

    const codigo = await screen.findByLabelText(/Código de origen/);
    expect(codigo.tagName).toBe('SELECT');
    expect(codigo).toHaveTextContent('SW201 — Estructuras de Datos');
    await user.selectOptions(codigo, 'SW201');
    expect(screen.getByLabelText(/Nombre de origen/)).toHaveValue('Estructuras de Datos');
  });

  it('muestra los códigos cuando carga el catálogo y completa el nombre seleccionado', async () => {
    const user = userEvent.setup();
    let liberarCatalogo: ((materias: Awaited<ReturnType<typeof catalogos.listarAsignaturas>>) => void) | undefined;
    catalogos.listarAsignaturas.mockImplementation((mallaId: number) =>
      mallaId === 10
        ? new Promise((resolve) => { liberarCatalogo = resolve; })
        : Promise.resolve([]),
    );
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    await user.selectOptions(await screen.findByLabelText(/Carrera de origen/), 'SOFT');
    const codigo = await screen.findByLabelText(/Código de origen/);
    expect(codigo).toBeDisabled();
    await waitFor(() => expect(liberarCatalogo).toBeDefined());
    liberarCatalogo?.([
      { id: 10, mallaId: 10, codigo: 'SW201', nombre: 'Estructuras de Datos', creditos: 4, horas: 192, semestre: 2, planCompleto: true, unidades: [] },
    ]);

    await screen.findByRole('option', { name: 'SW201 — Estructuras de Datos' });
    await user.selectOptions(codigo, 'SW201');
    expect(screen.getByLabelText(/Nombre de origen/)).toHaveValue('Estructuras de Datos');
  });
});
