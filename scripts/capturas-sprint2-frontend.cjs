// Uso: npx vite preview --port 4173 (en frontend/) y luego: node scripts/capturas-sprint2-frontend.cjs docs/evidencias/sprint2-frontend
// Capturas de las pantallas del Sprint 2 con respuestas simuladas del Gateway.
const { chromium } = require('playwright');
const OUT = process.argv[2];
const BASE = 'http://localhost:4173';
const now = '2026-11-03T15:10:00Z';
const exp = (id, codigo, tipo, o, d, doc, docId, asig, dest) => ({ id, codigo, tipoTramite: tipo, rutaNormativa: tipo==='EXTERNO'?'CAU-P-122':'CAU-P-011', carreraOrigenCodigo: o, carreraDestinoCodigo: d, estado: 'EN_PROCESO', correlationId: id, eventoPublicado: true, intentosPublicacion: 1, ultimoErrorPublicacion: null, fechaCreacionUtc: now, fechaPublicacionUtc: now, documentos: [{ id: docId, nombreArchivo: doc }], asignaturas: [{ codigoOrigen: asig, nombreOrigen: '', codigoDestino: dest, nota: 8 }] });
const u = (n, nombre, horas, c='') => ({ numero: n, nombre, horas, contenidos: c || nombre });
const ex = [
  { id: 'e1', expedienteId: 'x1', documentoId: 'd1', codigoAsignaturaOrigen: 'TI101', nombreAsignaturaOrigen: 'Fundamentos de Programación', creditos: 4, horas: 84, confianza: 0.94, estado: 'CONFIRMADA', requiereConfirmacion: false, unidades: [u(1,'Lógica de programación',12),u(2,'Datos y operadores',12),u(3,'Condicionales y bucles',12),u(4,'Subprogramas',12),u(5,'Vectores y matrices',12),u(6,'Recursividad',12)], creadoUtc: '2026-10-22T14:10:00Z', confirmadoUtc: now, confirmadoPor: 'operador@uta.edu.ec', observacion: null },
  { id: 'e2', expedienteId: 'x2', documentoId: 'd2', codigoAsignaturaOrigen: 'UC-BD1', nombreAsignaturaOrigen: 'Bases de Datos I', creditos: null, horas: 64, confianza: 0.68, estado: 'PENDIENTE_CONFIRMACION', requiereConfirmacion: true, unidades: [u(1,'Introducción a las bases de datos',8),u(2,'Modelo entidad-relación',12),u(3,'Modelo relacional y normalización',12),u(4,'Lenguaje SQL',16),u(5,'Transaccion es y concurrencia',8)], creadoUtc: '2026-10-22T14:42:00Z', confirmadoUtc: null, confirmadoPor: null, observacion: null },
  { id: 'e3', expedienteId: 'x3', documentoId: 'd3', codigoAsignaturaOrigen: 'ESPE-POO', nombreAsignaturaOrigen: '', creditos: null, horas: null, confianza: 0, estado: 'PENDIENTE_OCR_LLM', requiereConfirmacion: false, unidades: [], creadoUtc: '2026-10-23T13:05:00Z', confirmadoUtc: null, confirmadoPor: null, observacion: null },
  { id: 'e4', expedienteId: 'x4', documentoId: 'd4', codigoAsignaturaOrigen: 'IND-FIS', nombreAsignaturaOrigen: 'Física I', creditos: 4, horas: 96, confianza: 0.88, estado: 'CONFIRMADA', requiereConfirmacion: false, unidades: [u(1,'Cinemática',48),u(2,'Dinámica',48)], creadoUtc: '2026-10-23T15:30:00Z', confirmadoUtc: now, confirmadoPor: 'operador@uta.edu.ec', observacion: null },
];
const exps = [exp('x1','EXP-20261022-4F2A91','INTERNO','TI','SW','plan-analitico-programacion.pdf','d1','TI101','SW101'), exp('x2','EXP-20261022-7B03C2','EXTERNO','U. Cuenca','SW','silabo-bases-datos.pdf','d2','UC-BD1','SW205'), exp('x3','EXP-20261023-91D35E','EXTERNO','ESPE','SW','notas-escaneadas.pdf','d3','ESPE-POO','SW110'), exp('x4','EXP-20261023-A1E7F0','INTERNO','IND','SW','plan-analitico-fisica.pdf','d4','IND-FIS','SW120')];
const dest = ['Algoritmos y diagramas de flujo','Variables y tipos de datos','Estructuras de control','Funciones','Arreglos','Cadenas de caracteres','Archivos y persistencia'];
const sim = [91,88,90,83,86,77,42]; const src=[[1],[2],[3],[4],[5],[5],[6]];
const cob = dest.map((n,i)=>({ numero:i+1, nombre:n, horas:12, similitudMaxima: sim[i], cubierta: sim[i]>=75, unidadesOrigenContribuyentes: src[i]}));
const analisis = { extraccion: ex[0], creditos: { pcred: 100, categoria: 'NORMAL', alertas: [], bloqueoDuro: false, detalleCalculo: 'Pcred = min(100, 4 / 3 * 100) = 100.00%; Ruta=CAU-P-011; Años=2; MatrículaDestino=1; Externo=False' }, contenido: { pcont: 85.71, unidadesCubiertas: cob.filter(c=>c.cubierta), unidadesFaltantes: cob.filter(c=>!c.cubierta), tauUsado: 75, detalleCalculo: '', violacionRN19: false, unidadesOrigenDuplicadas: [] }, precedentes: [], estadoFinal: 0, alertasGlobales: ['Unidad destino 7 (Archivos y persistencia) NO cubierta: similitud 42.00% < τ=75%'], detalleTrazabilidad: '' };
const precedentes = [{ id: 'p1', resolucion: 'RES-FISEI-2025-031', asignaturasOrigen: [{codigo:'TI101',nombre:'Fundamentos de Programación'}], asignaturaDestino: 'SW101 Fundamentos de Programación', institucionOrigen: 'UTA · Tecnologías de la Información', fecha: '2025-03-12' }];
const asig = [{ id: 1, mallaId: 1, codigo: 'SW101', nombre: 'Fundamentos de Programación', creditos: 3, horas: 84, semestre: 1, planCompleto: true, unidades: dest.map((n,i)=>u(i+1,n,12)) }];
const parametros = { id: 1, rutaNormativa: 'CAU-P-011', theta: 80, delta: 15, tau: 75, version: 3, actualizadoUtc: now, actualizadoPor: 'admin' };

