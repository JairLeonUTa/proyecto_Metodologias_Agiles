import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SolicitudNuevaPage } from './SolicitudNuevaPage';

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
  carreraOrigenCodigo: 'IND',
  carreraOrigenNombre: 'Ingeniería Industrial',
  carreraDestinoCodigo: 'SW',
  carreraDestinoNombre: 'Ingeniería de Software',
  revisores: [
    { nombre: 'Ing. Hernán Naranjo', correo: 'hnaranjo@uta.edu.ec' },
    { nombre: 'Ing. María Vaca', correo: 'mvaca@uta.edu.ec' },
  ],
  asignaturas: [
    {
      codigo: 'IND105',
      nombre: 'Física I',
      semestre: 1,
      creditos: 4,
      horasTotales: 192,
      fechaAprobacion: '2024-02-16',
      nota: 8.6,
      unidades: [{ numero: 1, nombre: 'Cinemática', horas: 192, contenidos: 'Movimiento' }],
    },
  ],
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
    expect(screen.getByText('IND — Ingeniería Industrial')).toBeInTheDocument();
    expect(screen.getByText('Física I')).toBeInTheDocument();
    expect(screen.getByText('2024-02-16')).toBeInTheDocument();
    expect(screen.getByText('8.60')).toBeInTheDocument();
    expect(screen.getByText('Ing. Hernán Naranjo y Ing. María Vaca')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Registrar y enviar solicitud' }));

    expect(await screen.findByText('Solicitud registrada y enviada')).toBeInTheDocument();
    expect(api.registrarCertificado).toHaveBeenCalledTimes(1);
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
