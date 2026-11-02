import type { ReactNode } from 'react';

interface PropsLayoutAutenticacion {
  titulo: string;
  subtitulo: string;
  children: ReactNode;
  pie?: ReactNode;
}

/**
 * Marco de las vistas públicas: composición centrada. Una cabecera con formas
 * orgánicas superpuestas en los azules del logo sostiene la identidad
 * (Homologa UTA · Universidad Técnica de Ambato) y la hoja blanca con el
 * formulario se levanta sobre ese campo. Sin paneles oscuros — la hoja ES el
 * documento.
 */
export function LayoutAutenticacion({ titulo, subtitulo, children, pie }: PropsLayoutAutenticacion): JSX.Element {
  return (
    <div className="flex min-h-screen flex-col bg-frame">
      <a className="salto-contenido" href="#contenido-autenticacion">
        Saltar al contenido principal
      </a>

      <header className="relative isolate overflow-hidden bg-institucional-50 pb-48 pt-10 sm:pb-52 sm:pt-14">
        {/* Formas pálidas del fondo de la cabecera */}
        <div aria-hidden="true" className="absolute inset-0 -z-10 overflow-hidden">
          <div className="absolute -left-[16%] -top-[24%] h-[62%] w-[46%] rounded-[56%_44%_48%_52%/46%_56%_44%_54%] bg-institucional-100" />
          <div className="absolute -right-[14%] -top-[28%] h-[66%] w-[44%] rounded-[46%_54%_52%_48%/54%_46%_56%_44%] bg-institucional-100" />
        </div>

        {/* Campo de formas orgánicas superpuestas en la base de la cabecera */}
        <div aria-hidden="true" className="absolute inset-x-0 bottom-0 z-0 h-44 overflow-hidden sm:h-48">
          <div className="absolute left-[-8%] top-[18%] h-[130%] w-[38%] rounded-[46%_54%_58%_42%/52%_48%_58%_42%] bg-institucional-900" />
          <div className="absolute left-[18%] top-[-4%] h-[126%] w-[32%] rounded-[56%_44%_46%_54%/42%_58%_44%_56%] bg-institucional-300" />
          <div className="absolute left-[40%] top-[26%] h-[122%] w-[28%] rounded-[44%_56%_54%_46%/58%_42%_56%_44%] bg-institucional-600" />
          <div className="absolute right-[24%] top-[34%] h-[112%] w-[22%] rounded-[48%_52%_58%_42%/56%_44%_58%_42%] bg-turquesa" />
          <div className="absolute right-[-6%] top-[6%] h-[128%] w-[34%] rounded-[52%_48%_44%_56%/48%_52%_46%_54%] bg-institucional-700" />
        </div>

        <div className="relative mx-auto flex max-w-3xl flex-col items-center px-6 text-center">
          <img src="/logo.png" alt="Homologa UTA" className="h-11 w-auto sm:h-12" />
          <p className="mt-5 text-sm font-medium text-muted">Universidad Técnica de Ambato</p>

          <p className="mt-7 max-w-xl text-[1.75rem] font-semibold leading-tight tracking-tight text-institucional-900 sm:text-[2rem]">
            Homologación académica
          </p>
          <p className="mt-3 max-w-lg text-sm leading-relaxed text-muted sm:text-base">
            Evaluación de solicitudes con reglas normativas, umbrales configurables y trazabilidad completa del
            expediente.
          </p>
        </div>
      </header>

      {/* Formulario: hoja blanca centrada, levantada sobre el campo azul */}
      <main
        id="contenido-autenticacion"
        tabIndex={-1}
        className="flex flex-1 items-start justify-center px-4 pb-12 pt-8 sm:pt-0"
      >
        <div className="relative z-10 w-full max-w-md sm:-mt-16">
          <div className="sheet-animate rounded-2xl border border-stone-200 bg-white p-6 sm:p-8">
            <h1 className="text-xl font-semibold tracking-tight text-ink">{titulo}</h1>
            <p className="mt-1.5 text-sm text-muted">{subtitulo}</p>

            <div className="mt-6">{children}</div>

            {pie ? <div className="mt-6">{pie}</div> : null}
          </div>
        </div>
      </main>
    </div>
  );
}
