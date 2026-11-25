import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';
import type { EvaluacionMateria, ResultadoFinalMateria, SolicitudAnalisis } from '../types/analisis';

const RESULTADO: Record<string, string> = {
  HOMOLOGADA: 'HOMOLOGADA',
  NO_HOMOLOGADA: 'NO HOMOLOGADA',
  SIN_ACUERDO: 'SIN ACUERDO',
  PENDIENTE: 'PENDIENTE',
};

const AZUL: [number, number, number] = [30, 58, 110];

/** Fecha larga en español: "25 de noviembre de 2026". */
function fechaLarga(fecha: Date): string {
  return fecha.toLocaleDateString('es-EC', { day: 'numeric', month: 'long', year: 'numeric' });
}

function evaluacionDe(s: SolicitudAnalisis, m: ResultadoFinalMateria): EvaluacionMateria | undefined {
  return s.resultado?.evaluaciones.find(
    (e) => (e.codigoDestino && e.codigoDestino === m.codigoDestino) || e.materiaDestino === m.nombreDestino,
  );
}

/** "Homologa" / "No homologa" / "—" del docente para una materia. */
function votoDe(s: SolicitudAnalisis, correo: string, codigo: string, bloqueada: boolean): string {
  if (bloqueada) return 'No aplica';
  const d = s.dictamenes.find((x) => x.correoDocente === correo)?.decisiones.find((x) => x.codigoDestino === codigo);
  if (!d) return '—';
  return d.aprobada ? 'Homologa' : 'No homologa';
}

/** Nombre del archivo de la resolución de una solicitud. */
export function nombreArchivoResolucion(s: SolicitudAnalisis): string {
  return `Resolucion-homologacion-${s.codigoExpediente || s.numeroCertificado}.pdf`;
}

/**
 * Arma la resolución de homologación (PDF formal) de una solicitud resuelta: datos del trámite, la decisión
 * de cada materia con los votos de los dos docentes, las decisiones del administrador y las firmas.
 */
