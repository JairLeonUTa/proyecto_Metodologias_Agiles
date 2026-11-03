import { useCallback } from 'react';
import { expedientesApi, extraccionesApi } from '../api';
import type { Extraccion } from '../types/analisis';
import type { Expediente } from '../types/expedientes';
import { usePeticion } from './usePeticion';
import type { ResultadoPeticion } from './usePeticion';

export interface ExtraccionesConExpediente {
  extracciones: Extraccion[];
  /** Expedientes de MS B indexados por id. Vacío si el rol no puede leerlos. */
  expedientes: Record<string, Expediente>;
  /** `true` si MS B no respondió: la bandeja sigue funcionando con datos de MS C. */
  sinDatosExpediente: boolean;
}

/**
 * Carga la bandeja de MS C y, en paralelo, los expedientes de MS B para mostrar
 * código, trámite y nombre del PDF. Un fallo de MS B no bloquea la bandeja (S2-A09).
 */
export function useExtraccionesConExpediente(estado?: string): ResultadoPeticion<ExtraccionesConExpediente> {
  const cargar = useCallback(async (): Promise<ExtraccionesConExpediente> => {
    const [extracciones, expedientes] = await Promise.allSettled([
      extraccionesApi.bandeja(estado || undefined),
      expedientesApi.listar(),
    ]);

    if (extracciones.status === 'rejected') {
      throw extracciones.reason;
    }

    const indice: Record<string, Expediente> = {};
    if (expedientes.status === 'fulfilled') {
      for (const expediente of expedientes.value) {
        indice[expediente.id] = expediente;
      }
    }

    return {
      extracciones: extracciones.value,
      expedientes: indice,
      sinDatosExpediente: expedientes.status === 'rejected',
    };
  }, [estado]);

  return usePeticion(cargar, [estado]);
}
