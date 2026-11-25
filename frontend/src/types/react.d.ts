import 'react';

/**
 * React 18 todavía no tipa el atributo `inert` (llega como booleano en React 19).
 * Se declara aquí para poder marcar los paneles cerrados como no interactivos
 * mientras se reproduce la animación de salida.
 */
declare module 'react' {
  // <T> tiene que estar para ampliar la interfaz original de React, aunque aquí no se use.
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  interface HTMLAttributes<T> {
    inert?: string;
  }
}
