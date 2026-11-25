import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ErrorApi } from '../api/httpClient';
import { RegistroManualPage } from './RegistroManualPage';

const api = vi.hoisted(() => ({
  opcionesManual: vi.fn(),
  validarManual: vi.fn(),
  registrarManual: vi.fn(),
  listarCarreras: vi.fn(),
}));

vi.mock('../api', async (importOriginal) => {
  const real = await importOriginal<typeof import('../api')>();
  return {
    ...real,
    expedientesApi: {
      ...real.expedientesApi,
      opcionesManual: api.opcionesManual,
      validarManual: api.validarManual,
      registrarManual: api.registrarManual,
    },
    carrerasApi: { ...real.carrerasApi, listar: api.listarCarreras },
  };
});

vi.mock('../hooks/useAuth', () => ({
  useAuth: () => ({ usuario: { rol: 'OPERADOR', nombreCompleto: 'Operador Prueba' } }),
}));

const CERTIFICADO = {
  numeroCertificado: 'MANUAL-20261124-ABC123',
  fechaEmision: '2026-11-24',
  emitidoPor: 'Registro manual (Operador Prueba)',
  estudianteNombre: 'Torres Vega Martín',
  estudianteIdentificacion: '1805123456',
  tipoTramite: 'INTERNO',
  rutaNormativa: 'CAU-P-011',
  institucionOrigen: 'Universidad Técnica de Ambato',
  carreraOrigen: 'Tecnologías de la Información',
  carreraDestino: 'Software',
  revisores: [
    { nombre: 'Ing. Hernán Naranjo', correo: 'hnaranjo@uta.edu.ec' },
    { nombre: 'Ing. María Vaca', correo: 'mvaca@uta.edu.ec' },
  ],
  materias: [{ nombre: 'Física', nivel: 1, periodoAcademico: 'Abr-Sep 2025', nota: 8.5, aprobada: true }],
  solicitudes: [{ numero: 1, materiasOrigen: ['Física'], materiaDestino: 'Física' }],
};

describe('Registro manual de una solicitud (sin PDF)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.listarCarreras.mockResolvedValue([
      { id: 1, codigo: 'TI', nombre: 'Tecnologías de la Información', facultad: 'FISEI', activa: true },
      { id: 2, codigo: 'SW', nombre: 'Software', facultad: 'FISEI', activa: true },
    ]);
    api.opcionesManual.mockResolvedValue({
      docentes: [
        { nombre: 'Ing. Hernán Naranjo', correo: 'hnaranjo@uta.edu.ec' },
        { nombre: 'Ing. María Vaca', correo: 'mvaca@uta.edu.ec' },
      ],
      materiasOrigen: [],
      materiasDestino: ['FISICA'],
    });
  });

  const abrir = () =>
    render(
      <MemoryRouter>
        <RegistroManualPage />
      </MemoryRouter>,
    );

  const llenar = async (user: ReturnType<typeof userEvent.setup>) => {
    await user.type(screen.getByLabelText(/Nombres y apellidos completos/), 'Torres Vega Martín');
    await user.type(screen.getByLabelText(/Cédula/), '1805123456');
    await screen.findAllByRole('option', { name: 'Software' });
    await user.selectOptions(screen.getByLabelText(/Carrera de origen/), 'Tecnologías de la Información');
    await user.selectOptions(screen.getByLabelText(/Carrera a la que quiere pasar/), 'Software');
    await screen.findAllByRole('option', { name: /Hernán Naranjo/ });
    await user.selectOptions(screen.getByLabelText(/Docente revisor 1/), 'hnaranjo@uta.edu.ec');
    await user.selectOptions(screen.getByLabelText(/Docente revisor 2/), 'mvaca@uta.edu.ec');
    await user.type(screen.getByLabelText('Materia'), 'Física');
    await user.type(screen.getByLabelText('Período académico'), 'Abr-Sep 2025');
    await user.type(screen.getByLabelText('Nota'), '8,5');
    await user.selectOptions(screen.getByLabelText('Materia aprobada'), 'Física');
    await user.type(screen.getByLabelText(/Homologar por/), 'Física');
  };

  it('revisa los datos antes de registrar y luego registra la solicitud', async () => {
    const user = userEvent.setup();
    api.validarManual.mockResolvedValue(CERTIFICADO);
    api.registrarManual.mockResolvedValue({
      expediente: { id: 'e9', codigo: 'EXP-20261124-ABC123' },
      certificado: CERTIFICADO,
      publicado: true,
      mensaje: 'Enviada al agente.',
    });
    abrir();

    await llenar(user);
    const registrar = screen.getByRole('button', { name: 'Registrar y enviar solicitud' });
    expect(registrar).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Revisar los datos' }));
    expect(await screen.findByText('Datos revisados: todo está correcto')).toBeInTheDocument();
    expect(api.validarManual).toHaveBeenCalledWith(
      expect.objectContaining({
        estudianteNombre: 'Torres Vega Martín',
        cedula: '1805123456',
        tipoTramite: 'Cambio de carrera',
        carreraDestino: 'Software',
        materias: [{ nombre: 'Física', nivel: 1, periodoAcademico: 'Abr-Sep 2025', nota: 8.5, aprobada: true }],
        solicitudes: [{ materiasOrigen: ['Física'], materiaDestino: 'Física' }],
      }),
    );

    await user.click(screen.getByRole('button', { name: 'Registrar y enviar solicitud' }));
    expect(await screen.findByText('Expediente EXP-20261124-ABC123')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver la solicitud y su análisis' })).toHaveAttribute('href', '/solicitudes/e9');
  });

  it('si faltan datos muestra la lista de verificación y no deja registrar', async () => {
    const user = userEvent.setup();
    api.validarManual.mockRejectedValue(
      new ErrorApi('El certificado tiene campos sin llenar.', 422, [], {
        tipo: 'CAMPO_VACIO',
        secciones: [
          { seccion: 'Estudiante', ok: false, problemas: ['Falta la cédula del estudiante.'] },
          { seccion: 'Revisores', ok: true, problemas: [] },
        ],
      }),
    );
    abrir();

    await user.click(screen.getByRole('button', { name: 'Revisar los datos' }));

    const lista = await screen.findByRole('list', { name: 'Revisión del certificado por sección' });
    expect(within(lista).getByText('Falta la cédula del estudiante.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Registrar y enviar solicitud' })).toBeDisabled();
    expect(api.registrarManual).not.toHaveBeenCalled();
  });

  it('una nota menor que 7 marca la materia como reprobada', async () => {
    const user = userEvent.setup();
    abrir();

    await user.type(screen.getByLabelText('Nota'), '5,9');

    expect(screen.getByLabelText('Estado')).toHaveValue('no');
  });
});
