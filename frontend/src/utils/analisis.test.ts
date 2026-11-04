import { describe, expect, it } from 'vitest';
import { extraccionDePrueba } from '../test/fixtures';
import type { Expediente } from '../types/expedientes';
import {
  aSolicitudConfirmacion,
  borradorDesdeExtraccion,
  camposVacios,
  contextoExpediente,
  anosDesdeFecha,
  etapasExtraccion,
  formatearPorcentaje,
  hayErrores,
  nivelConfianza,
  matriculaParaSeleccion,
  porcentajeACoseno,
  presentarEstadoExtraccion,
  rutaAccionExtraccion,
  validarConfirmacion,
} from './analisis';

describe('formato de cifras (mockups)', () => {
  it('usa coma decimal y dos decimales', () => {
    expect(formatearPorcentaje(85.714)).toBe('85,71 %');
    expect(porcentajeACoseno(75)).toBe('0,75');
    expect(formatearPorcentaje(null)).toBe('—');
  });
});

describe('bandeja de procesamiento (S2-A01)', () => {
  it('clasifica la confianza con el umbral 0,80', () => {
    expect(nivelConfianza({ estado: 'CONFIRMADA', confianza: 0.94 })).toBe('alta');
    expect(nivelConfianza({ estado: 'PENDIENTE_CONFIRMACION', confianza: 0.68 })).toBe('baja');
    expect(nivelConfianza({ estado: 'PENDIENTE_OCR_LLM', confianza: 0 })).toBe('sin-dato');
  });

  it('asigna etiqueta y acción según el estado', () => {
    expect(presentarEstadoExtraccion('PENDIENTE_CONFIRMACION')).toMatchObject({
      etiqueta: 'Confirmación pendiente',
      accion: 'Confirmar datos',
    });
    expect(presentarEstadoExtraccion('PENDIENTE_OCR_LLM').etiqueta).toBe('Pendiente OCR / LLM');
    expect(presentarEstadoExtraccion('CANCELADA')).toMatchObject({
      etiqueta: 'Extracción cancelada',
      tono: 'error',
    });
    expect(rutaAccionExtraccion({ id: 'x', estado: 'PENDIENTE_CONFIRMACION' })).toBe('/procesamiento/x/confirmar');
  });

  it('marca OCR y LLM como no disponibles cuando la extracción se cancela', () => {
    const etapas = etapasExtraccion(extraccionDePrueba({ estado: 'CANCELADA', unidades: [] }));

    expect(etapas[1]).toMatchObject({ estado: 'fallida', detalle: 'OCR no disponible' });
    expect(etapas[2]).toMatchObject({ estado: 'fallida', detalle: 'LLM no ejecutado' });
    expect(etapas[3].estado).toBe('pendiente');
    expect(rutaAccionExtraccion({ id: 'x', estado: 'PENDIENTE_OCR_LLM' })).toBe('/procesamiento/x/estado');
  });

  it('une la extracción con su expediente de MS B o usa un código corto', () => {
    const extraccion = extraccionDePrueba();
    expect(contextoExpediente(extraccion).codigo).toBe('EXP-AAAAAAAA');

    const expediente = {
      id: extraccion.expedienteId,
      codigo: 'EXP-20261022-7B03C2',
      tipoTramite: 'EXTERNO',
      rutaNormativa: 'CAU-P-122',
      institucionOrigen: 'Institución de prueba',
      carreraOrigenCodigo: 'UC',
      carreraDestinoCodigo: 'SW',
      documentos: [{ id: 'doc-1', nombreArchivo: 'silabo-bases-datos.pdf' }],
      asignaturas: [{ codigoOrigen: 'bd101', nombreOrigen: 'BD', codigoDestino: 'SW205', nota: 8, fechaAprobacionCertificada: '2023-11-05', numeroMatriculaDestino: 2 }],
    } as unknown as Expediente;

    expect(contextoExpediente(extraccion, expediente)).toMatchObject({
      codigo: 'EXP-20261022-7B03C2',
      documento: 'silabo-bases-datos.pdf',
      rutaAcademica: 'UC → SW',
      codigoDestino: 'SW205',
      institucionOrigen: 'Institución de prueba',
      fechaAprobacionCertificada: '2023-11-05',
      numeroMatriculaDestino: 2,
    });
  });

  it('calcula años completos desde la fecha certificada y deja fechas ausentes o inválidas sin derivar', () => {
    expect(anosDesdeFecha('2023-11-05', new Date(2026, 10, 4))).toBe(2);
    expect(anosDesdeFecha('2025-11-05', new Date(2026, 10, 4))).toBe(0);
    expect(anosDesdeFecha(null, new Date(2026, 10, 4))).toBeNull();
    expect(anosDesdeFecha('fecha-invalida', new Date(2026, 10, 4))).toBeNull();
  });
});

