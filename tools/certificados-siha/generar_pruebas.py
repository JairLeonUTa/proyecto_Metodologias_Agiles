"""Genera un lote de certificados SIHA-CH-01 de PRUEBA (válidos e inválidos) para probar Homologa UTA.

Reutiliza la plantilla oficial de generar_certificados.py. No toca los ejemplos existentes.
Uso:    python tools/certificados-siha/generar_pruebas.py
Salida: documentos-prueba/certificados-pruebas/*.pdf  +  LEEME.md con el resultado esperado de cada uno.
"""
from __future__ import annotations

import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generar_certificados as g  # noqa: E402
from generar_certificados import Certificado, Materia, REVISORES  # noqa: E402

SALIDA = os.path.join(g.RAIZ, "documentos-prueba", "certificados-pruebas")
P1, P2, P3 = "Abr-Sep 2024", "Oct 2024-Mar 2025", "Abr-Sep 2025"
HN, MV, JC = REVISORES["hnaranjo"], REVISORES["mvaca"], REVISORES["jcastro"]
UTA = "Universidad Técnica de Ambato"
CC, MOV = ("Cambio de carrera", "CAU-P-011"), ("Movilidad externa", "CAU-P-122")


def cert(archivo, numero, estudiante, cedula, tramite, origen, destino, materias, solicitudes,
         revisores=(HN, MV), institucion=UTA, fecha="2026-11-03"):
    return Certificado(archivo, numero, fecha, estudiante, cedula, tramite[0], tramite[1], institucion,
                       origen, destino, materias=list(materias), solicitudes=list(solicitudes),
                       revisores=list(revisores))


def M(nombre, nivel, nota, estado="Aprobada", periodo=None):
    return Materia(nombre, nivel, periodo or {1: P1, 2: P2, 3: P3}[nivel], nota, estado)


# Historial base válido TI (3 niveles) reutilizado por los casos de error de un solo fallo.
TI_BASE = [
    M("Álgebra Lineal", 1, "8.40"), M("Fundamentos de Programación", 1, "8.90"),
    M("Cálculo Diferencial", 1, "7.60"), M("Física", 1, "8.10"),
    M("Metodología de la Investigación", 1, "9.00"),
    M("Cálculo Integral", 2, "7.30"), M("Lógica Matemática", 2, "7.80"),
    M("Estructura de Datos", 2, "8.00"), M("Programación Orientada a Objetos", 2, "8.20"),
]
TI_SOL = [(["Fundamentos de Programación"], "Algoritmos y Lógica de Programación"),
          (["Álgebra Lineal"], "Álgebra Lineal"), (["Física"], "Física")]


def base_error(archivo, numero, estudiante, **cambios):
    datos = dict(archivo=archivo, numero=numero, estudiante=estudiante, cedula="1804111222",
                 tramite=CC, origen="Tecnologías de la Información", destino="Software",
                 materias=TI_BASE, solicitudes=TI_SOL)
    datos.update(cambios)
    return cert(**datos)


