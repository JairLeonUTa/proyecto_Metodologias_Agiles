/**
 * Períodos académicos ordinarios (grado) de la Universidad Técnica de Ambato, para sugerirlos en el
 * historial del registro manual. Son solo sugerencias: el campo sigue aceptando texto libre (movilidad
 * externa usa los períodos de otra universidad).
 *
 * Fuentes (consultadas el 2026-11-25):
 * - uta.edu.ec/v2.0/calendario-academico: el sitio solo publica los calendarios desde AGOSTO 2025 - ENERO 2026.
 * - Períodos anteriores: resoluciones CAU y trabajos de titulación del repositorio UTA (repositorio.uta.edu.ec),
 *   que usan Abril-Septiembre / Octubre-Marzo hasta 2022 y Abril-Agosto / Septiembre-Febrero / Marzo-Agosto después.
 */
export interface PeriodoUta {
  nombre: string;
  /** Mes en que empieza (AAAA-MM): no se sugieren períodos que todavía no empiezan. */
  inicio: string;
}

export const PERIODOS_UTA: readonly PeriodoUta[] = [
  { nombre: 'ABRIL - SEPTIEMBRE 2015', inicio: '2015-04' },
  { nombre: 'OCTUBRE 2015 - MARZO 2016', inicio: '2015-10' },
  { nombre: 'ABRIL - SEPTIEMBRE 2016', inicio: '2016-04' },
  { nombre: 'OCTUBRE 2016 - MARZO 2017', inicio: '2016-10' },
  { nombre: 'ABRIL - SEPTIEMBRE 2017', inicio: '2017-04' },
  { nombre: 'OCTUBRE 2017 - MARZO 2018', inicio: '2017-10' },
  { nombre: 'ABRIL - SEPTIEMBRE 2018', inicio: '2018-04' },
  { nombre: 'OCTUBRE 2018 - MARZO 2019', inicio: '2018-10' },
  { nombre: 'ABRIL - SEPTIEMBRE 2019', inicio: '2019-04' },
  { nombre: 'OCTUBRE 2019 - MARZO 2020', inicio: '2019-10' },
  { nombre: 'ABRIL - SEPTIEMBRE 2020', inicio: '2020-04' },
  { nombre: 'OCTUBRE 2020 - MARZO 2021', inicio: '2020-10' },
  { nombre: 'ABRIL - SEPTIEMBRE 2021', inicio: '2021-04' },
  { nombre: 'OCTUBRE 2021 - MARZO 2022', inicio: '2021-10' },
  { nombre: 'ABRIL - SEPTIEMBRE 2022', inicio: '2022-04' },
  { nombre: 'OCTUBRE 2022 - MARZO 2023', inicio: '2022-10' },
  { nombre: 'ABRIL - AGOSTO 2023', inicio: '2023-04' },
  { nombre: 'SEPTIEMBRE 2023 - FEBRERO 2024', inicio: '2023-09' },
  { nombre: 'MARZO - AGOSTO 2024', inicio: '2024-03' },
  { nombre: 'SEPTIEMBRE 2024 - FEBRERO 2025', inicio: '2024-09' },
  { nombre: 'MARZO - AGOSTO 2025', inicio: '2025-03' },
  { nombre: 'AGOSTO 2025 - ENERO 2026', inicio: '2025-08' },
  { nombre: 'ENERO - JULIO 2026', inicio: '2026-01' },
  { nombre: 'JULIO - DICIEMBRE 2026', inicio: '2026-07' },
];

/** Igual que el lector del certificado: una materia aprobada hace más de 10 años no es vigente. */
export const VIGENCIA_MAXIMA_ANOS = 10;

/** Año más alto que aparece en el nombre ("OCTUBRE 2015 - MARZO 2016" → 2016), como lo lee el backend. */
export function anioDe(nombre: string): number {
  const anios = nombre.match(/\b(19|20)\d{2}\b/g) ?? [];
  return anios.reduce((max, a) => Math.max(max, Number(a)), 0);
}

/**
 * Períodos que se pueden elegir hoy: ya empezaron y siguen vigentes (no tienen más de 10 años).
 * Del más reciente al más antiguo, que es lo que más se usa.
 */
export function periodosSugeridos(hoy: Date = new Date()): string[] {
  const anio = hoy.getFullYear();
  const mes = `${anio}-${String(hoy.getMonth() + 1).padStart(2, '0')}`;
  return PERIODOS_UTA.filter((p) => p.inicio <= mes && anioDe(p.nombre) >= anio - VIGENCIA_MAXIMA_ANOS)
    .map((p) => p.nombre)
    .reverse();
}
