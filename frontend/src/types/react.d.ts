import 'react';

/**
 * React 18 todavía no tipa el atributo `inert` (llega como booleano en React 19).
 * Se declara aquí para poder marcar los paneles cerrados como no interactivos
 * mientras se reproduce la animación de salida.
 */
declare module 'react' {
  interface HTMLAttributes<T> {
    inert?: string;
  }
}
