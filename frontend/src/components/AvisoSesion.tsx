import { useEffect, useState } from 'react';
import { mensajeDeError } from '../api';
import { useAuth } from '../hooks/useAuth';
import { Boton } from './ui';

/** Minutos antes del vencimiento en que aparece el aviso. */
export const MINUTOS_AVISO = 5;

/** Minutos que faltan para que venza la sesión (redondeado hacia arriba), o null si no se sabe. */
export function minutosRestantes(expiraUtc: string | null, ahora: number = Date.now()): number | null {
  if (!expiraUtc) return null;
  const vence = Date.parse(expiraUtc);
  if (Number.isNaN(vence)) return null;
  return Math.ceil((vence - ahora) / 60000);
}

/**
 * Aviso antes de que venza la sesión: el token dura una hora y antes se cerraba sin aviso. Cinco minutos
 * antes aparece una franja con "Seguir conectado" (pide un token nuevo sin volver a escribir la
 * contraseña). Si vence, se cierra la sesión con un mensaje claro.
 */
export function AvisoSesion(): JSX.Element | null {
  const { expiraUtc, renovarSesion, cerrarSesion } = useAuth();
  const [ahora, setAhora] = useState(() => Date.now());
  const [renovando, setRenovando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const reloj = setInterval(() => setAhora(Date.now()), 15000);
    return () => clearInterval(reloj);
  }, []);

  const minutos = minutosRestantes(expiraUtc, ahora);

  useEffect(() => {
    if (minutos !== null && minutos <= 0) {
      cerrarSesion('Su sesión venció por tiempo. Vuelva a iniciar sesión para continuar.');
    }
  }, [minutos, cerrarSesion]);

  if (minutos === null || minutos > MINUTOS_AVISO || minutos <= 0) return null;

  const seguir = async (): Promise<void> => {
    setRenovando(true);
    setError(null);
    try {
      await renovarSesion();
    } catch (fallo) {
      setError(mensajeDeError(fallo));
    } finally {
      setRenovando(false);
    }
  };

  return (
    <div
      role="alert"
      className="mx-2 mt-2 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900"
    >
      <p>
        <strong>Su sesión se cerrará en {minutos === 1 ? '1 minuto' : `${minutos} minutos`}.</strong> Si sigue trabajando, pulse
        «Seguir conectado» para no perder lo que está haciendo.
        {error ? <span className="mt-1 block text-red-700">{error}</span> : null}
      </p>
      <div className="flex gap-2">
        <Boton onClick={() => void seguir()} cargando={renovando} textoCargando="Renovando…">
          Seguir conectado
        </Boton>
        <Boton variante="secundario" onClick={() => cerrarSesion()}>
          Cerrar sesión
        </Boton>
      </div>
    </div>
  );
}
