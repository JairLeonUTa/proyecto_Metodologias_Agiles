import { HORAS_POR_CREDITO, PDF } from '../types/comunes';

/** Mensajes de validación locales, redactados de forma consistente en toda la aplicación (S1-A10). */

export function esCorreoInstitucional(valor: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(valor.trim());
}

/** Valida los umbrales heurísticos θ, δ, τ (S1-A03). Devuelve el mensaje de error o `null`. */
export function validarParametros(theta: number, delta: number, tau: number): string | null {
  for (const [nombre, valor] of [
    ['θ', theta],
    ['δ', delta],
    ['τ', tau],
  ] as const) {
    if (!Number.isFinite(valor)) {
      return `El valor de ${nombre} es obligatorio.`;
    }
    if (valor < 0 || valor > 100) {
      return `El valor de ${nombre} debe estar entre 0 y 100.`;
    }
  }

  if (delta > theta) {
    return 'La banda de tolerancia δ no puede ser mayor que el umbral θ.';
  }

  return null;
}

/** Valida localmente un archivo PDF antes de la carga (S1-A07). */
export function validarArchivoPdf(archivo: File): string | null {
  const nombre = archivo.name.toLowerCase();
  const tipoValido = archivo.type === PDF.MIME || archivo.type === '';
  if (!tipoValido || !nombre.endsWith(PDF.EXTENSION)) {
    return `El archivo "${archivo.name}" no es un PDF válido. Solo se admiten documentos .pdf.`;
  }

  if (archivo.size === 0) {
    return `El archivo "${archivo.name}" está vacío.`;
  }

  if (archivo.size > PDF.TAMANO_MAXIMO_BYTES) {
    return `El archivo "${archivo.name}" excede el tamaño máximo de 10 MB.`;
  }

  return null;
}

/** Valida la coherencia entre créditos y horas de una asignatura. */
export function validarHoras(creditos: number, horas: number): string | null {
  if (horas <= 0) {
    return 'Las horas de la asignatura deben ser mayores que cero.';
  }
  if (creditos > 0 && horas !== creditos * HORAS_POR_CREDITO) {
    return `Las ${horas} horas no corresponden a ${creditos} créditos (la UTA usa ${HORAS_POR_CREDITO} h/crédito).`;
  }
  return null;
}

/** Convierte un texto de entrada en número, aceptando coma decimal. */
export function aNumero(valor: string): number {
  if (valor.trim() === '') {
    return Number.NaN;
  }
  return Number(valor.replace(',', '.'));
}

/** Calcula el hash SHA-256 de un archivo en el navegador (Web Crypto). */
export async function calcularSha256(archivo: File): Promise<string | null> {
  try {
    if (!window.crypto?.subtle) {
      return null;
    }
    const contenido = await archivo.arrayBuffer();
    const resumen = await window.crypto.subtle.digest('SHA-256', contenido);
    return Array.from(new Uint8Array(resumen))
      .map((byte) => byte.toString(16).padStart(2, '0'))
      .join('');
  } catch {
    return null;
  }
}
