"""Genera los certificados de ejemplo con el formato oficial SIHA-CH-01.

El certificado trae: estudiante, trámite, carreras con su nombre completo, historial académico
(materias aprobadas y reprobadas con nivel, período académico y nota), qué materia quiere homologar
por cuál y los dos docentes revisores. Créditos, horas y contenidos NO van aquí: el agente los toma
de los sílabos oficiales (src/Services/Siha.MsA.Api/Data/CatalogoUta).

Uso:  python tools/certificados-siha/generar_certificados.py      (requiere: pip install reportlab)
Salida: documentos-prueba/certificados/*.pdf y frontend/public/plantillas/*.pdf
"""
from __future__ import annotations

import os
import shutil
from dataclasses import dataclass, field

from reportlab.lib.colors import HexColor
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase.pdfmetrics import stringWidth
from reportlab.pdfgen import canvas

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SALIDA = os.path.join(RAIZ, "documentos-prueba", "certificados")
PUBLICO = os.path.join(RAIZ, "frontend", "public", "plantillas")

# Deben existir en MS A como usuarios DOCENTE (DbSeederA.DocentesDemo los crea con Seed:DocentePassword).
REVISORES = {
    "hnaranjo": ("Ing. Hernán Naranjo", "hnaranjo@uta.edu.ec"),
    "mvaca": ("Ing. María Vaca", "mvaca@uta.edu.ec"),
    "jcastro": ("Ing. Javier Castro", "jcastro@uta.edu.ec"),
}

AZUL = HexColor("#7a1f2b")  # granate institucional
GRIS = HexColor("#555555")
ANCHO, ALTO = A4
MARGEN = 50


@dataclass
class Materia:
    nombre: str
    nivel: int
    periodo: str
    nota: str
    estado: str  # Aprobada / Reprobada


@dataclass
class Certificado:
    archivo: str
    numero: str
    fecha_emision: str
    estudiante: str
    cedula: str
    tipo: str
    ruta: str
    institucion: str
    carrera_origen: str
    carrera_destino: str
    materias: list
    solicitudes: list  # [( [materias aprobadas], materia de destino )]
    revisores: list = field(default_factory=lambda: [REVISORES["hnaranjo"], REVISORES["mvaca"]])


class Escritor:
    """Escribe líneas 'Etiqueta: valor' con salto de página y pie 'Página n de N'."""

    def __init__(self, ruta: str, total_paginas: int | None):
        self.c = canvas.Canvas(ruta, pagesize=A4)
        self.c.setTitle("Certificado de homologación SIHA-CH-01")
        self.total = total_paginas
        self.pagina = 1
        self.y = ALTO - MARGEN

    def _pie(self):
        self.c.setFont("Helvetica", 8)
        self.c.setFillColor(GRIS)
        texto = f"Página {self.pagina} de {self.total or '?'}"
        self.c.drawRightString(ANCHO - MARGEN, 30, texto)
        self.c.drawString(MARGEN, 30, "SIHA-CH-01")
        self.c.setFillColor(HexColor("#000000"))

    def espacio(self, alto: float):
        if self.y - alto < 60:
            self._pie()
            self.c.showPage()
            self.pagina += 1
            self.y = ALTO - MARGEN

    def titulo(self, texto: str, tam=14):
        self.espacio(tam + 8)
        self.c.setFont("Helvetica-Bold", tam)
        self.c.setFillColor(AZUL)
        self.c.drawCentredString(ANCHO / 2, self.y, texto)
        self.c.setFillColor(HexColor("#000000"))
        self.y -= tam + 6

    def seccion(self, texto: str):
        self.espacio(26)
        self.y -= 6
        self.c.setFillColor(AZUL)
        self.c.rect(MARGEN, self.y - 4, ANCHO - 2 * MARGEN, 16, fill=1, stroke=0)
        self.c.setFillColor(HexColor("#ffffff"))
        self.c.setFont("Helvetica-Bold", 10)
        self.c.drawString(MARGEN + 6, self.y, texto)
        self.c.setFillColor(HexColor("#000000"))
        self.y -= 20

    def pares(self, pares: list[tuple[str, str]], x0=MARGEN, bold_valor=False):
        """Varias etiquetas en una misma línea."""
        self.espacio(15)
        x = x0
        for etiqueta, valor in pares:
            self.c.setFont("Helvetica-Bold", 10)
            self.c.drawString(x, self.y, f"{etiqueta}:")
            x += stringWidth(f"{etiqueta}:", "Helvetica-Bold", 10) + 4
            self.c.setFont("Helvetica-Bold" if bold_valor else "Helvetica", 10)
            self.c.drawString(x, self.y, valor)
            x += stringWidth(valor, "Helvetica", 10) + 26
        self.y -= 15

    def texto_largo(self, etiqueta: str, valor: str, x0: float):
        """'Contenidos: ...' con ajuste de línea; las continuaciones no llevan etiqueta."""
        ancho_max = ANCHO - MARGEN - x0
        palabras = valor.split()
        primera = True
        linea = ""
        lineas = []
        for p in palabras:
            prefijo = f"{etiqueta}: " if primera and not lineas else ""
            prueba = (linea + " " + p).strip()
            if stringWidth(prefijo + prueba, "Helvetica", 9) > ancho_max and linea:
                lineas.append(linea)
                linea = p
            else:
                linea = prueba
        if linea:
            lineas.append(linea)
        for i, l in enumerate(lineas):
            self.espacio(12)
            if i == 0:
                self.c.setFont("Helvetica-Bold", 9)
                self.c.drawString(x0, self.y, f"{etiqueta}:")
                xv = x0 + stringWidth(f"{etiqueta}:", "Helvetica-Bold", 9) + 4
            else:
                xv = x0 + 12
            self.c.setFont("Helvetica", 9)
            self.c.drawString(xv, self.y, l)
            self.y -= 12

    def linea_simple(self, texto: str, fuente="Helvetica", tam=9, x0=MARGEN):
        self.espacio(tam + 4)
        self.c.setFont(fuente, tam)
        self.c.drawString(x0, self.y, texto)
        self.y -= tam + 4

    def cerrar(self):
        self._pie()
        self.c.save()
        return self.pagina


