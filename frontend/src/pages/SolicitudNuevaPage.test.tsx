import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ListaVerificacion, SolicitudNuevaPage } from './SolicitudNuevaPage';

describe('Lista de verificación del certificado', () => {
  it('muestra los problemas y el estado accesible de una sección con problemas', () => {
    render(<ListaVerificacion secciones={[{ seccion: 'Historial académico', ok: false, problemas: ['Falta una nota'] }]} />);

    expect(screen.getByText('Falta una nota')).toBeInTheDocument();
    expect(screen.getByText('Sección con problemas')).toHaveClass('sr-only');
  });

  it('muestra Sin problemas. y el estado accesible de una sección correcta', () => {
    render(<ListaVerificacion secciones={[{ seccion: 'Datos del estudiante', ok: true, problemas: [] }]} />);

    expect(screen.getByText('Sin problemas.')).toBeInTheDocument();
    expect(screen.getByText('Sección correcta')).toHaveClass('sr-only');
  });
});

const api = vi.hoisted(() => ({
  validarCertificado: vi.fn(),
  registrarCertificado: vi.fn(),
}));

vi.mock('../api', async (importOriginal) => {
  const real = await importOriginal<typeof import('../api')>();
  return {
    ...real,
    expedientesApi: { ...real.expedientesApi, ...api },
  };
});

vi.mock('../hooks/useAuth', () => ({
  useAuth: () => ({ usuario: { rol: 'OPERADOR', nombreCompleto: 'Operador Prueba' } }),
}));

const CERTIFICADO = {
  numeroCertificado: 'SIHA-2026-0147',
  fechaEmision: '2026-10-30',
  emitidoPor: 'Secretaría Académica FISEI',
  estudianteNombre: 'Pérez Gómez Ana Lucía',
  estudianteIdentificacion: '1804567890',
  tipoTramite: 'INTERNO',
  rutaNormativa: 'CAU-P-011',
  institucionOrigen: 'Universidad Técnica de Ambato',
  carreraOrigen: 'Tecnologías de la Información',
  carreraDestino: 'Software',
  revisores: [
    { nombre: 'Ing. Carlos Paredes', correo: 'cparedes@uta.edu.ec' },
    { nombre: 'Ing. María Vaca', correo: 'mvaca@uta.edu.ec' },
  ],
  materias: [
    { nombre: 'Fundamentos de Programación', nivel: 1, periodoAcademico: 'Abr-Sep 2025', nota: 8.9, aprobada: true },
    { nombre: 'Medidas Eléctricas', nivel: 2, periodoAcademico: 'Oct 2025-Mar 2026', nota: 5.9, aprobada: false },
  ],
  solicitudes: [{ numero: 1, materiasOrigen: ['Fundamentos de Programación'], materiaDestino: 'Algoritmos y Lógica de Programación' }],
};

const pdf = (): File => new File(['%PDF-1.4'], 'certificado.pdf', { type: 'application/pdf' });

