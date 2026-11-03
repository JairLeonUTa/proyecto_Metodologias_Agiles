import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import type { EvaluacionContenido, EvaluacionCreditos, Precedente } from '../../types/analisis';
import { ConsultaPrecedentes } from './ConsultaPrecedentes';
import { IndicadorConfianza } from './IndicadorConfianza';
import { PanelCreditos } from './PanelCreditos';
import { Pestanas } from './Pestanas';
import { TablaCobertura } from './TablaCobertura';

describe('IndicadorConfianza (S2-A01)', () => {
  it('muestra la cifra con coma y marca la confianza baja', () => {
    render(<IndicadorConfianza extraccion={{ estado: 'PENDIENTE_CONFIRMACION', confianza: 0.68 }} />);
    expect(screen.getByRole('meter', { name: /0,68, baja/ })).toBeInTheDocument();
  });

  it('PDF escaneado sin leer: guion en lugar de inventar un valor', () => {
    render(<IndicadorConfianza extraccion={{ estado: 'PENDIENTE_OCR_LLM', confianza: 0 }} />);
    expect(screen.queryByRole('meter')).toBeNull();
    expect(screen.getByLabelText('Confianza aún no disponible')).toHaveTextContent('—');
  });
});

describe('PanelCreditos (S2-A04)', () => {
  const datos = {
    asignaturaOrigen: 'Dibujo Técnico',
    asignaturaDestino: 'Dibujo (SW)',
    creditosOrigen: 2,
    creditosDestino: 3,
    anosTranscurridos: 12,
    numeroMatricula: 3,
    esCreditoExterno: false,
  };

  it('rotula Pcred como métrica de apoyo y muestra las alertas de admisibilidad', () => {
    const creditos: EvaluacionCreditos = {
      pcred: 66.67,
      categoria: 'BLOQUEO_3RA_MATRICULA',
      alertas: ['3ra matrícula detectada en malla destino (matrícula #3): bloqueo por RN-16.'],
      bloqueoDuro: true,
      detalleCalculo: 'Pcred = min(100, 2 / 3 * 100) = 66.67%',
    };
    render(<PanelCreditos creditos={creditos} datos={datos} parametros={null} />);

    expect(screen.getByText('66,67 %')).toBeInTheDocument();
    expect(screen.getByText('métrica informativa de apoyo')).toBeInTheDocument();
    const alertas = screen.getByRole('list', { name: 'Alertas de admisibilidad' });
    expect(within(alertas).getByText(/Vigencia > 10 años/)).toBeInTheDocument();
    expect(within(alertas).getByText(/Tercera matrícula detectada/)).toBeInTheDocument();
    expect(screen.getByText('No disponibles')).toBeInTheDocument();
  });

  it('muestra θ, δ, τ y la versión de la regla aplicada', () => {
    const creditos: EvaluacionCreditos = { pcred: 100, categoria: 'NORMAL', alertas: [], bloqueoDuro: false, detalleCalculo: '' };
    render(
      <PanelCreditos
        creditos={creditos}
        datos={{ ...datos, anosTranscurridos: 2, numeroMatricula: 1 }}
        parametros={{ id: 1, rutaNormativa: 'CAU-P-011', theta: 80, delta: 15, tau: 75, version: 3, actualizadoUtc: '', actualizadoPor: '' }}
      />,
    );
    expect(screen.getByText(/θ 80 · δ 15 · τ 0,75/)).toBeInTheDocument();
    expect(screen.getByText('Reglas versión 3')).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'Alertas de admisibilidad' })).toBeNull();
  });
});