def dibujar(cert: Certificado, ruta: str, total: int | None) -> int:
    e = Escritor(ruta, total)
    e.titulo("UNIVERSIDAD TÉCNICA DE AMBATO", 12)
    e.titulo("CERTIFICADO DE HOMOLOGACIÓN SIHA-IA", 15)
    e.c.setFont("Helvetica", 9)
    e.c.drawCentredString(ANCHO / 2, e.y, "Formato SIHA-CH-01 · Documento preaprobado por Secretaría Académica")
    e.y -= 11
    e.c.drawCentredString(ANCHO / 2, e.y, "Solo incluye materias de los últimos 10 años y sin tercera matrícula")
    e.y -= 16
    e.pares([("Número de certificado", cert.numero), ("Fecha de emisión", cert.fecha_emision)])
    e.pares([("Emitido por", "Secretaría Académica FISEI")])

    e.seccion("DATOS DEL ESTUDIANTE")
    e.pares([("Estudiante", cert.estudiante)])
    e.pares([("Cédula", cert.cedula)])

    e.seccion("TRÁMITE")
    e.pares([("Tipo de trámite", cert.tipo), ("Ruta normativa", cert.ruta)])
    e.pares([("Institución de origen", cert.institucion)])
    e.pares([("Carrera de origen", cert.carrera_origen)])
    e.pares([("Carrera de destino", cert.carrera_destino)])

    e.seccion("DOCENTES REVISORES")
    for i, (nombre, correo) in enumerate(cert.revisores, start=1):
        e.pares([(f"Docente revisor {i}", f"{nombre} - {correo}")])

    e.seccion("HISTORIAL ACADÉMICO EN LA CARRERA DE ORIGEN")
    for m in sorted(cert.materias, key=lambda m: (m.nivel, m.nombre)):
        e.espacio(32)
        e.pares([("Materia", m.nombre)], bold_valor=True)
        e.pares([("Nivel", str(m.nivel)), ("Período académico", m.periodo), ("Nota", m.nota), ("Estado", m.estado)], x0=MARGEN + 12)

    e.seccion("MATERIAS QUE SOLICITA HOMOLOGAR")
    for i, (origen, destino) in enumerate(cert.solicitudes, start=1):
        e.espacio(14 * (len(origen) + 2))
        e.linea_simple(f"Solicitud {i}", "Helvetica-Bold", 9)
        for o in origen:
            e.pares([("Materia aprobada", o)], x0=MARGEN + 12)
        e.pares([("Homologar por", destino)], x0=MARGEN + 12, bold_valor=True)

    e.y -= 10
    e.linea_simple("FIN DEL CERTIFICADO", "Helvetica-Bold", 10)
    e.y -= 20
    e.linea_simple("_______________________________", x0=ANCHO / 2 - 80)
    e.linea_simple("Secretaría Académica FISEI", x0=ANCHO / 2 - 60)
    return e.cerrar()


def generar(cert: Certificado):
    os.makedirs(SALIDA, exist_ok=True)
    ruta = os.path.join(SALIDA, cert.archivo)
    paginas = dibujar(cert, ruta, None)
    dibujar(cert, ruta, paginas)
    return ruta


def plan_analitico_no_valido(ruta: str):
    """PDF que NO sigue la plantilla (plan analítico suelto): debe rechazarse."""
    c = canvas.Canvas(ruta, pagesize=A4)
    c.setFont("Helvetica-Bold", 14)
    c.drawString(MARGEN, ALTO - 70, "PLAN ANALÍTICO - FÍSICA")
    c.setFont("Helvetica", 10)
    y = ALTO - 100
    for linea in ["Créditos: 3", "Horas totales: 144", "Unidad 1: Estática (36 horas)", "Unidad 2: Dinámica (36 horas)"]:
        c.drawString(MARGEN, y, linea)
        y -= 16
    c.save()