describe('Registro con un único certificado SIHA', () => {
  beforeEach(() => {
    api.validarCertificado.mockReset();
    api.registrarCertificado.mockReset();
  });

  it('no pide carreras, materias, fechas ni matrículas: solo el PDF', () => {
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    expect(screen.queryByLabelText(/Carrera de origen/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Fecha de aprobación/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Matrícula/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/revisor/i)).not.toBeInTheDocument();
    expect(screen.getByTestId('certificado-pdf')).toHaveAttribute('type', 'file');
  });

  it('muestra los datos leídos del certificado y lo registra', async () => {
    const user = userEvent.setup();
    api.validarCertificado.mockResolvedValue(CERTIFICADO);
    api.registrarCertificado.mockResolvedValue({
      expediente: { id: 'e1', codigo: 'EXP-1', estado: 'PENDIENTE_CONTENIDOS' },
      certificado: CERTIFICADO,
      publicado: true,
      mensaje: 'Expediente publicado.',
    });
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    await user.upload(screen.getByTestId('certificado-pdf'), pdf());

    expect(await screen.findByText('Pérez Gómez Ana Lucía · 1804567890')).toBeInTheDocument();
    expect(screen.getAllByText('Tecnologías de la Información → Software').length).toBeGreaterThan(1);
    expect(screen.getByText('Algoritmos y Lógica de Programación')).toBeInTheDocument();
    expect(screen.getByText('Abr-Sep 2025')).toBeInTheDocument();
    expect(screen.getByText('8,90')).toBeInTheDocument();
    expect(screen.getByText('Reprobada')).toBeInTheDocument();
    expect(screen.getByText('Ing. Carlos Paredes y Ing. María Vaca')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Registrar y enviar solicitud' }));

    expect(await screen.findByText('Solicitud registrada y enviada')).toBeInTheDocument();
    expect(api.registrarCertificado).toHaveBeenCalledTimes(1);
  });

  it('muestra el error 409 junto a las acciones y enlaza al expediente existente', async () => {
    const user = userEvent.setup();
    const { ErrorApi } = await import('../api/httpClient');
    api.validarCertificado.mockResolvedValue(CERTIFICADO);
    api.registrarCertificado.mockRejectedValue(new ErrorApi('El certificado ya fue registrado.', 409, [], { mensaje: 'El certificado ya fue registrado.', expedienteId: 'e1', codigo: 'EXP-1' }));
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);
    await user.upload(screen.getByTestId('certificado-pdf'), pdf());
    await screen.findByText(/1804567890/);
    await user.click(screen.getByRole('button', { name: 'Registrar y enviar solicitud' }));
    const alerta = await screen.findByRole('alert');
    const boton = screen.getByRole('button', { name: 'Registrar y enviar solicitud' });
    const contenido = screen.getByText(/1804567890/);
    expect(contenido.compareDocumentPosition(alerta) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(alerta.compareDocumentPosition(boton) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(document.activeElement).toBe(alerta.parentElement);
    expect(screen.getByRole('link', { name: 'Ver la solicitud registrada' })).toHaveAttribute('href', '/solicitudes/e1');
  });

  it('si el error 409 no incluye expedienteId enlaza a la lista de solicitudes', async () => {
    const user = userEvent.setup();
    const { ErrorApi } = await import('../api/httpClient');
    api.validarCertificado.mockResolvedValue(CERTIFICADO);
    api.registrarCertificado.mockRejectedValue(new ErrorApi('El certificado ya fue registrado.', 409, [], { mensaje: 'El certificado ya fue registrado.' }));
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);
    await user.upload(screen.getByTestId('certificado-pdf'), pdf());
    await screen.findByText(/1804567890/);
    await user.click(screen.getByRole('button', { name: 'Registrar y enviar solicitud' }));
    expect(await screen.findByRole('link', { name: 'Ver la lista de solicitudes' })).toHaveAttribute('href', '/solicitudes');
  });
  it('avisa cuando la materia de destino no tiene el nombre oficial, sin impedir registrar', async () => {
    const user = userEvent.setup();
    api.validarCertificado.mockResolvedValue({
      ...CERTIFICADO,
      avisos: [
        {
          solicitud: 1,
          materiaDestino: 'Algoritmos y Logica',
          sugerencia: 'Algoritmos y Logica de Programacion',
          mensaje: '«Algoritmos y Logica» no aparece en la malla vigente de Software. ¿Quiso decir «Algoritmos y Logica de Programacion»?',
        },
      ],
    });
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    await user.upload(screen.getByTestId('certificado-pdf'), pdf());

    expect(await screen.findByText('Revise el nombre de la materia de destino')).toBeInTheDocument();
    expect(screen.getByText(/¿Quiso decir «Algoritmos y Logica de Programacion»\?/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Registrar y enviar solicitud' })).toBeEnabled();
  });

  it('en un cambio de carrera no ofrece adjuntar sílabos', async () => {
    const user = userEvent.setup();
    api.validarCertificado.mockResolvedValue(CERTIFICADO);
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    await user.upload(screen.getByTestId('certificado-pdf'), pdf());

    await screen.findByText('Pérez Gómez Ana Lucía · 1804567890');
    expect(screen.queryByText(/Sílabos de la otra universidad/)).not.toBeInTheDocument();
  });

  it('en movilidad externa permite adjuntar el sílabo de cada materia y lo manda al registrar', async () => {
    const user = userEvent.setup();
    const externo = {
      ...CERTIFICADO,
      tipoTramite: 'EXTERNO',
      rutaNormativa: 'CAU-P-122',
      institucionOrigen: 'Escuela Superior Politécnica de Chimborazo',
    };
    api.validarCertificado.mockResolvedValueOnce(externo);
    api.validarCertificado.mockResolvedValueOnce({
      ...externo,
      silabos: [{ materia: 'Fundamentos de Programación', nombreEnSilabo: 'Fundamentos de Programación', creditos: 4, unidades: 4, horasTotales: 144 }],
    });
    api.registrarCertificado.mockResolvedValue({
      expediente: { id: 'e1', codigo: 'EXP-1', estado: 'PENDIENTE_CONTENIDOS' },
      certificado: externo,
      publicado: true,
      mensaje: 'Expediente publicado.',
    });
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    await user.upload(screen.getByTestId('certificado-pdf'), pdf());

    expect(await screen.findByText('Sílabos de la otra universidad (opcional)')).toBeInTheDocument();
    expect(screen.getByText('Sin sílabo: esta materia la deciden los docentes revisores.')).toBeInTheDocument();

    const silabo = new File(['%PDF-1.4'], 'silabo.pdf', { type: 'application/pdf' });
    await user.upload(screen.getByTestId('silabo-Fundamentos de Programación'), silabo);

    expect(await screen.findByText(/Sílabo leído: 4 unidad\(es\) y 144 hora\(s\)/)).toBeInTheDocument();
    expect(api.validarCertificado).toHaveBeenLastCalledWith(expect.any(File), [
      { materia: 'Fundamentos de Programación', archivo: silabo },
    ]);

    await user.click(screen.getByRole('button', { name: 'Registrar y enviar solicitud' }));

    expect(await screen.findByText('Solicitud registrada y enviada')).toBeInTheDocument();
    expect(api.registrarCertificado).toHaveBeenCalledWith(expect.any(File), [
      { materia: 'Fundamentos de Programación', archivo: silabo },
    ]);
  });

  it('con campos sin llenar muestra la lista por sección y deja el botón deshabilitado', async () => {
    const user = userEvent.setup();
    const { ErrorApi } = await import('../api/httpClient');
    api.validarCertificado.mockRejectedValue(
      new ErrorApi('El certificado tiene campos sin llenar. Corrija el documento en Secretaría y vuelva a cargarlo.', 422, [], {
        mensaje: 'El certificado tiene campos sin llenar. Corrija el documento en Secretaría y vuelva a cargarlo.',
        tipo: 'CAMPO_VACIO',
        detalles: ['Falta la cédula del estudiante.', 'Falta el período académico de «Física».'],
        problemas: [],
        secciones: [
          { seccion: 'Certificado', ok: true, problemas: [] },
          { seccion: 'Estudiante', ok: false, problemas: ['Falta la cédula del estudiante.'] },
          { seccion: 'Trámite', ok: true, problemas: [] },
          { seccion: 'Revisores', ok: true, problemas: [] },
          { seccion: 'Historial', ok: false, problemas: ['Falta el período académico de «Física».'] },
          { seccion: 'Solicitudes', ok: true, problemas: [] },
        ],
      }),
    );
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    await user.upload(screen.getByTestId('certificado-pdf'), pdf());

    expect(await screen.findByText('El certificado tiene campos sin llenar')).toBeInTheDocument();
    const lista = screen.getByRole('list', { name: 'Revisión del certificado por sección' });
    expect(lista).toHaveTextContent('Estudiante');
    expect(lista).toHaveTextContent('Falta la cédula del estudiante.');
    expect(lista).toHaveTextContent('Falta el período académico de «Física».');
    expect(screen.getAllByText('Sin problemas.')).toHaveLength(4);
    expect(screen.getByRole('button', { name: 'Registrar y enviar solicitud' })).toBeDisabled();
  });

  it('si el PDF no es el certificado lo dice así, sin lista de secciones', async () => {
    const user = userEvent.setup();
    const { ErrorApi } = await import('../api/httpClient');
    api.validarCertificado.mockRejectedValue(
      new ErrorApi('Este PDF no es el certificado SIHA.', 422, ['El PDF no tiene texto seleccionable.'], {
        mensaje: 'Este PDF no es el certificado SIHA. Solo se acepta la plantilla oficial.',
        tipo: 'NO_ES_CERTIFICADO',
        detalles: ['El PDF no tiene texto seleccionable.'],
        problemas: [],
        secciones: [],
      }),
    );
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    await user.upload(screen.getByTestId('certificado-pdf'), pdf());

    expect(await screen.findByText('Este PDF no es el certificado SIHA')).toBeInTheDocument();
    const rechazo = screen.getByTestId('rechazo-certificado');
    expect(rechazo.textContent?.match(/Este PDF no es el certificado SIHA/g)).toHaveLength(1);
    expect(screen.getByText('Solo se acepta la plantilla oficial.')).toBeInTheDocument();
    expect(screen.getByText('El PDF no tiene texto seleccionable.')).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'Revisión del certificado por sección' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Registrar y enviar solicitud' })).toBeDisabled();
  });

  it('muestra los errores cuando el PDF no cumple la plantilla y no permite registrar', async () => {
    const user = userEvent.setup();
    const { ErrorApi } = await import('../api/httpClient');
    api.validarCertificado.mockRejectedValue(
      new ErrorApi('El PDF no cumple la plantilla oficial del certificado SIHA (SIHA-CH-01).', 422, [
        'Asignatura IND105: la nota 6.4 no es aprobatoria (mínimo 7).',
      ]),
    );
    render(<MemoryRouter><SolicitudNuevaPage /></MemoryRouter>);

    await user.upload(screen.getByTestId('certificado-pdf'), pdf());

    expect(await screen.findByText(/no cumple la plantilla oficial/)).toBeInTheDocument();
    expect(screen.getByText(/no es aprobatoria/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Registrar y enviar solicitud' })).not.toBeInTheDocument();
  });
});
