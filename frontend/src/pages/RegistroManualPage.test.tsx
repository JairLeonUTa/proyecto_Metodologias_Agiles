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
  afinidadManual: vi.fn(),
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
      afinidadManual: api.afinidadManual,
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
    { nombre: 'Ing. Carlos Paredes', correo: 'cparedes@uta.edu.ec' },
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
        { nombre: 'Ing. Carlos Paredes', correo: 'cparedes@uta.edu.ec' },
        { nombre: 'Ing. María Vaca', correo: 'mvaca@uta.edu.ec' },
      ],
      materiasOrigen: ['FISICA', 'ALGEBRA LINEAL', 'CALCULO INTEGRAL'],
      materiasDestino: ['FISICA'],
      mallaOrigen: [
        { nombre: 'FISICA', nivel: 1, creditos: 4 },
        { nombre: 'ALGEBRA LINEAL', nivel: 1, creditos: 4 },
        { nombre: 'CALCULO INTEGRAL', nivel: 2, creditos: 4 },
      ],
      mallaDestino: [
        { nombre: 'FISICA', nivel: 1, creditos: 4 },
        { nombre: 'ALGORITMOS Y LOGICA DE PROGRAMACION', nivel: 1, creditos: 5 },
      ],
    });
    api.afinidadManual.mockResolvedValue({ sugerencias: [], solicitudes: [] });
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
    await screen.findAllByRole('option', { name: /Carlos Paredes/ });
    await user.selectOptions(screen.getByLabelText(/Docente revisor 1/), 'cparedes@uta.edu.ec');
    await user.selectOptions(screen.getByLabelText(/Docente revisor 2/), 'mvaca@uta.edu.ec');
    await user.selectOptions(screen.getByLabelText('Materia'), 'Fisica');
    await user.selectOptions(screen.getByLabelText('Período académico'), 'AGOSTO 2025 - ENERO 2026');
    await user.type(screen.getByLabelText('Nota'), '8,5');
    await user.selectOptions(screen.getByLabelText('Materia aprobada'), 'Fisica');
    await user.selectOptions(screen.getByLabelText(/Homologar por/), 'Fisica');
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
        materias: [{ nombre: 'Fisica', nivel: 1, periodoAcademico: 'AGOSTO 2025 - ENERO 2026', nota: 8.5, aprobada: true }],
        solicitudes: [{ materiasOrigen: ['Fisica'], materiaDestino: 'Fisica' }],
      }),
    );

    await user.click(screen.getByRole('button', { name: 'Registrar y enviar solicitud' }));
    expect(api.registrarManual).toHaveBeenCalledWith(expect.objectContaining({ numeroCertificado: 'MANUAL-20261124-ABC123' }));
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

  const elegirCarreras = async (user: ReturnType<typeof userEvent.setup>) => {
    await screen.findAllByRole('option', { name: 'Software' });
    await user.selectOptions(screen.getByLabelText(/Carrera de origen/), 'Tecnologías de la Información');
    await user.selectOptions(screen.getByLabelText(/Carrera a la que quiere pasar/), 'Software');
  };

  it('carga las materias de la malla de origen con su nivel y copia el período', async () => {
    const user = userEvent.setup();
    abrir();
    await elegirCarreras(user);

    await user.selectOptions(await screen.findByLabelText(/Cargar las materias de la malla hasta el nivel/), '2');
    await user.click(screen.getByRole('button', { name: 'Cargar materias de la malla' }));

    const materias = screen.getAllByLabelText('Materia');
    expect(materias.map((m) => (m as HTMLSelectElement).value)).toEqual(['Fisica', 'Algebra lineal', 'Calculo integral']);
    expect(screen.getAllByLabelText('Nivel').map((n) => (n as HTMLSelectElement).value)).toEqual(['1', '1', '2']);

    await user.selectOptions(screen.getAllByLabelText('Período académico')[0], 'AGOSTO 2025 - ENERO 2026');
    const periodos = screen.getAllByLabelText('Período académico').map((p) => (p as HTMLSelectElement).value);
    expect(periodos).toEqual(['AGOSTO 2025 - ENERO 2026', 'AGOSTO 2025 - ENERO 2026', '']);
  });

  it('filtra las materias según el nivel seleccionado y limpia la materia si se cambia a otro nivel', async () => {
    const user = userEvent.setup();
    abrir();
    await elegirCarreras(user);
    await screen.findByLabelText(/Cargar las materias de la malla/);

    const selectorNivel = screen.getByLabelText('Nivel');
    const selectorMateria = screen.getByLabelText('Materia');

    // En nivel 1 solo aparecen las materias de nivel 1
    expect(within(selectorMateria).queryByRole('option', { name: 'Fisica' })).toBeInTheDocument();
    expect(within(selectorMateria).queryByRole('option', { name: 'Algebra lineal' })).toBeInTheDocument();
    expect(within(selectorMateria).queryByRole('option', { name: 'Calculo integral' })).not.toBeInTheDocument();

    await user.selectOptions(selectorMateria, 'Fisica');
    expect(selectorMateria).toHaveValue('Fisica');

    // Al cambiar a nivel 2 se limpia la materia de nivel 1 y solo aparece la de nivel 2
    await user.selectOptions(selectorNivel, '2');
    expect(selectorMateria).toHaveValue('');
    expect(within(selectorMateria).queryByRole('option', { name: 'Fisica' })).not.toBeInTheDocument();
    expect(within(selectorMateria).queryByRole('option', { name: 'Calculo integral' })).toBeInTheDocument();
  });

  it('sugiere la materia de destino y avisa si lo pedido no se relaciona', async () => {
    const user = userEvent.setup();
    api.afinidadManual.mockImplementation(async (consulta: { solicitudes: { materiasOrigen: string[]; materiaDestino: string }[] }) => {
      const pedida = consulta.solicitudes[0];
      if (pedida.materiaDestino === 'Algoritmos y logica de programacion') {
        return {
          sugerencias: [],
          solicitudes: [
            {
              solicitud: 1,
              seRelacionan: false,
              porcentaje: 3,
              mensaje: 'Solicitud 1: «Fisica» no se relaciona con «Algoritmos y logica de programacion».',
              sugerencia: 'Fisica',
            },
          ],
        };
      }
      return pedida.materiasOrigen[0]
        ? { sugerencias: [], solicitudes: [{ solicitud: 1, seRelacionan: true, porcentaje: 60, mensaje: 'Se relacionan: sus temas se parecen un 60 %.', sugerencia: null }] }
        : { sugerencias: [{ materiaOrigen: 'Fisica', materiaDestino: 'Fisica', porcentaje: 60 }], solicitudes: [] };
    });
    abrir();
    await elegirCarreras(user);
    await screen.findByLabelText(/Cargar las materias de la malla/);
    await user.selectOptions(screen.getByLabelText('Materia'), 'Fisica');
    await user.type(screen.getByLabelText('Nota'), '8');

    expect(await screen.findByText(/Sugerencias automáticas/)).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Materia aprobada'), 'Fisica');
    expect(screen.getByLabelText(/Homologar por/)).toHaveValue('Fisica');
    expect(await screen.findByText(/Se relacionan: sus temas se parecen un 60 %/)).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText(/Homologar por/), 'Algoritmos y logica de programacion');
    expect(await screen.findByText('Estas materias no se relacionan')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Usar «Fisica»' }));
    expect(screen.getByLabelText(/Homologar por/)).toHaveValue('Fisica');
  });

  it('al agregar las sugerencias deja elegida la materia de destino aunque cambien las mayúsculas', async () => {
    const user = userEvent.setup();
    api.afinidadManual.mockResolvedValue({
      sugerencias: [{ materiaOrigen: 'Fisica', materiaDestino: 'FISICA', porcentaje: 60 }],
      solicitudes: [],
    });
    abrir();
    await elegirCarreras(user);
    await screen.findByLabelText(/Cargar las materias de la malla/);
    await user.selectOptions(screen.getByLabelText('Materia'), 'Fisica');
    await user.type(screen.getByLabelText('Nota'), '8');

    await user.click(await screen.findByRole('button', { name: 'Agregar todas las sugerencias' }));

    expect(screen.getByLabelText(/Homologar por/)).toHaveValue('Fisica');
  });

  it('ofrece períodos académicos de la UTA precargados en el combobox Período académico', () => {
    abrir();

    const campoPeriodo = screen.getByLabelText('Período académico');
    const opciones = within(campoPeriodo)
      .getAllByRole('option')
      .map((o) => (o as HTMLOptionElement).value)
      .filter(Boolean);
    expect(opciones.length).toBeGreaterThan(10);
    expect(opciones).toContain('AGOSTO 2025 - ENERO 2026');
    expect(opciones).toContain('ABRIL - SEPTIEMBRE 2020');
  });
});
