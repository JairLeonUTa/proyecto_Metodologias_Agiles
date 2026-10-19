import { useContext } from 'react';
import { ContextoAuth } from '../context/AuthContext';
import type { ContextoAutenticacion } from '../context/AuthContext';

/**
 * Hook de acceso a la sesión y a los permisos del usuario autenticado (S1-A02).
 * Falla de forma explícita si se usa fuera del `ProveedorAutenticacion`.
 */
export function useAuth(): ContextoAutenticacion {
  const contexto = useContext(ContextoAuth);
  if (!contexto) {
    throw new Error('useAuth debe usarse dentro de ProveedorAutenticacion.');
  }
  return contexto;
}