CASOS = [
    # ---------------- VÁLIDOS ----------------
    ("Debe registrarse", "TEL → SW, 4 materias, revisores Naranjo y Castro", cert(
        "V01-TEL-SW-cambio-de-carrera.pdf", "SIHA-2026-0201", "Andrade Ruiz Mateo Sebastián", "1804222333",
        CC, "Telecomunicaciones", "Software",
        [M("Cálculo de una Variable", 1, "8.10"), M("Metodología de la Investigación", 1, "8.70"),
         M("Física Básica", 1, "7.90"), M("Álgebra Lineal", 1, "8.30"),
         M("Tecnologías de la Información (TIC)", 1, "9.20"), M("Química", 1, "7.40"),
         M("Cálculo de Varias Variables", 2, "6.10", "Reprobada"), M("Fundamentos de Programación", 2, "8.80"),
         M("Base de Datos", 2, "8.50"), M("Física Aplicada", 2, "7.20")],
        [(["Fundamentos de Programación"], "Algoritmos y Lógica de Programación"),
         (["Álgebra Lineal"], "Álgebra Lineal"), (["Física Básica"], "Física"),
         (["Cálculo de una Variable"], "Cálculo Diferencial")], revisores=(HN, JC))),
    ("Debe registrarse", "Automatización y Robótica → SW, 5 materias (incluye una reprobada en nivel 2, permitido)", cert(
        "V02-ROB-SW-cambio-de-carrera.pdf", "SIHA-2026-0202", "Benítez Lara Camila Fernanda", "1804333444",
        CC, "Automatización y Robótica", "Software",
        [M("Metodología de la Investigación", 1, "8.00"), M("Cálculo I", 1, "7.50"),
         M("Fundamentos de Programación", 1, "9.10"), M("Mecánica Básica", 1, "7.70"),
         M("Introducción a la Automatización y Robótica", 1, "8.60"), M("Álgebra Lineal", 1, "8.20"),
         M("Tecnologías del Aprendizaje y el Conocimiento (TAC)", 1, "9.40"),
         M("Cálculo II", 2, "5.80", "Reprobada"), M("Programación Avanzada", 2, "8.40"),
         M("Física Aplicada", 2, "7.10")],
        [(["Fundamentos de Programación"], "Algoritmos y Lógica de Programación"),
         (["Programación Avanzada"], "Programación Orientada a Objetos"),
         (["Álgebra Lineal"], "Álgebra Lineal"), (["Cálculo I"], "Cálculo Diferencial"),
         (["Metodología de la Investigación"], "Metodología de la Investigación")], revisores=(MV, JC))),
    ("Debe registrarse", "SW → TI (dirección inversa), 3 niveles", cert(
        "V03-SW-TI-cambio-de-carrera.pdf", "SIHA-2026-0203", "Castillo Núñez Diego Alejandro", "1804444555",
        CC, "Software", "Tecnologías de la Información",
        [M("Física", 1, "8.00"), M("Álgebra Lineal", 1, "7.90"), M("Cálculo Diferencial", 1, "7.40"),
         M("Algoritmos y Lógica de Programación", 1, "9.30"), M("Lógica Matemática", 1, "8.80"),
         M("Fundamentos de Ingeniería de Software", 2, "8.60"), M("Cálculo Integral", 2, "7.10"),
         M("Programación Orientada a Objetos", 2, "8.90"), M("Sistemas Operativos", 2, "8.20"),
         M("Estructura de Datos", 3, "8.70"), M("Probabilidad y Estadística", 3, "6.40", "Reprobada")],
        [(["Algoritmos y Lógica de Programación"], "Fundamentos de Programación"),
         (["Estructura de Datos"], "Estructura de Datos"), (["Sistemas Operativos"], "Sistemas Operativos"),
         (["Programación Orientada a Objetos"], "Programación Orientada a Objetos")])),
    ("Debe registrarse", "Industrial → TI con una sola materia (caso mínimo)", cert(
        "V04-IND-TI-una-materia.pdf", "SIHA-2026-0204", "Díaz Torres Valeria Sofía", "1804555666",
        CC, "Ingeniería Industrial", "Tecnologías de la Información",
        [M("Química", 1, "7.20"), M("Física Básica", 1, "7.80"), M("Álgebra", 1, "8.10"),
         M("Introducción a la Ingeniería Industrial", 1, "8.90"), M("Lógica Matemática", 1, "8.00"),
         M("Tecnologías de la Información y la Comunicación (TIC)", 1, "9.00")],
        [(["Lógica Matemática"], "Lógica Matemática")])),
    ("Debe registrarse", "Industrial → SW: dos materias aprobadas para una sola de destino", cert(
        "V05-IND-SW-dos-materias-a-una.pdf", "SIHA-2026-0205", "Espinoza Vega Nicolás Andrés", "1804666777",
        CC, "Ingeniería Industrial", "Software",
        [M("Química", 1, "7.60"), M("Física Básica", 1, "8.30"), M("Álgebra", 1, "7.70"),
         M("Introducción a la Ingeniería Industrial", 1, "8.40"), M("Lógica Matemática", 1, "8.10"),
         M("Tecnologías de la Información y la Comunicación (TIC)", 1, "9.20"),
         M("Cálculo Diferencial", 2, "7.90"), M("Álgebra Lineal", 2, "8.00"), M("Programación", 2, "8.70"),
         M("Cálculo Integral", 3, "7.50")],
        [(["Cálculo Diferencial", "Cálculo Integral"], "Cálculo Integral"),
         (["Programación"], "Algoritmos y Lógica de Programación"), (["Álgebra Lineal"], "Álgebra Lineal")],
        revisores=(JC, HN))),
    ("A verificar", "Movilidad externa (CAU-P-122) desde otra institución, TI → SW", cert(
        "V06-movilidad-externa-TI-SW.pdf", "SIHA-2026-0206", "Fiallos Mejía Sara Daniela", "1804777888",
        MOV, "Tecnologías de la Información", "Software", TI_BASE,
        TI_SOL + [(["Estructura de Datos"], "Estructura de Datos")],
        institucion="Escuela Superior Politécnica de Chimborazo")),
    ("Debe registrarse", "TI → SW con historial largo (3 niveles, varias páginas)", cert(
        "V07-TI-SW-historial-largo.pdf", "SIHA-2026-0207", "Gavilanes Ortiz Martín Esteban", "1804888999",
        CC, "Tecnologías de la Información", "Software",
        TI_BASE + [M("Realidad Nacional", 2, "8.60"), M("Medidas Eléctricas", 2, "6.80", "Reprobada"),
                   M("Programación Avanzada", 3, "8.30"), M("Probabilidad y Estadística", 3, "7.70"),
                   M("Sistemas Operativos", 3, "8.10"), M("Fundamentos de Bases de Datos", 3, "8.90"),
                   M("Fundamentos de Redes y Comunicación de Datos", 3, "7.60")],
        TI_SOL + [(["Estructura de Datos"], "Estructura de Datos"),
                  (["Programación Orientada a Objetos"], "Programación Orientada a Objetos"),
                  (["Lógica Matemática"], "Lógica Matemática"), (["Cálculo Diferencial"], "Cálculo Diferencial"),
                  (["Cálculo Integral"], "Cálculo Integral"), (["Sistemas Operativos"], "Sistemas Operativos"),
                  (["Probabilidad y Estadística"], "Probabilidad y Estadística")], revisores=(MV, HN))),

    # ---------------- INVÁLIDOS (un solo error cada uno) ----------------
    ("Debe rechazarse", "Cédula con 9 dígitos", base_error("E01-cedula-9-digitos.pdf", "SIHA-2026-0301", "Error Cédula Uno", cedula="180411122")),
    ("Debe rechazarse", "Revisor 2 con correo Gmail", base_error("E02-revisor-gmail.pdf", "SIHA-2026-0302", "Error Revisor Gmail", revisores=(HN, ("Ing. Pedro Ruiz", "pruiz@gmail.com")))),
    ("Debe rechazarse", "Los dos revisores son la misma persona", base_error("E03-revisores-repetidos.pdf", "SIHA-2026-0303", "Error Revisor Repetido", revisores=(HN, HN))),
    ("Debe rechazarse", "Revisor con correo UTA que no es docente del sistema", base_error("E04-revisor-no-existe.pdf", "SIHA-2026-0304", "Error Revisor Inexistente", revisores=(HN, ("Ing. Laura García", "lgarcia@uta.edu.ec")))),
    ("Debe rechazarse", "Pide homologar una materia reprobada", base_error("E05-pide-materia-reprobada.pdf", "SIHA-2026-0305", "Error Materia Reprobada",
        materias=TI_BASE + [M("Medidas Eléctricas", 2, "5.90", "Reprobada")], solicitudes=TI_SOL + [(["Medidas Eléctricas"], "Cálculo Integral")])),
    ("Debe rechazarse", "Materia aprobada hace más de 10 años (2014)", base_error("E06-materia-mas-de-10-anios.pdf", "SIHA-2026-0306", "Error Vigencia",
        materias=TI_BASE + [M("Realidad Nacional", 2, "8.00", periodo="Abr-Sep 2014")])),
    ("Debe rechazarse", "No aprobó todo el primer nivel (Física reprobada)", base_error("E07-primer-nivel-no-aprobado.pdf", "SIHA-2026-0307", "Error Primer Nivel",
        materias=[m if m.nombre != "Física" else M("Física", 1, "5.40", "Reprobada") for m in TI_BASE], solicitudes=TI_SOL[:2])),
    ("Debe rechazarse", "Nota 6.50 marcada como Aprobada (incoherente)", base_error("E08-nota-incoherente.pdf", "SIHA-2026-0308", "Error Nota",
        materias=[m if m.nombre != "Lógica Matemática" else M("Lógica Matemática", 2, "6.50") for m in TI_BASE])),
    ("Debe rechazarse", "Carrera de origen escrita con siglas (TI)", base_error("E09-carrera-con-siglas.pdf", "SIHA-2026-0309", "Error Siglas", origen="TI")),
    ("Debe rechazarse", "La misma materia aprobada usada en dos solicitudes", base_error("E10-materia-usada-dos-veces.pdf", "SIHA-2026-0310", "Error Materia Duplicada",
        solicitudes=TI_SOL + [(["Física"], "Cálculo Diferencial")])),
    ("Debe rechazarse", "La misma materia de destino pedida dos veces", base_error("E11-destino-repetido.pdf", "SIHA-2026-0311", "Error Destino Repetido",
        solicitudes=TI_SOL + [(["Cálculo Diferencial"], "Física")])),
    ("Debe rechazarse", "Cambio de carrera con ruta CAU-P-122 (debe ser CAU-P-011)", base_error("E12-ruta-normativa-incorrecta.pdf", "SIHA-2026-0312", "Error Ruta", tramite=("Cambio de carrera", "CAU-P-122"))),
    ("Debe rechazarse", "Carrera de origen igual a la de destino", base_error("E13-origen-igual-destino.pdf", "SIHA-2026-0313", "Error Misma Carrera", origen="Software")),
    ("Debe rechazarse", "Fecha de emisión en el futuro (2027-01-15)", base_error("E14-fecha-futura.pdf", "SIHA-2026-0314", "Error Fecha", fecha="2027-01-15")),
    ("Debe rechazarse", "Solicita una materia que no está en el historial", base_error("E15-materia-no-esta-en-historial.pdf", "SIHA-2026-0315", "Error Materia Inexistente",
        solicitudes=TI_SOL + [(["Sistemas Digitales"], "Sistemas Operativos")])),
    ("Debe rechazarse", "Carrera de destino que no existe en el catálogo (Ingeniería Civil)", base_error("E16-carrera-no-existe.pdf", "SIHA-2026-0316", "Error Carrera Catálogo", destino="Ingeniería Civil")),
    ("Debe rechazarse", "No pide ninguna homologación", base_error("E17-sin-solicitudes.pdf", "SIHA-2026-0317", "Error Sin Solicitudes", solicitudes=[])),
]


