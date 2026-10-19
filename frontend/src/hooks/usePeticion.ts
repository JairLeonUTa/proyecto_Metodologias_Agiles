import { useCallback, useEffect, useRef, useState } from 'react';
import { detallesDeError, mensajeDeError } from '../api';

/** Resultado de una carga asíncrona de datos. */
export interface ResultadoPeticion<T> {
  datos: T | null;
  cargando: boolean;
  error: string | null;
  detalles: string[];
  /** Vuelve a ejecutar la petición. */
  recargar: () => void;
  /** Permite mutar los datos en memoria tras una operación de escritura. */
  actualizarDatos: (datos: T | null) => void;
  /** Descarta el error actual sin recargar. */
  limpiarError: () => void;
}

/**
 * Hook de carga de datos contra el API Gateway.
 *
 * Ejecuta `ejecutar` al montar y cada vez que cambian las `dependencias`
 * (pasadas explícitamente para respetar la aridad del arreglo).
 */
export function usePeticion<T>(
  ejecutar: () => Promise<T>,
  dependencias: ReadonlyArray<unknown> = [],
): ResultadoPeticion<T> {
  const referencia = useRef(ejecutar);
  referencia.current = ejecutar;

  const [datos, setDatos] = useState<T | null>(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [detalles, setDetalles] = useState<string[]>([]);
  const [intento, setIntento] = useState(0);

  useEffect(() => {
    let vigente = true;
    setCargando(true);
    setError(null);
    setDetalles([]);

    referencia
      .current()
      .then((resultado) => {
        if (vigente) {
          setDatos(resultado);
          setCargando(false);
        }
      })
      .catch((fallo: unknown) => {
        if (vigente) {
          setDatos(null);
          setError(mensajeDeError(fallo));
          setDetalles(detallesDeError(fallo));
          setCargando(false);
        }
      });

    return () => {
      vigente = false;
    };
    // La aridad de `dependencias` es estable en todos los usos del hook.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [intento, ...dependencias]);

  const recargar = useCallback(() => setIntento((valor) => valor + 1), []);
  const actualizarDatos = useCallback((nuevos: T | null) => setDatos(nuevos), []);
  const limpiarError = useCallback(() => {
    setError(null);
    setDetalles([]);
  }, []);

  return { datos, cargando, error, detalles, recargar, actualizarDatos, limpiarError };
}
