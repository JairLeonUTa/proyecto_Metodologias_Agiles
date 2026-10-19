import { useEffect, useRef } from 'react';
import type { RefObject } from 'react';

/**
 * Lleva el foco al elemento indicado cuando se monta la vista.
 * Se usa para que la navegación por teclado no pierda el foco tras un cambio
 * de ruta (S1-A10, accesibilidad).
 */
export function useFocoInicial<T extends HTMLElement = HTMLHeadingElement>(activo = true): RefObject<T> {
  const referencia = useRef<T>(null);

  useEffect(() => {
    if (!activo) {
      return;
    }
    // Se aplaza al siguiente ciclo para que el encabezado de la vista ya exista.
    const id = window.setTimeout(() => referencia.current?.focus(), 0);
    return () => window.clearTimeout(id);
  }, [activo]);

  return referencia;
}
