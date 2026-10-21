import { render, screen, fireEvent, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Modal } from './Modal';
import { Boton } from './Boton';
import { describe, it, expect, vi, beforeEach } from 'vitest';

describe('Modal - accesibilidad y gestión de foco', () => {
  const onCerrar = vi.fn();
  const children = (
    <form>
      <label htmlFor="campo1">Campo 1</label>
      <input id="campo1" type="text" placeholder="Escribe aquí" />
      <label htmlFor="campo2">Campo 2</label>
      <input id="campo2" type="text" placeholder="Segundo campo" />
      <select id="select1">
        <option value="">Seleccione</option>
        <option value="a">Opción A</option>
      </select>
      <textarea id="textarea1" placeholder="Área de texto" />
    </form>
  );
  const acciones = (
    <>
      <Boton variante="secundario" onClick={onCerrar}>
        Cancelar
      </Boton>
      <Boton type="submit">Guardar</Boton>
    </>
  );

  beforeEach(() => {
    vi.clearAllMocks();
    document.body.style.overflow = '';
  });

  it('abre el modal y el primer input es enfocable (no botones en header)', async () => {
    render(<Modal abierto={true} titulo="Test" onCerrar={onCerrar}>{children}</Modal>);

    await act(async () => {
      await new Promise((r) => setTimeout(r, 0));
    });

    const primerInput = screen.getByPlaceholderText('Escribe aquí');
    const btnCerrar = screen.getByRole('button', { name: /cerrar/i });

    expect(primerInput).not.toBeDisabled();
    expect(btnCerrar).toHaveAttribute('tabIndex', '-1');
    expect(btnCerrar).toHaveAttribute('aria-label', 'Cerrar el cuadro de diálogo');
  });

  it('permite escribir texto completo en el input sin perder el foco (test clave del fix)', async () => {
    const user = userEvent.setup();
    render(<Modal abierto={true} titulo="Test" onCerrar={onCerrar}>{children}</Modal>);

    await act(async () => {
      await new Promise((r) => setTimeout(r, 0));
    });

    const input = screen.getByPlaceholderText('Escribe aquí');
    await user.type(input, 'Texto de prueba completo');

    // TEST CLAVE: El foco sigue en el input después de escribir texto completo
    expect(input).toHaveFocus();
    expect(input).toHaveValue('Texto de prueba completo');
  });

  it('Tab navega entre elementos enfocables dentro del modal (focus trapping)', async () => {
    const user = userEvent.setup();
    render(<Modal abierto={true} titulo="Test" onCerrar={onCerrar} acciones={acciones}>{children}</Modal>);

    await act(async () => {
      await new Promise((r) => setTimeout(r, 0));
    });

    const input1 = screen.getByPlaceholderText('Escribe aquí');
    const input2 = screen.getByPlaceholderText('Segundo campo');
    const select = screen.getByRole('combobox');
    const textarea = screen.getByPlaceholderText('Área de texto');
    const btnCancelar = screen.getByRole('button', { name: /cancelar/i });
    const btnGuardar = screen.getByRole('button', { name: /guardar/i });

    // Tab desde input1 -> input2
    await user.tab();
    expect(input2).toHaveFocus();

    // Tab -> select
    await user.tab();
    expect(select).toHaveFocus();

    // Tab -> textarea
    await user.tab();
    expect(textarea).toHaveFocus();

    // Tab -> botón Cancelar (en footer)
    await user.tab();
    expect(btnCancelar).toHaveFocus();

    // Tab -> botón Guardar
    await user.tab();
    expect(btnGuardar).toHaveFocus();

    // Tab -> vuelve al primer input (focus trapping cíclico)
    await user.tab();
    expect(input1).toHaveFocus();
  });

  it('Shift+Tab navega en orden inverso (focus trapping inverso)', async () => {
    const user = userEvent.setup();
    render(<Modal abierto={true} titulo="Test" onCerrar={onCerrar} acciones={acciones}>{children}</Modal>);

    await act(async () => {
      await new Promise((r) => setTimeout(r, 0));
    });

    const btnGuardar = screen.getByRole('button', { name: /guardar/i });

    // Shift+Tab desde input1 -> botón Guardar (último elemento)
    await user.tab({ shift: true });
    expect(btnGuardar).toHaveFocus();
  });

  it('Escape cierra el modal y llama a onCerrar', async () => {
    render(<Modal abierto={true} titulo="Test" onCerrar={onCerrar}>{children}</Modal>);

    await act(async () => {
      await new Promise((r) => setTimeout(r, 0));
    });

    fireEvent.keyDown(document, { key: 'Escape' });

    expect(onCerrar).toHaveBeenCalledTimes(1);
  });

  it('llama a onCerrar y cierra el modal al pulsar Escape (verificación de callback)', async () => {
    render(<Modal abierto={true} titulo="Test" onCerrar={onCerrar}>{children}</Modal>);

    await act(async () => {
      await new Promise((r) => setTimeout(r, 0));
    });

    fireEvent.keyDown(document, { key: 'Escape' });

    expect(onCerrar).toHaveBeenCalledTimes(1);
  });

  it('bloquea scroll del body mientras está abierto', () => {
    const originalOverflow = document.body.style.overflow;
    render(<Modal abierto={true} titulo="Test" onCerrar={onCerrar}>{children}</Modal>);

    expect(document.body.style.overflow).toBe('hidden');

    // Limpieza manual para el siguiente test
    document.body.style.overflow = originalOverflow;
  });

  it('botón "Cerrar" (X) del header tiene tabIndex={-1} y NO está en orden de tabulación', async () => {
    render(<Modal abierto={true} titulo="Test" onCerrar={onCerrar}>{children}</Modal>);

    await act(async () => {
      await new Promise((r) => setTimeout(r, 0));
    });

    const btnCerrar = screen.getByRole('button', { name: /cerrar/i });

    // Verificación estructural: el botón tiene tabIndex={-1}
    expect(btnCerrar).toHaveAttribute('tabIndex', '-1');
    expect(btnCerrar).toHaveAttribute('aria-label', 'Cerrar el cuadro de diálogo');
  });
});