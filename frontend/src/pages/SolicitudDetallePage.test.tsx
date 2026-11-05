import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SolicitudDetallePage } from './SolicitudDetallePage';

const api = vi.hoisted(() => ({ obtenerSolicitud: vi.fn(), responder: vi.fn(), reanalizar: vi.fn() }));

vi.mock('../api', async (importOriginal) => {
  const real = await importOriginal<typeof import('../api')>();
  return {
    ...real,
    extraccionesApi: { ...real.extraccionesApi, ...api },
  };
});

vi.mock('../hooks/useAuth', () => ({
  useAuth: () => ({ usuario: { rol: 'DOCENTE', correoInstitucional: 'hnaranjo@uta.edu.ec', nombreCompleto: 'Ing. Hernán Naranjo' } }),
}));

const evaluacion = (numero: number, destino: string, origen: string, valor: number) => ({
  numero,
  materiaDestino: destino,
  codigoDestino: 'X',
  nivelDestino: 1,
  creditosDestino: 4,
  horasDestino: 192,
  materiasOrigen: [{ nombre: origen, nivel: 1, periodoAcademico: 'Abr-Sep 2025', nota: 8.9, horas: 192, codigoCatalogo: 'Y', encontradaEnMalla: true }],
  contenidos: valor,
  profundidad: valor,
  cargaHoraria: 100,
  estimacion: valor >= 80 ? 'CUMPLE' : 'CRITERIO_DOCENTE',
  unidades: [],
  explicacion: [`✔ Unidad 1 «Algoritmos» (48 h): la cubre la unidad 1 de «${origen}».`],
  conclusion: valor >= 80 ? 'Cumple el 80 %: el agente recomienda homologar.' : 'No llega al 80 % en contenidos: queda a criterio de los docentes revisores.',
  precedentes: [],
});

const SOLICITUD = {
  expedienteId: 'e1',
  codigoExpediente: 'EXP-1',
  numeroCertificado: 'SIHA-1',
  estudianteNombre: 'Ana Pérez',
  tipoTramite: 'INTERNO',
  rutaNormativa: 'CAU-P-011',
  institucionOrigen: 'UTA',
  carreraOrigen: 'Tecnologías de la Información',
  carreraDestino: 'Software',
  historial: [
    { nombre: 'Fundamentos de Programación', nivel: 1, periodoAcademico: 'Abr-Sep 2025', nota: 8.9, aprobada: true },
    { nombre: 'Medidas Eléctricas', nivel: 2, periodoAcademico: 'Oct 2025-Mar 2026', nota: 5.9, aprobada: false },
  ],
  revisores: [
    { nombre: 'Ing. Hernán Naranjo', correo: 'hnaranjo@uta.edu.ec', respondio: false },
    { nombre: 'Ing. María Vaca', correo: 'mvaca@uta.edu.ec', respondio: false },
  ],
  estado: 'ANALIZADO',
  mensaje: null,
  analizadoUtc: '2026-11-04T00:00:00Z',
  resultado: {
    expedienteId: 'e1',
    requisitos: [{ regla: 'Aprobó al menos el primer período académico', cumple: true, explicacion: 'Aprobó las 5 materias del primer nivel.' }],
    evaluaciones: [
      evaluacion(1, 'Algoritmos y Lógica de Programación', 'Fundamentos de Programación', 100),
      evaluacion(2, 'Cálculo Diferencial', 'Cálculo Diferencial', 50),
    ],
    alertas: [],
    resumen: 'Se analizaron 2 materias.',
    detalleTrazabilidad: '',
  },
  dictamenes: [],
  resultadoFinal: [
    { codigoDestino: 'Algoritmos y Lógica de Programación', nombreDestino: 'Algoritmos y Lógica de Programación', estimacion: 'CUMPLE', aprobaciones: 0, rechazos: 0, resultado: 'PENDIENTE' },
    { codigoDestino: 'Cálculo Diferencial', nombreDestino: 'Cálculo Diferencial', estimacion: 'CRITERIO_DOCENTE', aprobaciones: 0, rechazos: 0, resultado: 'PENDIENTE' },
  ],
  estadoRevision: 'PENDIENTE',
  puedeResponder: true,
};

describe('Solicitud: análisis explicado y decisión del docente', () => {
  beforeEach(() => {
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

  it('muestra el cambio de carrera, el historial con reprobadas, las reglas y la explicación del agente', async () => {
    abrir();

    expect(await screen.findByText('Tecnologías de la Información')).toBeInTheDocument();
    expect(screen.getAllByText('Reprobada').length).toBeGreaterThan(0);
    expect(screen.getByText('Aprobó al menos el primer período académico')).toBeInTheDocument();
    expect(screen.getAllByText(/la cubre la unidad 1/).length).toBeGreaterThan(0);
    expect(screen.getByText(/queda a criterio de los docentes/)).toBeInTheDocument();
    expect(screen.queryByText(/θ|δ|τ|0,65/)).not.toBeInTheDocument();
  });

  it('el docente decide con botones y debe decidir lo que queda a su criterio', async () => {
    const user = userEvent.setup();
    abrir();

    const enviar = await screen.findByRole('button', { name: 'Enviar mi decisión' });
    expect(enviar).toBeDisabled();

    const grupo = screen.getByRole('group', { name: 'Decisión para Cálculo Diferencial' });
    await user.click(grupo.querySelectorAll('button')[1]);
    await user.click(enviar);

    expect(api.responder).toHaveBeenCalledWith('e1', {
      decisiones: [
        { codigoDestino: 'Algoritmos y Lógica de Programación', aprobada: true, observacion: null },
        { codigoDestino: 'Cálculo Diferencial', aprobada: false, observacion: null },
      ],
      comentario: null,
    });
  });
});
