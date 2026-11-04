import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SolicitudDetallePage } from './SolicitudDetallePage';

const api = vi.hoisted(() => ({ obtener: vi.fn(), obtenerSolicitud: vi.fn(), responder: vi.fn(), reanalizar: vi.fn() }));

vi.mock('../api', async (importOriginal) => {
  const real = await importOriginal<typeof import('../api')>();
  return {
    ...real,
    expedientesApi: { ...real.expedientesApi, obtener: api.obtener },
    extraccionesApi: { ...real.extraccionesApi, obtenerSolicitud: api.obtenerSolicitud, responder: api.responder, reanalizar: api.reanalizar },
  };
});

vi.mock('../hooks/useAuth', () => ({
  useAuth: () => ({ usuario: { rol: 'DOCENTE', correoInstitucional: 'hnaranjo@uta.edu.ec', nombreCompleto: 'Ing. Hernán Naranjo' } }),
}));

const propuesta = (codigo: string, nombre: string, pcont: number) => ({
  codigoDestino: codigo,
  nombreDestino: nombre,
  creditosDestino: 4,
  semestreDestino: 1,
  origenes: [{ extraccionId: 'x', codigo: 'IND105', nombre: 'Física I', creditos: 4, horas: 192, fechaAprobacion: '2024-02-16', anosTranscurridos: 2, numeroMatricula: null }],
  pcont,
  pcred: 100,
  categoriaCreditos: 'NORMAL',
  alcanzaContenido: pcont >= 80,
  estadoFinal: pcont >= 80 ? 0 : 6,
  unidades: [],
  alertas: [],
  precedentes: [],
});

const SOLICITUD = {
  expedienteId: 'e1',
  codigoExpediente: 'EXP-1',
  numeroCertificado: 'SIHA-1',
  estudianteNombre: 'Ana',
  tipoTramite: 'INTERNO',
  rutaNormativa: 'CAU-P-011',
  institucionOrigen: 'UTA',
  carreraOrigenCodigo: 'IND',
  carreraDestinoCodigo: 'SW',
  revisores: [
    { nombre: 'Ing. Hernán Naranjo', correo: 'hnaranjo@uta.edu.ec', respondio: false },
    { nombre: 'Ing. María Vaca', correo: 'mvaca@uta.edu.ec', respondio: false },
  ],
  estado: 'ANALIZADO',
  mensaje: null,
  analizadoUtc: '2026-11-04T00:00:00Z',
  resultado: {
    expedienteId: 'e1',
    propuestas: [propuesta('SW101', 'Fundamentos de Programación', 100)],
    sinCoberturaSuficiente: [propuesta('SW105', 'Física I', 62.5)],
    origenesSinAsignar: [],
    alertas: [],
    detalleTrazabilidad: '',
  },
  dictamenes: [],
  resultadoFinal: [
    { codigoDestino: 'SW101', nombreDestino: 'Fundamentos de Programación', estimacion: 'CUMPLE', aprobaciones: 0, rechazos: 0, resultado: 'PENDIENTE' },
    { codigoDestino: 'SW105', nombreDestino: 'Física I', estimacion: 'CRITERIO_DOCENTE', aprobaciones: 0, rechazos: 0, resultado: 'PENDIENTE' },
  ],
  estadoRevision: 'PENDIENTE',
  puedeResponder: true,
};

describe('Solicitud: análisis del agente y respuesta del docente', () => {
  beforeEach(() => {
    api.obtener.mockResolvedValue({
      id: 'e1', codigo: 'EXP-1', estudianteNombre: 'Ana', numeroCertificado: 'SIHA-1', carreraOrigenCodigo: 'IND',
      carreraDestinoCodigo: 'SW', rutaNormativa: 'CAU-P-011', institucionOrigen: 'UTA',
      revisores: [{ nombre: 'Ing. Hernán Naranjo', correo: 'hnaranjo@uta.edu.ec' }, { nombre: 'Ing. María Vaca', correo: 'mvaca@uta.edu.ec' }],
      asignaturas: [{ codigoOrigen: 'IND105', nombreOrigen: 'Física I', codigoDestino: null, nota: 8.6, fechaAprobacionCertificada: '2024-02-16', semestre: 1, creditos: 4, horas: 192 }],
    });
    api.obtenerSolicitud.mockResolvedValue(SOLICITUD);
    api.responder.mockResolvedValue({ ...SOLICITUD, estadoRevision: 'EN_REVISION' });
  });

  const abrir = () =>
    render(
      <MemoryRouter initialEntries={['/solicitudes/e1']}>
        <Routes>
          <Route path="/solicitudes/:id" element={<SolicitudDetallePage />} />
        </Routes>
      </MemoryRouter>,
    );

  it('muestra las notas, lo que cumple el 80 % y lo que queda a criterio del docente, sin umbrales', async () => {
    abrir();

    expect(await screen.findByText('8.60')).toBeInTheDocument();
    expect(screen.getByText('Cumple')).toBeInTheDocument();
    expect(screen.getByText('A criterio del docente')).toBeInTheDocument();
    expect(screen.queryByText(/θ|δ|τ|0,65/)).not.toBeInTheDocument();
  });

  it('el docente debe decidir lo que queda a su criterio antes de enviar', async () => {
    const user = userEvent.setup();
    abrir();

    const enviar = await screen.findByRole('button', { name: 'Enviar mi respuesta' });
    expect(enviar).toBeDisabled();

    const [, noAprobarFisica] = screen.getAllByLabelText('No aprobar');
    await user.click(noAprobarFisica);
    await user.click(enviar);

    expect(api.responder).toHaveBeenCalledWith('e1', {
      decisiones: [
        { codigoDestino: 'SW101', aprobada: true, observacion: null },
        { codigoDestino: 'SW105', aprobada: false, observacion: null },
      ],
      comentario: null,
    });
  });
});