def pdf_escaneado(origen: str, destino: str):
    """Convierte un certificado válido en un PDF de imagen (sin texto seleccionable)."""
    from PIL import Image, ImageDraw, ImageFont
    from reportlab.lib.utils import ImageReader
    from reportlab.pdfgen import canvas
    img = Image.new("L", (1240, 1754), 255)
    d = ImageDraw.Draw(img)
    try:
        f = ImageFont.truetype("DejaVuSans.ttf", 26)
    except OSError:
        f = ImageFont.load_default()
    y = 80
    for linea in ["UNIVERSIDAD TÉCNICA DE AMBATO", "CERTIFICADO DE HOMOLOGACIÓN SIHA-IA", "Formato SIHA-CH-01",
                  "Número de certificado: SIHA-2026-0318", "Estudiante: Error Escaneado", "Cédula: 1804111222",
                  "Carrera de origen: Tecnologías de la Información", "Carrera de destino: Software",
                  "(documento escaneado: el texto es una imagen)", "FIN DEL CERTIFICADO"]:
        d.text((90, y), linea, fill=0, font=f)
        y += 60
    buf = io.BytesIO()
    img.rotate(0.6, fillcolor=255).save(buf, format="PNG")
    buf.seek(0)
    c = canvas.Canvas(destino, pagesize=g.A4)
    c.drawImage(ImageReader(buf), 0, 0, width=g.ANCHO, height=g.ALTO)
    c.save()