describe('TablaCobertura (S2-A06)', () => {
  const unidad = (numero: number, similitud: number, cubierta: boolean) => ({
    numero,
    nombre: `Unidad ${numero}`,
    horas: 12,
    similitudMaxima: similitud,
    cubierta,
    unidadesOrigenContribuyentes: [numero],
  });

  it('caso 6 de 7 unidades: 85,71 % y U.7 faltante', () => {
    const contenido: EvaluacionContenido = {
      pcont: 85.71,
      unidadesCubiertas: [1, 2, 3, 4, 5, 6].map((n) => unidad(n, 80 + n, true)),
      unidadesFaltantes: [unidad(7, 42, false)],
      tauUsado: 75,
      detalleCalculo: '',
      violacionRN19: false,
      unidadesOrigenDuplicadas: [],
    };
    render(
      <TablaCobertura
        contenido={contenido}
        unidadesOrigen={[{ numero: 7, nombre: 'Recursividad', horas: 12, contenidos: '' }]}
        versionReglas={3}
      />,
    );

    expect(screen.getByText('85,71 %')).toBeInTheDocument();
    expect(screen.getByText('6 de 7 unidades cubiertas')).toBeInTheDocument();
    expect(screen.getByText('τ = 0,75')).toBeInTheDocument();
    expect(screen.getAllByText('✓ Cubierta')).toHaveLength(6);
    const fila = screen.getByText('✗ Faltante').closest('tr') as HTMLElement;
    expect(within(fila).getByText('U.7 Unidad 7')).toBeInTheDocument();
    expect(within(fila).getByText('0,42')).toBeInTheDocument();
    expect(within(fila).getByText('U.7 Recursividad')).toBeInTheDocument();
  });
});

describe('ConsultaPrecedentes (S2-A07 / S2-A09)', () => {
  const precedente: Precedente = {
    id: 'p1',
    resolucion: 'RES-FISEI-2025-031',
    asignaturasOrigen: [
      { codigo: 'FIS1', nombre: 'Física I' },
      { codigo: 'FIS2', nombre: 'Física II' },
    ],
    asignaturaDestino: 'Física',
    institucionOrigen: 'UTA · Ingeniería Industrial',
    fecha: '2025-03-12',
  };

  it('coincidencia exacta: resolución, asignaturas e institución', () => {
    render(<ConsultaPrecedentes titulo="Caso" precedentes={[precedente]} />);
    expect(screen.getByText('RES-FISEI-2025-031')).toBeInTheDocument();
    expect(screen.getByText('Física I + Física II → Física')).toBeInTheDocument();
    expect(screen.getByText('12/03/2025')).toBeInTheDocument();
    expect(screen.getByText('1 coincidencia exacta')).toBeInTheDocument();
  });

  it('array vacío: avisa "Sin precedentes registrados" sin inventar resoluciones', () => {
    render(<ConsultaPrecedentes titulo="Caso" precedentes={[]} />);
    expect(screen.getByText(/Sin precedentes registrados/)).toBeInTheDocument();
    expect(screen.queryByText(/RES-/)).toBeNull();
  });

  it('falla de la consulta: aviso que no bloquea el cálculo', () => {
    render(<ConsultaPrecedentes titulo="Caso" precedentes={null} error="Servicio no disponible." />);
    expect(screen.getByRole('alert')).toHaveTextContent('no se ve afectado');
  });
});

describe('Pestanas (S2-A10, navegación por teclado)', () => {
  function Demo(): JSX.Element {
    const [activa, setActiva] = useState('a');
    return (
      <Pestanas
        etiqueta="Demo"
        activa={activa}
        alCambiar={setActiva}
        pestanas={[
          { id: 'a', etiqueta: 'Créditos', contenido: <p>Panel A</p> },
          { id: 'b', etiqueta: 'Cobertura', contenido: <p>Panel B</p> },
          { id: 'c', etiqueta: 'Precedentes', contenido: <p>Panel C</p> },
        ]}
      />
    );
  }

  it('las flechas mueven el foco y activan la pestaña', async () => {
    const usuario = userEvent.setup();
    render(<Demo />);
    await usuario.click(screen.getByRole('tab', { name: 'Créditos' }));
    await usuario.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: 'Cobertura' })).toHaveFocus();
    expect(screen.getByText('Panel B')).toBeInTheDocument();
    await usuario.keyboard('{ArrowLeft}{ArrowLeft}');
    expect(screen.getByRole('tab', { name: 'Precedentes' })).toHaveAttribute('aria-selected', 'true');
  });
});
