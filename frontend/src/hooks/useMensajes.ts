import { useCallback, useState } from 'react';

export type TipoMensaje = 'exito' | 'error' | 'informacion' | 'advertencia';

export interface Mensaje {
  tipo: TipoMensaje;
  texto: string;
  detalles: string[];
}

/** Estado de mensajes consistente para todas las pantallas (S1-A10). */
export interface UseMensajes {
  mensaje: Mensaje | null;
  exito: (texto: string) => void;
  error: (texto: string, detalles?: string[]) => void;
  informacion: (texto: string) => void;
  advertencia: (texto: string) => void;
  limpiar: () => void;
}

/** Gestiona un mensaje único de éxito/error/información con sus detalles de validación. */
export function useMensajes(): UseMensajes {
  const [mensaje, setMensaje] = useState<Mensaje | null>(null);

  const exito = useCallback((texto: string) => {
    setMensaje({ tipo: 'exito', texto, detalles: [] });
  }, []);

  const error = useCallback((texto: string, detalles: string[] = []) => {
    setMensaje({ tipo: 'error', texto, detalles });
  }, []);

  const informacion = useCallback((texto: string) => {
    setMensaje({ tipo: 'informacion', texto, detalles: [] });
  }, []);

  const advertencia = useCallback((texto: string) => {
    setMensaje({ tipo: 'advertencia', texto, detalles: [] });
  }, []);

  const limpiar = useCallback(() => setMensaje(null), []);

  return { mensaje, exito, error, informacion, advertencia, limpiar };
}