def main():
    os.makedirs(SALIDA, exist_ok=True)
    g.SALIDA = SALIDA  # generar() escribe en g.SALIDA
    filas = []
    for esperado, descripcion, c in CASOS:
        g.generar(c)
        filas.append((c.archivo, esperado, descripcion))
    escaneado = os.path.join(SALIDA, "E18-pdf-escaneado.pdf")
    pdf_escaneado(None, escaneado)
    filas.append(("E18-pdf-escaneado.pdf", "Debe rechazarse", "PDF escaneado (imagen, sin texto seleccionable)"))
    plan = os.path.join(SALIDA, "E19-plan-analitico-suelto.pdf")
    g.plan_analitico_no_valido(plan)
    filas.append(("E19-plan-analitico-suelto.pdf", "Debe rechazarse", "No es la plantilla SIHA-CH-01 (plan analítico suelto)"))

    with open(os.path.join(SALIDA, "LEEME.md"), "w", encoding="utf-8") as fh:
        fh.write("# Certificados SIHA-CH-01 de prueba\n\nGenerados con `python tools/certificados-siha/generar_pruebas.py`.\n\n")
        fh.write("| Archivo | Resultado esperado | Qué prueba |\n|---|---|---|\n")
        for a, e, dsc in filas:
            fh.write(f"| `{a}` | {e} | {dsc} |\n")
    for a, e, dsc in filas:
        print(f"{a:45s} {e:18s} {dsc}")


if __name__ == "__main__":
    main()
