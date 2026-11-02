/**
 * Icon set: minimal outline SVGs, 1.5px stroke, one consistent style.
 * No external dependency. Each icon inherits `currentColor`.
 */

interface PropsIcono {
  nombre: NombreIcono;
  className?: string;
}

export type NombreIcono =
  | 'inicio'
  | 'reglas'
  | 'nueva-solicitud'
  | 'bandeja'
  | 'consulta'
  | 'carreras'
  | 'mallas'
  | 'asignaturas'
  | 'importar'
  | 'usuarios'
  | 'buscar'
  | 'menu'
  | 'cerrar'
  | 'refrescar'
  | 'descargar'
  | 'flecha-volver'
  | 'documento'
  | 'carpeta'
  | 'salir'
  | 'chevron';

const TRAZOS: Record<NombreIcono, JSX.Element> = {
  inicio: (
    <>
      <path d="M3 10.5L12 3l9 7.5" />
      <path d="M5 9.5V21h14V9.5" />
      <path d="M9 21v-6h6v6" />
    </>
  ),
  reglas: (
    <>
      <path d="M4 6h16M4 12h16M4 18h16" />
      <circle cx="8" cy="6" r="2" fill="currentColor" stroke="none" />
      <circle cx="16" cy="12" r="2" fill="currentColor" stroke="none" />
      <circle cx="10" cy="18" r="2" fill="currentColor" stroke="none" />
    </>
  ),
  'nueva-solicitud': (
    <>
      <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
      <path d="M14 2v6h6" />
      <path d="M12 12v6M9 15h6" />
    </>
  ),
  bandeja: (
    <>
      <path d="M4 4h16v16H4z" />
      <path d="M4 14h4l2 3h4l2-3h4" />
    </>
  ),
  consulta: (
    <>
      <circle cx="11" cy="11" r="7" />
      <path d="M21 21l-4.35-4.35" />
    </>
  ),
  carreras: (
    <>
      <path d="M12 3L2 9l10 6 10-6-10-6z" />
      <path d="M6 12v5c0 1 3 3 6 3s6-2 6-3v-5" />
    </>
  ),
  mallas: (
    <>
      <rect x="3" y="3" width="7" height="7" rx="1" />
      <rect x="14" y="3" width="7" height="7" rx="1" />
      <rect x="3" y="14" width="7" height="7" rx="1" />
      <rect x="14" y="14" width="7" height="7" rx="1" />
    </>
  ),
  asignaturas: (
    <>
      <path d="M4 19.5A2.5 2.5 0 016.5 17H20" />
      <path d="M6.5 2H20v20H6.5A2.5 2.5 0 014 19.5v-15A2.5 2.5 0 016.5 2z" />
    </>
  ),
  importar: (
    <>
      <path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4" />
      <path d="M12 3v12M7 8l5-5 5 5" />
    </>
  ),
  usuarios: (
    <>
      <path d="M16 21v-2a4 4 0 00-4-4H6a4 4 0 00-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M22 21v-2a4 4 0 00-3-3.87" />
      <path d="M16 3.13a4 4 0 010 7.75" />
    </>
  ),
  buscar: (
    <>
      <circle cx="11" cy="11" r="7" />
      <path d="M21 21l-4.35-4.35" />
    </>
  ),
  menu: (
    <>
      <path d="M3 6h18M3 12h18M3 18h18" />
    </>
  ),
  cerrar: (
    <>
      <path d="M18 6L6 18M6 6l12 12" />
    </>
  ),
  refrescar: (
    <>
      <path d="M21 12a9 9 0 11-2.64-6.36" />
      <path d="M21 3v6h-6" />
    </>
  ),
  descargar: (
    <>
      <path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4" />
      <path d="M7 10l5 5 5-5" />
      <path d="M12 15V3" />
    </>
  ),
  'flecha-volver': (
    <>
      <path d="M19 12H5" />
      <path d="M12 19l-7-7 7-7" />
    </>
  ),
  documento: (
    <>
      <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
      <path d="M14 2v6h6" />
      <path d="M16 13H8M16 17H8M10 9H8" />
    </>
  ),
  carpeta: (
    <>
      <path d="M22 19a2 2 0 01-2 2H4a2 2 0 01-2-2V5a2 2 0 012-2h5l2 3h9a2 2 0 012 2z" />
    </>
  ),
  salir: (
    <>
      <path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4" />
      <path d="M16 17l5-5-5-5" />
      <path d="M21 12H9" />
    </>
  ),
  chevron: (
    <path d="M6 9l6 6 6-6" />
  ),
};

/** Inline outline icon, 16px by default, inherits color from parent. */
export function Icono({ nombre, className = 'h-4 w-4' }: PropsIcono): JSX.Element {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {TRAZOS[nombre]}
    </svg>
  );
}