P1, P2 = "Abr-Sep 2025", "Oct 2025-Mar 2026"


def main():
    ti_sw = Certificado(
        "SIHA-TI-SW-cambio-de-carrera.pdf", "SIHA-2026-0147", "2026-10-30",
        "Pérez Gómez Ana Lucía", "1804567890", "Cambio de carrera", "CAU-P-011",
        "Universidad Técnica de Ambato", "Tecnologías de la Información", "Software",
        materias=[
            Materia("Álgebra Lineal", 1, P1, "8.40", "Aprobada"),
            Materia("Fundamentos de Programación", 1, P1, "8.90", "Aprobada"),
            Materia("Cálculo Diferencial", 1, P1, "7.60", "Aprobada"),
            Materia("Física", 1, P1, "8.10", "Aprobada"),
            Materia("Metodología de la Investigación", 1, P1, "9.00", "Aprobada"),
            Materia("Cálculo Integral", 2, P2, "6.20", "Reprobada"),
            Materia("Lógica Matemática", 2, P2, "7.80", "Aprobada"),
            Materia("Medidas Eléctricas", 2, P2, "5.90", "Reprobada"),
            Materia("Estructura de Datos", 2, P2, "8.00", "Aprobada"),
            Materia("Realidad Nacional", 2, P2, "8.50", "Aprobada"),
            Materia("Programación Orientada a Objetos", 2, P2, "7.90", "Aprobada"),
        ],
        solicitudes=[
            (["Fundamentos de Programación"], "Algoritmos y Lógica de Programación"),
            (["Física"], "Física"),
            (["Álgebra Lineal"], "Álgebra Lineal"),
            (["Programación Orientada a Objetos"], "Programación Orientada a Objetos"),
            (["Estructura de Datos"], "Estructura de Datos"),
        ])

    ind_sw = Certificado(
        "SIHA-IND-SW-cambio-de-carrera.pdf", "SIHA-2026-0148", "2026-10-30",
        "Villacís Mora Carlos Andrés", "1805123456", "Cambio de carrera", "CAU-P-011",
        "Universidad Técnica de Ambato", "Ingeniería Industrial", "Software",
        materias=[
            Materia("Química", 1, P1, "7.40", "Aprobada"),
            Materia("Física Básica", 1, P1, "8.20", "Aprobada"),
            Materia("Álgebra", 1, P1, "7.90", "Aprobada"),
            Materia("Introducción a la Ingeniería Industrial", 1, P1, "8.80", "Aprobada"),
            Materia("Lógica Matemática", 1, P1, "8.00", "Aprobada"),
            Materia("Tecnologías de la Información y la Comunicación (TIC)", 1, P1, "9.10", "Aprobada"),
            Materia("Física Aplicada", 2, P2, "6.50", "Reprobada"),
            Materia("Cálculo Diferencial", 2, P2, "7.20", "Aprobada"),
            Materia("Programación", 2, P2, "8.60", "Aprobada"),
        ],
        solicitudes=[
            (["Física Básica"], "Física"),
            (["Lógica Matemática"], "Lógica Matemática"),
            (["Programación"], "Algoritmos y Lógica de Programación"),
            (["Cálculo Diferencial"], "Cálculo Diferencial"),
        ],
        revisores=[REVISORES["mvaca"], REVISORES["jcastro"]])

    # Rechazado: siglas en la carrera, primer nivel no aprobado, pide una reprobada y revisor con Gmail.
    erroneo = Certificado(
        "SIHA-con-errores.pdf", "SIHA-2026-0199", "2026-10-30",
        "Prueba Errores Juan", "18045", "Cambio de carrera", "CAU-P-011",
        "Universidad Técnica de Ambato", "TI", "Software",
        materias=[
            Materia("Fundamentos de Programación", 1, P1, "8.90", "Aprobada"),
            Materia("Física", 1, P1, "5.40", "Reprobada"),
            Materia("Álgebra Lineal", 1, "Abr-Sep 2013", "8.00", "Aprobada"),
        ],
        solicitudes=[(["Física"], "Física"), (["Fundamentos de Programación"], "Algoritmos y Lógica de Programación")],
        revisores=[REVISORES["hnaranjo"], ("Docente Externo", "docente@gmail.com")])

    rutas = [generar(c) for c in (ti_sw, ind_sw, erroneo)]
    invalido = os.path.join(SALIDA, "plan-analitico-no-valido.pdf")
    plan_analitico_no_valido(invalido)
    rutas.append(invalido)

    for viejo in os.listdir(SALIDA):
        if viejo.endswith(".pdf") and os.path.join(SALIDA, viejo) not in rutas:
            os.remove(os.path.join(SALIDA, viejo))
    shutil.rmtree(PUBLICO, ignore_errors=True)
    os.makedirs(PUBLICO, exist_ok=True)
    for r in rutas:
        shutil.copy(r, PUBLICO)
    print("\n".join(rutas))


if __name__ == "__main__":
    main()