describe('matrícula para análisis', () => {
  it('agrupa la tercera matrícula y las posteriores en la opción de bloqueo RN-16', () => {
    expect(matriculaParaSeleccion(null)).toBe('');
    expect(matriculaParaSeleccion(1)).toBe('1');
    expect(matriculaParaSeleccion(2)).toBe('2');
    expect(matriculaParaSeleccion(9)).toBe('3');
  });
});

describe('confirmación de datos (S2-A02 / S2-A03)', () => {
  it('bloquea la confirmación si faltan los créditos', () => {
    const borrador = borradorDesdeExtraccion(extraccionDePrueba());
    const errores = validarConfirmacion(borrador);
    expect(errores.creditos).toBeDefined();
    expect(hayErrores(errores)).toBe(true);
  });

  it('acepta datos completos y arma el cuerpo del PUT', () => {
    const borrador = { ...borradorDesdeExtraccion(extraccionDePrueba()), creditos: '3' };
    expect(hayErrores(validarConfirmacion(borrador))).toBe(false);
    expect(aSolicitudConfirmacion(borrador)).toMatchObject({
      creditos: 3,
      horas: 64,
      unidades: [
        { numero: 1, nombre: 'Introducción a las bases de datos', horas: 8 },
        { numero: 2, nombre: 'Modelo entidad-relación', horas: 12 },
      ],
    });
  });

  it('rechaza unidades duplicadas, sin nombre o sin horas, y la lista vacía', () => {
    const base = { ...borradorDesdeExtraccion(extraccionDePrueba()), creditos: '3' };
    const duplicada = { ...base, unidades: [base.unidades[0], { ...base.unidades[1], numero: '1' }] };
    expect(Object.values(validarConfirmacion(duplicada).porUnidad)).toContain('La unidad U.1 está repetida.');

    const sinHoras = { ...base, unidades: [{ ...base.unidades[0], horas: '' }] };
    expect(hayErrores(validarConfirmacion(sinHoras))).toBe(true);

    expect(validarConfirmacion({ ...base, unidades: [] }).unidades).toMatch(/al menos una unidad/);
    expect(validarConfirmacion({ ...base, creditos: '31' }).creditos).toBeDefined();
  });
});

describe('extracción ampliada OCR/LLM (S2-A05)', () => {
  it('marca OCR y LLM en curso mientras el PDF escaneado se procesa', () => {
    const etapas = etapasExtraccion(extraccionDePrueba({ estado: 'PENDIENTE_OCR_LLM', unidades: [] }));
    expect(etapas.map((e) => e.estado)).toEqual(['completada', 'en-curso', 'en-curso', 'pendiente', 'pendiente']);
    expect(etapas[1].detalle).toBe('OCR en curso');
  });

  it('pide validación del operador cuando hay datos dudosos', () => {
    const etapas = etapasExtraccion(extraccionDePrueba());
    expect(etapas[3]).toMatchObject({ estado: 'en-curso', detalle: 'Validación operador requerida' });
  });

  it('lista los campos que el sistema dejó vacíos en lugar de inventarlos', () => {
    const extraccion = extraccionDePrueba({
      unidades: [{ numero: 6, nombre: 'Recursividad', horas: 0, contenidos: '' }],
    });
    expect(camposVacios(extraccion)).toEqual(['créditos', 'horas de U.6']);
  });
});
