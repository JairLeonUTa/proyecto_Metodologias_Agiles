import type { TonoEtiqueta } from '../components/ui/Etiqueta';
import type { ConfirmarExtraccion, Extraccion } from '../types/analisis';
import { ESTADO_FINAL, ESTADOS_EXTRACCION, UMBRAL_CONFIANZA } from '../types/analisis';
import type { Expediente } from '../types/expedientes';

/** Utilidades de presentación del Sprint 2 (US-07 a US-11). */

const DECIMAL_2 = new Intl.NumberFormat('es-EC', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** 0.68 → "0,68" (formato de los mockups M-01 a M-06). */
export function formatearDecimal(valor: number | null | undefined): string {
  return valor === null || valor === undefined || Number.isNaN(valor) ? '—' : DECIMAL_2.format(valor);
}

/** 85.714 → "85,71 %". */
export function formatearPorcentaje(valor: number | null | undefined): string {
  return valor === null || valor === undefined || Number.isNaN(valor) ? '—' : `${DECIMAL_2.format(valor)} %`;
}

/** Similitud o τ en porcentaje (0–100) → coseno 0–1 con coma ("0,75"). */
export function porcentajeACoseno(valor: number | null | undefined): string {
  return valor === null || valor === undefined ? '—' : formatearDecimal(valor / 100);
}

/** Confianza alta, baja o sin dato (PDF escaneado aún sin leer). */
export function nivelConfianza(extraccion: Pick<Extraccion, 'estado' | 'confianza'>): 'alta' | 'baja' | 'sin-dato' {
  if (extraccion.estado === ESTADOS_EXTRACCION.PENDIENTE_OCR_LLM && !extraccion.confianza) {
    return 'sin-dato';
  }
  return extraccion.confianza >= UMBRAL_CONFIANZA ? 'alta' : 'baja';
}

export interface PresentacionEstado {
  etiqueta: string;
  tono: TonoEtiqueta;
  accion: string;
}

/** Texto, color y acción de la bandeja para cada estado de extracción (M-01). */
export function presentarEstadoExtraccion(estado: string): PresentacionEstado {
  switch (estado) {
    case ESTADOS_EXTRACCION.CONFIRMADA:
      return { etiqueta: 'Datos extraídos', tono: 'exito', accion: 'Ver detalle' };
    case ESTADOS_EXTRACCION.PENDIENTE_CONFIRMACION:
      return { etiqueta: 'Confirmación pendiente', tono: 'advertencia', accion: 'Confirmar datos' };
    case ESTADOS_EXTRACCION.PENDIENTE_OCR_LLM:
      return { etiqueta: 'Pendiente OCR / LLM', tono: 'informacion', accion: 'Ver estado' };
    default:
      return { etiqueta: estado || 'Desconocido', tono: 'neutro', accion: 'Ver detalle' };
  }
}

/** Ruta de la acción principal de una fila de la bandeja. */
export function rutaAccionExtraccion(extraccion: Pick<Extraccion, 'id' | 'estado'>): string {
  return extraccion.estado === ESTADOS_EXTRACCION.PENDIENTE_CONFIRMACION
    ? `/procesamiento/${extraccion.id}/confirmar`
    : `/procesamiento/${extraccion.id}/estado`;
}

export interface ContextoExpediente {
  codigo: string;
  tipoTramite: string | null;
  rutaAcademica: string | null;
  rutaNormativa: string | null;
  documento: string | null;
  codigoDestino: string | null;
}

/**
 * Une la extracción (MS C) con su expediente (MS B) para mostrar código, trámite y PDF.
 * Si el rol no puede leer MS B, devuelve un identificador corto en su lugar.
 */
export function contextoExpediente(extraccion: Extraccion, expediente?: Expediente | null): ContextoExpediente {
  if (!expediente) {
    return {
      codigo: `EXP-${extraccion.expedienteId.slice(0, 8).toUpperCase()}`,
      tipoTramite: null,
      rutaAcademica: null,
      rutaNormativa: null,
      documento: null,
      codigoDestino: null,
    };
  }

  const documento = expediente.documentos.find((doc) => doc.id === extraccion.documentoId);
  const asignatura = expediente.asignaturas.find(
    (item) => item.codigoOrigen.toUpperCase() === extraccion.codigoAsignaturaOrigen.toUpperCase(),
  );

  return {
    codigo: expediente.codigo,
    tipoTramite: expediente.tipoTramite,
    rutaAcademica: `${expediente.carreraOrigenCodigo} → ${expediente.carreraDestinoCodigo}`,
    rutaNormativa: expediente.rutaNormativa,
    documento: documento?.nombreArchivo ?? null,
    codigoDestino: asignatura?.codigoDestino ?? null,
  };
}

/** Borrador editable de la confirmación (M-02). Los números vacíos quedan como cadena vacía. */
export interface BorradorConfirmacion {
  creditos: string;
  horas: string;
  observacion: string;
  unidades: Array<{ clave: string; numero: string; nombre: string; horas: string; contenidos: string }>;
}

let contadorClaves = 0;
export function nuevaClave(): string {
  contadorClaves += 1;
  return `u-${contadorClaves}`;
}

export function borradorDesdeExtraccion(extraccion: Extraccion): BorradorConfirmacion {
  return {
    creditos: extraccion.creditos === null ? '' : String(extraccion.creditos),
    horas: extraccion.horas === null ? '' : String(extraccion.horas),
    observacion: extraccion.observacion ?? '',
    unidades: [...extraccion.unidades]
      .sort((a, b) => a.numero - b.numero)
      .map((unidad) => ({
        clave: nuevaClave(),
        numero: String(unidad.numero),
        nombre: unidad.nombre,
        horas: unidad.horas ? String(unidad.horas) : '',
        contenidos: unidad.contenidos ?? '',
      })),
  };
}

function entero(valor: string): number | null {
  if (!/^\d+$/.test(valor.trim())) {
    return null;
  }
  return Number(valor.trim());
}

export interface ErroresConfirmacion {
  creditos?: string;
  horas?: string;
  unidades?: string;
  /** Errores por clave de unidad. */
  porUnidad: Record<string, string>;
}

/**
 * Validación del cliente, idéntica a `ExtraccionService.Validar` en MS C:
 * créditos 1–30, horas 1–2000, ≥ 1 unidad, número 1–50 sin duplicados, nombre ≤ 200, horas 1–1000.
 */
export function validarConfirmacion(borrador: BorradorConfirmacion): ErroresConfirmacion {
  const errores: ErroresConfirmacion = { porUnidad: {} };

  const creditos = entero(borrador.creditos);
  if (creditos === null || creditos < 1 || creditos > 30) {
    errores.creditos = 'Ingrese los créditos (entero entre 1 y 30).';
  }

  const horas = entero(borrador.horas);
  if (horas === null || horas < 1 || horas > 2000) {
    errores.horas = 'Ingrese las horas totales (entero entre 1 y 2000).';
  }

  if (borrador.unidades.length === 0) {
    errores.unidades = 'Debe incluir al menos una unidad temática.';
  }

  const vistos = new Set<number>();
  for (const unidad of borrador.unidades) {
    const numero = entero(unidad.numero);
    const horasUnidad = entero(unidad.horas);
    const nombre = unidad.nombre.trim();

    if (numero === null || numero < 1 || numero > 50) {
      errores.porUnidad[unidad.clave] = 'Número de unidad entre 1 y 50.';
    } else if (vistos.has(numero)) {
      errores.porUnidad[unidad.clave] = `La unidad U.${numero} está repetida.`;
    } else if (!nombre || nombre.length > 200) {
      errores.porUnidad[unidad.clave] = 'El nombre es obligatorio (máximo 200 caracteres).';
    } else if (horasUnidad === null || horasUnidad < 1 || horasUnidad > 1000) {
      errores.porUnidad[unidad.clave] = 'Horas de la unidad entre 1 y 1000.';
    } else if (unidad.contenidos.length > 4000) {
      errores.porUnidad[unidad.clave] = 'Los contenidos admiten máximo 4000 caracteres.';
    }

    if (numero !== null) {
      vistos.add(numero);
    }
  }

  return errores;
}

export function hayErrores(errores: ErroresConfirmacion): boolean {
  return Boolean(
    errores.creditos || errores.horas || errores.unidades || Object.keys(errores.porUnidad).length > 0,
  );
}

export function aSolicitudConfirmacion(borrador: BorradorConfirmacion): ConfirmarExtraccion {
  return {
    creditos: Number(borrador.creditos),
    horas: Number(borrador.horas),
    observacion: borrador.observacion.trim() || null,
    unidades: borrador.unidades.map((unidad) => ({
      numero: Number(unidad.numero),
      nombre: unidad.nombre.trim(),
      horas: Number(unidad.horas),
      contenidos: unidad.contenidos.trim(),
    })),
  };
}

/** Etapas del pipeline de extracción ampliada (M-04). */
export type EstadoEtapa = 'completada' | 'en-curso' | 'pendiente';

export interface EtapaExtraccion {
  titulo: string;
  detalle: string;
  estado: EstadoEtapa;
}

/**
 * Deriva las cinco etapas del estado de la extracción (S2-A05):
 * "OCR en curso", "LLM analizando", "Validación operador requerida".
 */
export function etapasExtraccion(extraccion: Extraccion): EtapaExtraccion[] {
  const { estado } = extraccion;
  const ocrLlm = estado === ESTADOS_EXTRACCION.PENDIENTE_OCR_LLM;
  const porConfirmar = estado === ESTADOS_EXTRACCION.PENDIENTE_CONFIRMACION;
  const confirmada = estado === ESTADOS_EXTRACCION.CONFIRMADA;
  const unidades = extraccion.unidades.length;

  return [
    { titulo: '1. PDF recibido', detalle: 'Hash SHA-256 verificado', estado: 'completada' },
    {
      titulo: '2. OCR',
      detalle: ocrLlm ? 'OCR en curso' : 'Texto disponible',
      estado: ocrLlm ? 'en-curso' : 'completada',
    },
    {
      titulo: '3. LLM analizando',
      detalle: ocrLlm ? 'Ollama · en espera del texto' : `${unidades} unidad(es) estructuradas`,
      estado: ocrLlm ? 'en-curso' : 'completada',
    },
    {
      titulo: '4. Validación del operador',
      detalle: confirmada
        ? `Confirmado por ${extraccion.confirmadoPor ?? 'el operador'}`
        : porConfirmar
          ? 'Validación operador requerida'
          : 'Pendiente',
      estado: confirmada ? 'completada' : porConfirmar ? 'en-curso' : 'pendiente',
    },
    {
      titulo: '5. Cálculo',
      detalle: confirmada ? 'Listo para el agente' : 'En espera',
      estado: confirmada ? 'en-curso' : 'pendiente',
    },
  ];
}

/** Campos que el extractor dejó vacíos en lugar de inventarlos (CA1 de US-10). */
export function camposVacios(extraccion: Extraccion): string[] {
  const vacios: string[] = [];
  if (extraccion.creditos === null) vacios.push('créditos');
  if (extraccion.horas === null) vacios.push('horas totales');
  if (extraccion.unidades.length === 0) vacios.push('unidades temáticas');
  for (const unidad of extraccion.unidades) {
    if (!unidad.horas) vacios.push(`horas de U.${unidad.numero}`);
  }
  return vacios;
}

export interface PresentacionEstadoFinal {
  etiqueta: string;
  tono: 'exito' | 'advertencia' | 'error' | 'informacion';
  descripcion: string;
}

export function presentarEstadoFinal(estado: number): PresentacionEstadoFinal {
  switch (estado) {
    case ESTADO_FINAL.LISTA_PARA_DICTAMEN:
      return { etiqueta: 'Lista para dictamen', tono: 'exito', descripcion: 'Sin bloqueos: el docente puede revisar y emitir su dictamen.' };
    case ESTADO_FINAL.BLOQUEO_TERCERA_MATRICULA:
      return { etiqueta: 'Bloqueo por tercera matrícula', tono: 'error', descripcion: 'RN-16: la tercera matrícula en la malla destino impide continuar.' };
    case ESTADO_FINAL.BLOQUEO_RN19:
      return { etiqueta: 'Bloqueo RN-19', tono: 'error', descripcion: 'Una asignatura de origen no puede usarse para dos destinos del mismo expediente.' };
    case ESTADO_FINAL.REQUIERE_VALIDACION_CONOCIMIENTOS:
      return { etiqueta: 'Validación de conocimientos', tono: 'advertencia', descripcion: 'RN-03: la asignatura supera los 10 años de vigencia (CAU-P-011).' };
    case ESTADO_FINAL.ERROR_REGLAS_GRPC:
      return { etiqueta: 'Reglas no disponibles', tono: 'error', descripcion: 'No se obtuvieron θ, δ, τ del Microservicio A. El sistema no sustituye valores.' };
    case ESTADO_FINAL.EXTRACCION_NO_CONFIRMADA:
      return { etiqueta: 'Extracción sin confirmar', tono: 'advertencia', descripcion: 'El operador debe confirmar los datos antes del cálculo.' };
    default:
      return { etiqueta: `Estado ${estado}`, tono: 'informacion', descripcion: '' };
  }
}

export function presentarCategoriaCreditos(categoria: string): { etiqueta: string; tono: TonoEtiqueta } {
  switch (categoria) {
    case 'NORMAL':
      return { etiqueta: 'Admisible', tono: 'exito' };
    case 'VALIDACION_CONOCIMIENTOS':
      return { etiqueta: 'Validación de conocimientos', tono: 'advertencia' };
    case 'BLOQUEO_3RA_MATRICULA':
      return { etiqueta: 'Bloqueada (3.ª matrícula)', tono: 'error' };
    default:
      return { etiqueta: categoria, tono: 'neutro' };
  }
}

/** "Hace 20 s" para el indicador de actualización (M-04). */
export function haceSegundos(desde: number, ahora: number): string {
  const segundos = Math.max(0, Math.round((ahora - desde) / 1000));
  if (segundos < 60) return `Actualizado hace ${segundos} s`;
  return `Actualizado hace ${Math.round(segundos / 60)} min`;
}
