import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ErrorApi } from '../api';
import { extraccionDePrueba } from '../test/fixtures';
import { FormularioConfirmacion } from './ConfirmarExtraccionPage';

/**
 * Stub manual de `extraccionesApi.confirmar`: el espía de Vitest marca como no manejada la
 * promesa rechazada aunque el componente la capture, así que registramos las llamadas a mano.
 */
const stub = vi.hoisted(() => ({
  llamadas: [] as unknown[][],
  respuesta: (() => Promise.resolve(undefined)) as (...args: unknown[]) => Promise<unknown>,
}));

vi.mock('../api', async (original) => {
  const real = await original<typeof import('../api')>();
  return {
    ...real,
    extraccionesApi: {
      ...real.extraccionesApi,
      confirmar: (...args: unknown[]) => {
        stub.llamadas.push(args);
        return stub.respuesta(...args);
      },
    },
  };
});

function montar(extraccion = extraccionDePrueba(), alConfirmar = vi.fn()) {
  render(
    <MemoryRouter>
      <FormularioConfirmacion extraccion={extraccion} alConfirmar={alConfirmar} />
    </MemoryRouter>,
  );
  return { alConfirmar };
}

describe('Confirmación de datos extraídos (S2-A02 / S2-A03 / S2-A09)', () => {
  beforeEach(() => {
    stub.llamadas = [];
    stub.respuesta = () => Promise.resolve(undefined);
  });

  it('datos sin confirmar → bloqueo: no llama a MS C mientras falten los créditos', async () => {
    const usuario = userEvent.setup();
    montar();

    expect(screen.getByText(/Confianza de extracción: 0,68/)).toBeInTheDocument();
    expect(screen.getByText(/el cálculo sigue bloqueado/)).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Confirmar y calcular' }));

    expect(stub.llamadas).toHaveLength(0);
    expect(screen.getByText('Ingrese los créditos (entero entre 1 y 30).')).toBeInTheDocument();
  });

  it('muestra la fecha certificada real y deja vacío el campo si no existe', () => {
    const extraccion = extraccionDePrueba();
    const { rerender } = render(
      <MemoryRouter>
        <FormularioConfirmacion
          extraccion={extraccion}
          fechaAprobacionCertificada="2025-07-15"
          alConfirmar={vi.fn()}
        />
      </MemoryRouter>,
    );

    expect(screen.getByLabelText('Fecha de aprobación certificada')).toHaveValue('2025-07-15');

    rerender(
      <MemoryRouter>
        <FormularioConfirmacion extraccion={extraccion} alConfirmar={vi.fn()} />
      </MemoryRouter>,
    );
    expect(screen.getByLabelText('Fecha de aprobación certificada')).toHaveValue('');
    expect(screen.getByLabelText('Fecha de aprobación certificada')).toHaveAttribute('placeholder', 'No registrada');
  });

  it('completa los créditos y envía PUT /confirmar con los datos corregidos', async () => {
    const usuario = userEvent.setup();
    const confirmada = extraccionDePrueba({ creditos: 3, estado: 'CONFIRMADA' });
    stub.respuesta = () => Promise.resolve(confirmada);
    const { alConfirmar } = montar();

    await usuario.type(screen.getByLabelText(/Créditos/), '3');
    await usuario.clear(screen.getByLabelText('Nombre de la unidad 2'));
    await usuario.type(screen.getByLabelText('Nombre de la unidad 2'), 'Modelo E-R');
    await usuario.click(screen.getByRole('button', { name: 'Confirmar y calcular' }));

    expect(stub.llamadas[0]).toEqual([
      confirmada.id,
      expect.objectContaining({
        creditos: 3,
        horas: 64,
        unidades: expect.arrayContaining([expect.objectContaining({ numero: 2, nombre: 'Modelo E-R' })]),
      }),
    ]);
    expect(alConfirmar).toHaveBeenCalledWith(confirmada);
  });

  it('muestra el conflicto 409 cuando otro operador ya confirmó', async () => {
    const usuario = userEvent.setup();
    stub.respuesta = () => Promise.reject(new ErrorApi('La extracción ya fue confirmada.', 409));
    montar(extraccionDePrueba({ creditos: 3 }));

    await usuario.click(screen.getByRole('button', { name: 'Confirmar y calcular' }));

    expect(await screen.findByText(/Actualice la bandeja para ver el estado actual/)).toBeInTheDocument();
    expect(stub.llamadas).toHaveLength(1);
  });

  it('PDF ilegible (pendiente OCR/LLM): mensaje y formulario de solo lectura', () => {
    montar(extraccionDePrueba({ estado: 'PENDIENTE_OCR_LLM' }));
    expect(screen.getByText(/pendiente de OCR\/LLM/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Confirmar y calcular' })).toBeNull();
    expect(screen.getByLabelText(/Créditos/)).toBeDisabled();
  });
});
