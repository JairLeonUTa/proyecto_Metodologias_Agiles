import type { ReactNode } from 'react';

interface PropsLayoutAutenticacion {
  titulo: string;
  subtitulo: string;
  children: ReactNode;
  /** Contenido adicional bajo el formulario (credenciales de prueba, avisos). */
  pie?: ReactNode;
}

/** Marco de las vistas públicas (inicio de sesión y acceso denegado). */
export function LayoutAutenticacion({ titulo, subtitulo, children, pie }: PropsLayoutAutenticacion): JSX.Element {
  return (
    <div className="flex min-h-screen flex-col bg-gradient-to-br from-slate-900 via-institucional-900 to-slate-800">
      <a className="salto-contenido" href="#contenido-autenticacion">
        Saltar al contenido principal
      </a>

      <div className="flex flex-1 items-center justify-center px-4 py-10">
        <div className="w-full max-w-md space-y-5">
          <div className="flex items-center gap-3 text-white">
            <span
              aria-hidden="true"
              className="flex h-11 w-11 items-center justify-center rounded-md bg-institucional-600 text-base font-bold"
            >
              IA
            </span>
            <div>
              <p className="text-sm font-bold uppercase tracking-wide">SIHA-IA</p>
              <p className="text-xs text-slate-300">Universidad Técnica de Ambato</p>
            </div>
          </div>

          <div id="contenido-autenticacion" className="tarjeta p-6">
            <header className="mb-5">
              <h1 className="text-lg font-bold text-slate-900">{titulo}</h1>
              <p className="mt-1 text-sm text-slate-600">{subtitulo}</p>
            </header>

            {children}
          </div>

          {pie}
        </div>
      </div>

      <footer className="px-4 py-4 text-center text-xs text-slate-400">
        Sistema Inteligente de Homologación Académica Asistido por Agentes IA · Sprint 1
      </footer>
    </div>
  );
}
