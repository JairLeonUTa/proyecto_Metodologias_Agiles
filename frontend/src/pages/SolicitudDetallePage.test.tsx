import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ErrorApi } from '../api/httpClient';
import { SolicitudDetallePage } from './SolicitudDetallePage';

const api = vi.hoisted(() => ({
  obtenerSolicitud: vi.fn(),
  responder: vi.fn(),
  reanalizar: vi.fn(),
  decidirComoAdministrador: vi.fn(),
  reenviarAgente: vi.fn(),
}));
const DOCENTE = { rol: 'DOCENTE', correoInstitucional: 'cparedes@uta.edu.ec', nombreCompleto: 'Ing. Carlos Paredes' };
const sesion = vi.hoisted(() => ({ usuario: null as unknown }));

vi.mock('../api', async (importOriginal) => {
  const real = await importOriginal<typeof import('../api')>();
  return {
    ...real,
    extraccionesApi: {
      ...real.extraccionesApi,
      obtenerSolicitud: api.obtenerSolicitud,
      responder: api.responder,
      reanalizar: api.reanalizar,
      decidirComoAdministrador: api.decidirComoAdministrador,
    },
    expedientesApi: { ...real.expedientesApi, reenviarAgente: api.reenviarAgente },
  };
});

vi.mock('../hooks/useAuth', () => ({
  useAuth: () => ({ usuario: sesion.usuario }),
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
    { nombre: 'Ing. Carlos Paredes', correo: 'cparedes@uta.edu.ec', respondio: false },
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
    sesion.usuario = DOCENTE;
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

  it('oculta votos de materias bloqueadas y conserva los votos para NO_CUMPLE no bloqueada', async () => {
    const solicitud = {
      ...SOLICITUD,
      resultado: { ...SOLICITUD.resultado!, evaluaciones: SOLICITUD.resultado!.evaluaciones.map((e, i) => ({ ...e, estimacion: 'NO_CUMPLE', bloqueadaPorCreditos: i === 0 })) },
      resultadoFinal: SOLICITUD.resultadoFinal.map((m, i) => ({ ...m, estimacion: 'NO_CUMPLE' as const, bloqueadaPorCreditos: i === 0 })),
    };
    api.obtenerSolicitud.mockResolvedValue(solicitud);
    abrir();

    expect((await screen.findAllByText('No se puede homologar')).length).toBeGreaterThan(0);
    expect(screen.getByText('Los créditos no llegan al 80 %.')).toBeInTheDocument();
    expect((await screen.findAllByText('✘ No llega al 80 % · recomienda no homologar')).length).toBeGreaterThan(0);
    expect(screen.getByRole('group', { name: 'Decisión para Cálculo Diferencial' })).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Decisión para Algoritmos y Lógica de Programación' })).not.toBeInTheDocument();
  });
  it('si los docentes no coinciden, el administrador toma la decisión final con un motivo', async () => {
    const user = userEvent.setup();
    sesion.usuario = { rol: 'ADMINISTRADOR', correoInstitucional: 'admin@uta.edu.ec', nombreCompleto: 'Director' };
    const sinAcuerdo = {
      ...SOLICITUD,
      estadoRevision: 'RESUELTA',
      puedeResponder: false,
      resultadoFinal: [{ ...SOLICITUD.resultadoFinal[1], aprobaciones: 1, rechazos: 1, resultado: 'SIN_ACUERDO' }],
    };
    api.obtenerSolicitud.mockResolvedValue(sinAcuerdo);
    api.decidirComoAdministrador.mockResolvedValue({
      ...sinAcuerdo,
      resultadoFinal: [
        {
          ...sinAcuerdo.resultadoFinal[0],
          resultado: 'HOMOLOGADA',
          decisionFinal: { aprobada: true, comentario: 'Cubre los temas', nombreAdministrador: 'Director', fechaUtc: '2026-11-24T00:00:00Z' },
        },
      ],
    });
    abrir();

    expect(await screen.findByText('Los docentes no coinciden: su decisión final')).toBeInTheDocument();
    const guardar = screen.getByRole('button', { name: 'Guardar decisión final' });
    expect(guardar).toBeDisabled();
    await user.click(within(screen.getByRole('group', { name: 'Decisión final para Cálculo Diferencial' })).getByRole('button', { name: 'Sí, homologar' }));
    expect(guardar).toBeDisabled();
    await user.type(screen.getByLabelText('Motivo de la decisión final para Cálculo Diferencial'), 'Cubre los temas');
    await user.click(guardar);

    expect(api.decidirComoAdministrador).toHaveBeenCalledWith('e1', {
      codigoDestino: 'Cálculo Diferencial',
      aprobada: true,
      comentario: 'Cubre los temas',
    });
    expect(await screen.findByText(/Cubre los temas/)).toBeInTheDocument();
    expect(screen.queryByText('Los docentes no coinciden: su decisión final')).not.toBeInTheDocument();
  });

  it('el docente ve que, sin acuerdo, decide el administrador', async () => {
    api.obtenerSolicitud.mockResolvedValue({
      ...SOLICITUD,
      resultadoFinal: [{ ...SOLICITUD.resultadoFinal[1], aprobaciones: 1, rechazos: 1, resultado: 'SIN_ACUERDO' }],
    });
    abrir();

    expect(await screen.findByText(/La decisión final la toma el\s+administrador/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Guardar decisión final' })).not.toBeInTheDocument();
  });

  it('si el agente no responde lo explica y deja volver a enviar la solicitud', async () => {
    const user = userEvent.setup();
    sesion.usuario = { rol: 'OPERADOR', correoInstitucional: 'op@uta.edu.ec', nombreCompleto: 'Operador' };
    api.obtenerSolicitud.mockRejectedValue(new ErrorApi('Bad Gateway', 502));
    api.reenviarAgente.mockResolvedValue({});
    abrir();

    expect(await screen.findByText('El agente no está respondiendo')).toBeInTheDocument();
    expect(screen.getByText(/docker compose --profile sprint23 up -d/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Volver a enviar al agente' }));

    expect(api.reenviarAgente).toHaveBeenCalledWith('e1');
    expect(await screen.findByText(/Se volvió a enviar al agente/)).toBeInTheDocument();
  });

  it('un análisis con reglas anteriores ofrece volver a analizarlo', async () => {
    api.obtenerSolicitud.mockResolvedValue({ ...SOLICITUD, analisisDesactualizado: true });
    api.reanalizar.mockResolvedValue({ ...SOLICITUD, analisisDesactualizado: false });
    abrir();

    expect(await screen.findByText('Este análisis se hizo con reglas anteriores')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Volver a analizar con las reglas vigentes' }));

    expect(api.reanalizar).toHaveBeenCalledWith('e1');
    expect(await screen.findByText('Tecnologías de la Información')).toBeInTheDocument();
    expect(screen.queryByText('Este análisis se hizo con reglas anteriores')).not.toBeInTheDocument();
  });

  it('si el agente se está preparando lo explica y no pide reintentar a mano', async () => {
    api.obtenerSolicitud.mockResolvedValue({
      ...SOLICITUD,
      estado: 'PREPARANDO',
      mensaje: 'El agente se está preparando (la primera vez descarga el modelo). La solicitud se analizará sola; no hace falta hacer nada.',
      resultado: null,
      resultadoFinal: [],
      puedeResponder: false,
    });
    abrir();

    expect(await screen.findAllByText('El agente se está preparando')).not.toHaveLength(0);
    expect(screen.getByText(/no hace falta hacer nada/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Volver a analizar' })).not.toBeInTheDocument();
  });

  it('sin sílabo para comparar no muestra barras ni porcentajes', async () => {
    const sinSilabo = {
      ...evaluacion(1, 'Física', 'Física General', 0),
      estimacion: 'CRITERIO_DOCENTE' as const,
      unidades: [],
      sinSilabo: true,
      conclusion: 'Sin sílabo para comparar: queda a criterio de los docentes revisores.',
    };
    api.obtenerSolicitud.mockResolvedValue({
      ...SOLICITUD,
      resultado: { ...SOLICITUD.resultado, evaluaciones: [sinSilabo] },
      resultadoFinal: [{ ...SOLICITUD.resultadoFinal[1], codigoDestino: 'Física', nombreDestino: 'Física' }],
    });
    abrir();

    expect((await screen.findAllByText(/Sin sílabo para comparar · decide el docente/)).length).toBeGreaterThan(0);
    expect(screen.queryByRole('img', { name: /Contenidos/ })).not.toBeInTheDocument();
    expect(screen.queryByText('0 %')).not.toBeInTheDocument();
  });

  it('la que no llega al 80 % es una recomendación: el docente igual la decide y puede homologarla', async () => {
    const user = userEvent.setup();
    const noCumple = {
      ...evaluacion(2, 'Física', 'Física Básica', 100),
      estimacion: 'NO_CUMPLE' as const,
      creditosDestino: 3,
      porcentajeCreditos: 66.67,
      creditosOrigen: 2,
      unidades: [{ numero: 1, nombre: 'Estática', horas: 36, similitud: 95, cubierta: true, materiaOrigen: 'Física Básica', numeroUnidadOrigen: 1, nombreUnidadOrigen: 'Estática', horasUnidadOrigen: 36 }],
      conclusion: 'Recomendación: no homologar. No llega al 80 % en créditos (66,67 %). Los docentes revisores deciden.',
    };
    api.obtenerSolicitud.mockResolvedValue({
      ...SOLICITUD,
      resultado: { ...SOLICITUD.resultado, evaluaciones: [SOLICITUD.resultado.evaluaciones[0], noCumple] },
      resultadoFinal: [
        SOLICITUD.resultadoFinal[0],
        { codigoDestino: 'Física', nombreDestino: 'Física', estimacion: 'NO_CUMPLE', aprobaciones: 0, rechazos: 0, resultado: 'PENDIENTE' },
      ],
    });
    abrir();

    expect((await screen.findAllByText('✘ No llega al 80 % · recomienda no homologar')).length).toBeGreaterThan(0);
    expect(screen.getByRole('img', { name: 'Créditos: 66.67 %' })).toBeInTheDocument();
    expect(screen.getByText('2 crédito(s) aprobado(s) frente a 3.')).toBeInTheDocument();
    expect(screen.queryByText(/No se vota/)).not.toBeInTheDocument();

    const grupo = screen.getByRole('group', { name: 'Decisión para Física' });
    await user.click(within(grupo).getByRole('button', { name: 'Sí, homologar' }));
    await user.click(screen.getByRole('button', { name: 'Enviar mi decisión' }));
    expect(api.responder).toHaveBeenLastCalledWith('e1', {
      decisiones: [
        { codigoDestino: 'Algoritmos y Lógica de Programación', aprobada: true, observacion: null },
        { codigoDestino: 'Física', aprobada: true, observacion: null },
      ],
      comentario: null,
    });
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
