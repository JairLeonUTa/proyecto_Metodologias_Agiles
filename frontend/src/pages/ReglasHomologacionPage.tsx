import { reglasApi } from '../api';
import { EncabezadoPagina } from '../components/EncabezadoPagina';
import { usePeticion } from '../hooks/usePeticion';
import { useTituloPagina } from '../hooks/useTituloPagina';

interface Regla {
  titulo: string;
  texto: string;
}

const PARA_SOLICITAR: Regla[] = [
  {
    titulo: 'Haber aprobado el primer período académico',
    texto: 'No hace falta aprobar todas las materias de un nivel completo, pero sí el primer período de la carrera de origen. Reprobar materias en el segundo semestre no impide pedir el cambio.',
  },
  {
    titulo: 'Tener al menos una materia que se pueda homologar',
    texto: 'El estudiante debe pedir por lo menos una materia aprobada que se parezca a una materia de la carrera a la que quiere pasar.',
  },
  {
    titulo: 'Solo materias aprobadas',
    texto: 'Las materias reprobadas aparecen en el historial, pero no se pueden homologar.',
  },
  {
    titulo: 'Materias vigentes',
    texto: 'Solo cuentan materias aprobadas en los últimos 10 años y sin tercera matrícula. Secretaría ya filtra esto en el certificado.',
  },
];

/**
 * Reglas de homologación en lenguaje sencillo (instructivo UTA de cambio de carrera, CAU-P-011, y de
 * movilidad, CAU-P-122). Visible para operadores, docentes y administradores.
 */
export function ReglasHomologacionPage(): JSX.Element {
  useTituloPagina('Reglas de homologación');
  const parametros = usePeticion(() => reglasApi.obtenerParametros('CAU-P-011'), []);
  const minimo = parametros.datos?.theta ?? 80;

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <EncabezadoPagina
        titulo="Reglas de homologación"
        descripcion="Qué se necesita para pedir una homologación y cómo se decide, explicado de forma sencilla."
      />

      <section className="rounded-xl border border-slate-200 bg-white p-5">
        <h2 className="text-lg font-semibold text-ink">1. Para poder pedir el cambio de carrera</h2>
        <ul className="mt-3 space-y-3">
          {PARA_SOLICITAR.map((r) => (
            <li key={r.titulo} className="flex gap-3">
              <span className="text-lg font-bold text-emerald-600" aria-hidden="true">✔</span>
              <div>
                <p className="font-medium text-ink">{r.titulo}</p>
                <p className="text-sm text-muted">{r.texto}</p>
              </div>
            </li>
          ))}
        </ul>
      </section>

      <section className="rounded-xl border border-slate-200 bg-white p-5">
        <h2 className="text-lg font-semibold text-ink">2. Cuándo una materia se puede homologar</h2>
        <p className="mt-2 text-sm text-slate-700">
          La materia aprobada debe equivaler al menos al <strong>{minimo} %</strong> de la materia de la carrera de destino en tres
          aspectos. El agente los mide con los sílabos oficiales de la UTA:
        </p>
        <dl className="mt-3 grid gap-3 sm:grid-cols-3">
          <div className="rounded-lg bg-slate-50 p-3">
            <dt className="font-semibold text-ink">Contenidos</dt>
            <dd className="text-sm text-muted">Qué parte de los temas de la materia de destino ya estudió.</dd>
          </div>
          <div className="rounded-lg bg-slate-50 p-3">
            <dt className="font-semibold text-ink">Profundidad</dt>
            <dd className="text-sm text-muted">Cuántas horas dedicó a esos temas, frente a las que exige la carrera de destino.</dd>
          </div>
          <div className="rounded-lg bg-slate-50 p-3">
            <dt className="font-semibold text-ink">Carga horaria</dt>
            <dd className="text-sm text-muted">Horas totales de la materia aprobada frente a las de la materia de destino.</dd>
          </div>
        </dl>
        <p className="mt-3 text-sm text-slate-700">
          Si los tres llegan al {minimo} %, el agente indica <strong>“Cumple”</strong>. Si alguno no llega, la materia queda{' '}
          <strong>“a criterio del docente”</strong>: no se rechaza sola, la deciden los docentes.
        </p>
      </section>

      <section className="rounded-xl border border-slate-200 bg-white p-5">
        <h2 className="text-lg font-semibold text-ink">3. Quién decide</h2>
        <ol className="mt-3 list-decimal space-y-2 pl-5 text-sm text-slate-700">
          <li>El operador sube el certificado de homologación (PDF) que entrega Secretaría.</li>
          <li>El agente analiza cada materia pedida y explica por qué cumple o no.</li>
          <li>Los dos docentes revisores que indica el certificado revisan el análisis y deciden cada materia.</li>
          <li>
            Una materia queda <strong>homologada</strong> cuando los dos docentes la aprueban. Si no coinciden, la carrera receptora
            resuelve.
          </li>
        </ol>
        <p className="mt-3 text-xs text-faint">
          Base: instructivos de homologación de la UTA (CAU-P-011 cambio de carrera y CAU-P-122 movilidad). La aprobación final depende
          de la evaluación de la carrera receptora.
        </p>
      </section>
    </div>
  );
}