export function crearResolucionPdf(s: SolicitudAnalisis, ahora: Date = new Date()): jsPDF {
  const doc = new jsPDF({ unit: 'mm', format: 'a4' });
  const ancho = doc.internal.pageSize.getWidth();
  const margen = 18;
  const util = ancho - margen * 2;
  let y = 18;

  // Encabezado institucional.
  doc.setFont('helvetica', 'bold').setFontSize(13).setTextColor(...AZUL);
  doc.text('UNIVERSIDAD TÉCNICA DE AMBATO', ancho / 2, y, { align: 'center' });
  y += 6;
  doc.setFont('helvetica', 'normal').setFontSize(10).setTextColor(60);
  doc.text('Facultad de Ingeniería en Sistemas, Electrónica e Industrial', ancho / 2, y, { align: 'center' });
  y += 5;
  doc.text('Sistema Inteligente de Homologación Académica (SIHA-IA)', ancho / 2, y, { align: 'center' });
  y += 4;
  doc.setDrawColor(...AZUL).setLineWidth(0.6).line(margen, y, ancho - margen, y);
  y += 9;
  doc.setFont('helvetica', 'bold').setFontSize(14).setTextColor(20);
  doc.text('RESOLUCIÓN DE HOMOLOGACIÓN DE ASIGNATURAS', ancho / 2, y, { align: 'center' });
  y += 6;
  doc.setFont('helvetica', 'normal').setFontSize(10);
  doc.text(`N.º RES-${s.codigoExpediente}`, ancho / 2, y, { align: 'center' });
  y += 5;
  doc.text(`Ambato, ${fechaLarga(ahora)}`, ancho / 2, y, { align: 'center' });
  y += 8;

  // Datos del trámite.
  autoTable(doc, {
    startY: y,
    margin: { left: margen, right: margen },
    theme: 'grid',
    styles: { fontSize: 9, cellPadding: 1.8 },
    columnStyles: { 0: { fontStyle: 'bold', cellWidth: 48, fillColor: [238, 242, 248] } },
    body: [
      ['Estudiante', s.estudianteNombre],
      ['Expediente', s.codigoExpediente],
      ['Certificado', s.numeroCertificado],
      ['Tipo de trámite', `${s.tipoTramite} (${s.rutaNormativa})`],
      ['Institución de origen', s.institucionOrigen],
      ['Carrera de origen', s.carreraOrigen],
      ['Carrera de destino', s.carreraDestino],
      ['Docentes revisores', s.revisores.map((r) => `${r.nombre} (${r.correo})`).join('\n')],
    ],
  });
  y = (doc as jsPDF & { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 7;

  // Considerandos.
  const parrafo = (texto: string, negrita = false): void => {
    doc.setFont('helvetica', negrita ? 'bold' : 'normal').setFontSize(10).setTextColor(20);
    const lineas = doc.splitTextToSize(texto, util) as string[];
    if (y + lineas.length * 5 > 275) {
      doc.addPage();
      y = 20;
    }
    doc.text(lineas, margen, y, { align: 'justify', maxWidth: util });
    y += lineas.length * 5 + 2;
  };
  parrafo('CONSIDERANDO:', true);
  parrafo(
    `Que el/la estudiante ${s.estudianteNombre} solicitó la homologación de asignaturas aprobadas en la carrera de ` +
      `${s.carreraOrigen} (${s.institucionOrigen}) para la carrera de ${s.carreraDestino}, mediante el trámite de ` +
      `${s.tipoTramite.toLowerCase()} con la ruta normativa ${s.rutaNormativa}.`,
  );
  parrafo(
    'Que el agente de análisis de SIHA-IA comparó cada asignatura con el sílabo de la carrera de destino, exigiendo al ' +
      'menos el 80 % de los créditos (requisito obligatorio) y recomendando según la cobertura de contenidos (80 %).',
  );
  parrafo(
    `Que los docentes revisores ${s.revisores.map((r) => r.nombre).join(' y ')} revisaron el análisis y emitieron su ` +
      'decisión por cada asignatura; y que, cuando no coincidieron, la autoridad de la carrera emitió la decisión final.',
  );
  parrafo('SE RESUELVE:', true);

  // Decisión por materia.
  const [r1, r2] = s.revisores;
  const filas = s.resultadoFinal.map((m, i) => {
    const e = evaluacionDe(s, m);
    const bloqueada = Boolean(m.bloqueadaPorCreditos ?? e?.bloqueadaPorCreditos);
    const origen = e?.materiasOrigen.map((o) => `${o.nombre} (${o.nota.toFixed(2)})`).join('\n') ?? '—';
    const creditos = e?.porcentajeCreditos != null ? `${Math.round(e.porcentajeCreditos)} %` : '—';
    const contenidos = e && !e.sinSilabo ? `${Math.round(e.contenidos)} %` : '—';
    let resultado = RESULTADO[m.resultado] ?? m.resultado;
    if (bloqueada) resultado += '\n(créditos < 80 %)';
    if (m.decisionFinal) resultado += '\n(decisión del administrador)';
    return [
      String(i + 1),
      m.nombreDestino,
      origen,
      creditos,
      contenidos,
      r1 ? votoDe(s, r1.correo, m.codigoDestino, bloqueada) : '—',
      r2 ? votoDe(s, r2.correo, m.codigoDestino, bloqueada) : '—',
      resultado,
    ];
  });
  autoTable(doc, {
    startY: y,
    margin: { left: margen, right: margen },
    theme: 'grid',
    head: [['#', 'Asignatura de destino', 'Aprobada en origen (nota)', 'Créd.', 'Cont.', r1?.nombre ?? 'Docente 1', r2?.nombre ?? 'Docente 2', 'Resultado']],
    body: filas,
    styles: { fontSize: 8, cellPadding: 1.6, valign: 'middle' },
    headStyles: { fillColor: AZUL, textColor: 255, fontSize: 8 },
    columnStyles: { 0: { cellWidth: 7, halign: 'center' }, 3: { cellWidth: 12, halign: 'center' }, 4: { cellWidth: 12, halign: 'center' } },
    didParseCell: (d) => {
      if (d.section === 'body' && d.column.index === 7) {
        d.cell.styles.fontStyle = 'bold';
        d.cell.styles.textColor = String(d.cell.raw).startsWith('HOMOLOGADA') ? [4, 110, 40] : [150, 25, 30];
      }
    },
  });
  y = (doc as jsPDF & { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 7;

  const homologadas = s.resultadoFinal.filter((m) => m.resultado === 'HOMOLOGADA');
  const noHomologadas = s.resultadoFinal.filter((m) => m.resultado === 'NO_HOMOLOGADA');
  parrafo(
    `Artículo 1.- Homologar ${homologadas.length} asignatura(s) en la carrera de ${s.carreraDestino}` +
      (homologadas.length ? `: ${homologadas.map((m) => m.nombreDestino).join(', ')}.` : '.'),
  );
  parrafo(
    `Artículo 2.- No homologar ${noHomologadas.length} asignatura(s)` +
      (noHomologadas.length ? `: ${noHomologadas.map((m) => m.nombreDestino).join(', ')}; el/la estudiante deberá cursarlas.` : '.'),
  );

  const decisiones = s.resultadoFinal.filter((m) => m.decisionFinal);
  if (decisiones.length) {
    parrafo('Artículo 3.- Decisiones de la autoridad de carrera (los docentes no coincidieron):');
    for (const m of decisiones) {
      const d = m.decisionFinal!;
      parrafo(`• ${m.nombreDestino}: ${d.aprobada ? 'homologar' : 'no homologar'}. ${d.nombreAdministrador}: “${d.comentario}”.`);
    }
  }
  parrafo(`Artículo ${decisiones.length ? 4 : 3}.- Notificar la presente resolución al estudiante y a Secretaría Académica para su registro.`);

  // Firmas.
  if (y > 255) {
    doc.addPage();
    y = 30;
  } else {
    y += 18;
  }
  const firmantes = [
    ...s.revisores.map((r) => [r.nombre, 'Docente revisor']),
    [decisiones[0]?.decisionFinal?.nombreAdministrador ?? 'Coordinador(a) de carrera', 'Autoridad de carrera'],
  ];
  const anchoFirma = util / firmantes.length;
  firmantes.forEach(([nombre, cargo], i) => {
    const x = margen + anchoFirma * i + anchoFirma / 2;
    doc.setDrawColor(80).setLineWidth(0.3).line(x - anchoFirma / 2 + 6, y, x + anchoFirma / 2 - 6, y);
    doc.setFont('helvetica', 'bold').setFontSize(9).setTextColor(20).text(nombre, x, y + 5, { align: 'center' });
    doc.setFont('helvetica', 'normal').setFontSize(8).setTextColor(90).text(cargo, x, y + 9, { align: 'center' });
  });

  // Pie en cada página.
  const paginas = doc.getNumberOfPages();
  for (let p = 1; p <= paginas; p += 1) {
    doc.setPage(p);
    doc.setFont('helvetica', 'normal').setFontSize(7.5).setTextColor(120);
    doc.text(
      `Generado por SIHA-IA el ${ahora.toLocaleString('es-EC')} · Expediente ${s.codigoExpediente}`,
      margen,
      289,
    );
    doc.text(`Página ${p} de ${paginas}`, ancho - margen, 289, { align: 'right' });
  }
  return doc;
}

/** Descarga la resolución de homologación en PDF. */
export function descargarResolucionPdf(s: SolicitudAnalisis): void {
  crearResolucionPdf(s).save(nombreArchivoResolucion(s));
}