async function shoot(rol, ruta, nombre, accion) {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1360, height: 900 } });
  const usuario = { id: 1, correoInstitucional: rol==='OPERADOR'?'operador@uta.edu.ec':'docente@uta.edu.ec', nombreCompleto: rol==='OPERADOR'?'Operador académico':'Docente evaluador', rol, rolId: 1, activo: true, fechaCreacionUtc: now, ultimoAccesoUtc: now };
  await page.addInitScript((s) => localStorage.setItem('siha.sesion', JSON.stringify(s)), { token: 't', expiraUtc: '2099-01-01T00:00:00Z', usuario });
  await page.route('http://localhost:5000/**', async (route) => {
    const url = new URL(route.request().url()); const p = url.pathname; const m = route.request().method();
    const json = (b, s=200) => route.fulfill({ status: s, contentType: 'application/json', body: JSON.stringify(b) });
    if (p === '/api/auth/yo') return json(usuario);
    if (p === '/api/extracciones/bandeja') return json(ex);
    if (p === '/api/expedientes') return rol==='DOCENTE' ? json({mensaje:'Prohibido'},403) : json(exps);
    if (p.startsWith('/api/expedientes/')) { const e = exps.find(x=>p.endsWith(x.id)); return e ? json(e) : json({},404); }
    if (p === '/api/extracciones/precedentes/buscar') return json(precedentes);
    if (p.endsWith('/analizar')) return json(analisis);
    if (p.startsWith('/api/extracciones/')) { const e = ex.find(x=>p.endsWith('/'+x.id)); return e ? json(e) : json({mensaje:'No existe'},404); }
    if (p.startsWith('/api/asignaturas')) return json(asig);
    if (p.startsWith('/api/reglas/parametros')) return json(parametros);
    return json([]);
  });
  await page.goto(BASE + ruta); await page.waitForTimeout(900);
  if (accion) await accion(page);
  await page.screenshot({ path: `${OUT}/${nombre}.png`, fullPage: true });
  await browser.close();
}
(async () => {
  await shoot('OPERADOR', '/procesamiento', 'M01-bandeja');
  await shoot('OPERADOR', '/procesamiento/e2/confirmar', 'M02-confirmar', async (p) => { await p.getByRole('button', { name: 'Confirmar y calcular' }).click(); await p.waitForTimeout(300); });
  await shoot('DOCENTE', '/procesamiento/e3/estado', 'M04-estado-ocr');
  await shoot('DOCENTE', '/evaluacion', 'bandeja-analisis');
  for (const [vista, nombre] of [['creditos','M03-creditos'],['cobertura','M05-cobertura'],['precedentes','M06-precedentes']]) {
    await shoot('DOCENTE', `/evaluacion/e1?vista=${vista}`, nombre, async (p) => { await p.fill('#codigo-destino','SW101'); await p.fill('#anos','2'); await p.getByRole('button', { name: 'Ejecutar análisis' }).click(); await p.waitForTimeout(800); });
  }
})();
