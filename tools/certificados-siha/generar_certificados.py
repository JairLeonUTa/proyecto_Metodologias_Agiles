"""Genera la plantilla y los certificados de ejemplo con el formato oficial SIHA-CH-01.

Uso:  python tools/certificados-siha/generar_certificados.py
Requiere: pip install reportlab

Salida:
  documentos-prueba/certificados/*.pdf   (pruebas automáticas y demostración)
  frontend/public/plantillas/*.pdf        (descargables desde la pantalla de registro)
"""
from __future__ import annotations

import os
import shutil
import sys
from dataclasses import dataclass, field

from reportlab.lib.colors import HexColor
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase.pdfmetrics import stringWidth
from reportlab.pdfgen import canvas

sys.path.insert(0, os.path.dirname(__file__))
from datos_catalogo import (  # noqa: E402
    ESTRUCTURAS_UC, FISICA_I_IND, FISICA_II_IND, PROGRAMACION_BASICA_IND, PROGRAMACION_UC,
)

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
class Asignatura:
    codigo: str
    nombre: str
    semestre: int
    creditos: int
    horas: int
    fecha: str
    nota: str
    matricula: int
    unidades: list


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
    asignaturas: list = field(default_factory=list)
    revisores: list = field(default_factory=lambda: [REVISORES["hnaranjo"], REVISORES["mvaca"]])


def materia(codigo, nombre, semestre, creditos, fecha, nota, matricula, unidades):
    return Asignatura(codigo, nombre, semestre, creditos, sum(u[1] for u in unidades), fecha, nota, matricula, unidades)


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
    e.c.drawCentredString(ANCHO / 2, e.y, "Incluye solo materias aprobadas en los últimos 10 años y sin tercera matrícula")
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

    e.seccion("ASIGNATURAS APROBADAS")
    for a in cert.asignaturas:
        e.espacio(70)
        e.y -= 4
        e.pares([("Asignatura", f"{a.codigo} - {a.nombre}")], bold_valor=True)
        e.pares([("Semestre", str(a.semestre)), ("Créditos", str(a.creditos)), ("Horas totales", str(a.horas))], x0=MARGEN + 12)
        e.pares([("Fecha de aprobación", a.fecha), ("Nota", a.nota)], x0=MARGEN + 12)
        for i, (nombre, horas, contenidos) in enumerate(a.unidades, start=1):
            e.linea_simple(f"Unidad {i}: {nombre} ({horas} horas)", "Helvetica-Bold", 9, MARGEN + 12)
            e.texto_largo("Contenidos", contenidos, MARGEN + 24)

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
    c.drawString(MARGEN, ALTO - 70, "PLAN ANALÍTICO - FÍSICA I")
    c.setFont("Helvetica", 10)
    y = ALTO - 100
    for linea in ["Código: IND105", "Créditos: 4", "Horas totales: 192",
                  "Unidad 1: Magnitudes físicas y vectores (48 horas)", "Unidad 2: Cinemática de la partícula (48 horas)"]:
        c.drawString(MARGEN, y, linea)
        y -= 16
    c.save()


def main():
    ind_sw = dict(
        tipo="INTERNO", ruta="CAU-P-011", institucion="Universidad Técnica de Ambato",
        carrera_origen="IND - Ingeniería Industrial", carrera_destino="SW - Ingeniería de Software",
    )
    certificados = [
        Certificado("SIHA-IND-SW-completo.pdf", "SIHA-2026-0147", "2026-10-30",
                    "Pérez Gómez Ana Lucía", "1804567890", **ind_sw, asignaturas=[
                        materia("IND105", "Física I", 1, 4, "2024-02-16", "8.60", 1, FISICA_I_IND),
                        materia("IND108", "Programación Básica", 2, 4, "2024-08-02", "9.10", 1, PROGRAMACION_BASICA_IND),
                        materia("IND205", "Física II", 2, 4, "2024-08-09", "8.20", 1, FISICA_II_IND),
                    ]),
        Certificado("SIHA-IND-SW-solo-fisica1.pdf", "SIHA-2026-0148", "2026-10-30",
                    "Villacís Mora Carlos Andrés", "1805123456", **ind_sw, asignaturas=[
                        materia("IND105", "Física I", 1, 4, "2025-02-14", "7.80", 1, FISICA_I_IND),
                        materia("IND108", "Programación Básica", 2, 4, "2025-08-01", "8.40", 2, PROGRAMACION_BASICA_IND),
                    ], revisores=[REVISORES["mvaca"], REVISORES["jcastro"]]),
        Certificado("SIHA-UCuenca-SW-externo.pdf", "SIHA-2026-0151", "2026-10-31",
                    "Ortiz Salazar María José", "0104567891", tipo="EXTERNO", ruta="CAU-P-122",
                    institucion="Universidad de Cuenca", carrera_origen="CC - Ciencias de la Computación",
                    carrera_destino="SW - Ingeniería de Software", asignaturas=[
                        materia("UC-ED", "Estructuras de Datos", 3, 4, "2023-07-21", "8.90", 1, ESTRUCTURAS_UC),
                        materia("UC-PROG", "Programación I", 1, 4, "2021-07-23", "7.50", 1, PROGRAMACION_UC),
                    ], revisores=[REVISORES["hnaranjo"], REVISORES["jcastro"]]),
    ]

    # Certificado con errores para demostrar el rechazo: nota reprobatoria y horas que no cuadran.
    erroneo = Certificado("SIHA-con-errores.pdf", "SIHA-2026-0199", "2026-10-30",
                          "Prueba Errores Juan", "18045", **ind_sw, asignaturas=[
                              materia("IND105", "Física I", 1, 4, "2024-02-16", "6.40", 1, FISICA_I_IND),
                              materia("IND108", "Programación Básica", 2, 4, "2014-08-01", "8.00", 1, PROGRAMACION_BASICA_IND),
                          ], revisores=[REVISORES["hnaranjo"], ("Docente Externo", "docente@gmail.com")])
    erroneo.asignaturas[0].horas = 160

    rutas = [generar(c) for c in certificados + [erroneo]]
    invalido = os.path.join(SALIDA, "plan-analitico-no-valido.pdf")
    plan_analitico_no_valido(invalido)
    rutas.append(invalido)

    os.makedirs(PUBLICO, exist_ok=True)
    for r in rutas:
        shutil.copy(r, PUBLICO)
    print("\n".join(rutas))


if __name__ == "__main__":
    main()
