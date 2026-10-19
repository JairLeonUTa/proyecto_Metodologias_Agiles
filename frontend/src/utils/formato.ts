/** Utilidades de formato para la interfaz en español. */

const FORMATO_FECHA_HORA = new Intl.DateTimeFormat('es-EC', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

const FORMATO_FECHA = new Intl.DateTimeFormat('es-EC', { dateStyle: 'medium' });

/** Formatea una fecha ISO en fecha y hora local. Devuelve '—' si no hay valor. */
export function formatearFechaHora(valor: string | null | undefined): string {
  if (!valor) {
    return '—';
  }
  const fecha = new Date(valor);
  return Number.isNaN(fecha.getTime()) ? '—' : FORMATO_FECHA_HORA.format(fecha);
}

/** Formatea una fecha ISO (sin hora). */
export function formatearFecha(valor: string | null | undefined): string {
  if (!valor) {
    return '—';
  }
  const fecha = new Date(valor);
  return Number.isNaN(fecha.getTime()) ? '—' : FORMATO_FECHA.format(fecha);
}

/** Formatea una fecha `dateOnly` (yyyy-MM-dd) del catálogo normativo. */
export function formatearFechaCorta(valor: string | null | undefined): string {
  if (!valor) {
    return '—';
  }
  const [anio, mes, dia] = valor.split('-');
  if (!anio || !mes || !dia) {
    return valor;
  }
  return `${dia}/${mes}/${anio}`;
}

/** Convierte un tamaño en bytes a una unidad legible. */
export function formatearBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) {
    return '—';
  }
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  const unidades = ['KB', 'MB', 'GB'];
  let valor = bytes / 1024;
  let indice = 0;
  while (valor >= 1024 && indice < unidades.length - 1) {
    valor /= 1024;
    indice += 1;
  }
  return `${valor.toFixed(1)} ${unidades[indice]}`;
}

/** Acorta un hash largo conservando el inicio y el final. */
export function abreviarHash(hash: string | null | undefined, longitud = 10): string {
  if (!hash) {
    return '—';
  }
  return hash.length <= longitud * 2 ? hash : `${hash.slice(0, longitud)}…${hash.slice(-longitud)}`;
}

/** Normaliza un texto para búsquedas sin distinguir mayúsculas ni acentos. */
export function normalizar(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim();
}

/** Indica si un texto tiene forma de identificador GUID. */
export function esGuid(valor: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(valor.trim());
}
