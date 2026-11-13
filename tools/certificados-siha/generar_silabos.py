# -*- coding: utf-8 -*-
"""Sílabos (planes analíticos) de otra universidad para probar la movilidad externa.

Acompañan a `documentos-prueba/certificados-pruebas/V06-movilidad-externa-TI-SW.pdf`
(Escuela Superior Politécnica de Chimborazo, TI → Software): un sílabo por cada materia
aprobada que ese certificado pide homologar, más dos documentos inválidos para probar
los mensajes de error. Se adjuntan en la pantalla "Registrar solicitud".

Uso: pip install reportlab && python tools/certificados-siha/generar_silabos.py
"""
from __future__ import annotations

import os
from dataclasses import dataclass

from reportlab.lib.colors import HexColor
from reportlab.lib.pagesizes import A4
from reportlab.pdfgen import canvas

VERDE = HexColor("#1b5e20")  # color institucional de la universidad de origen (ficticio)
ANCHO, ALTO = A4
MARGEN = 50
DESTINO = os.path.join(os.path.dirname(__file__), "..", "..", "documentos-prueba", "silabos-externos")


@dataclass
class Unidad:
    numero: int
    nombre: str
    horas: int | None
    temas: list


@dataclass
class Silabo:
    archivo: str
    materia: str
    creditos: int | None
    horas_totales: int | None
    unidades: list


class Escritor:
    def __init__(self, ruta: str, titulo: str):
        self.c = canvas.Canvas(ruta, pagesize=A4)
        self.c.setTitle(titulo)
        self.y = ALTO - MARGEN

    def espacio(self, alto: float):
        if self.y - alto < 60:
            self.c.showPage()
            self.y = ALTO - MARGEN

    def linea(self, texto: str, fuente="Helvetica", tam=10, x0=MARGEN, color=None):
        self.espacio(tam + 5)
        self.c.setFont(fuente, tam)
        if color:
            self.c.setFillColor(color)
        self.c.drawString(x0, self.y, texto)
        self.c.setFillColor(HexColor("#000000"))
        self.y -= tam + 5

    def centrada(self, texto: str, fuente="Helvetica-Bold", tam=12, color=None):
        self.espacio(tam + 6)
        self.c.setFont(fuente, tam)
        if color:
            self.c.setFillColor(color)
        self.c.drawCentredString(ANCHO / 2, self.y, texto)
        self.c.setFillColor(HexColor("#000000"))
        self.y -= tam + 6

    def cerrar(self):
        self.c.save()


def dibujar(s: Silabo):
    ruta = os.path.join(DESTINO, s.archivo)
    e = Escritor(ruta, f"Sílabo de {s.materia}")
    e.centrada("ESCUELA SUPERIOR POLITÉCNICA DE CHIMBORAZO", tam=13, color=VERDE)
    e.centrada("FACULTAD DE INFORMÁTICA Y ELECTRÓNICA", tam=10, color=VERDE)
    e.centrada("SÍLABO DE LA ASIGNATURA", tam=11)
    e.linea("")
    e.linea(f"Asignatura: {s.materia}", "Helvetica-Bold", 11)
    if s.creditos is not None:
        e.linea(f"Créditos: {s.creditos}")
    if s.horas_totales is not None:
        e.linea(f"Horas totales: {s.horas_totales}")
    e.linea("Carrera: Tecnologías de la Información")
    e.linea("")
    e.linea("CONTENIDOS", "Helvetica-Bold", 11, color=VERDE)
    for u in s.unidades:
        horas = f" ({u.horas} horas)" if u.horas is not None else ""
        e.linea(f"Unidad {u.numero}: {u.nombre}{horas}", "Helvetica-Bold", 10)
        for tema in u.temas:
            e.linea(f"- {tema}", x0=MARGEN + 14, tam=9)
    e.linea("")
    e.linea("Bibliografía", "Helvetica-Bold", 10, color=VERDE)
    e.linea("Material bibliográfico de la asignatura según la biblioteca institucional.", tam=9)
    e.cerrar()
    return ruta


# Sílabos válidos: las cuatro materias que V06 pide homologar. Los temas siguen de cerca a los
# sílabos UTA de destino para que el agente encuentre coincidencias al hacer la demostración.
VALIDOS = [
    Silabo("ESPOCH-fundamentos-de-programacion.pdf", "Fundamentos de Programación", 4, None, [
        Unidad(1, "Introducción a los algoritmos", 36, [
            "Concepto de algoritmo y resolución de problemas",
            "Datos, variables, constantes y expresiones",
            "Diagramas de flujo y pseudocódigo"]),
        Unidad(2, "Estructuras de control", 36, [
            "Estructuras secuenciales y condicionales",
            "Estructuras repetitivas: mientras, hacer-mientras y para",
            "Anidamiento de estructuras de control"]),
        Unidad(3, "Funciones y modularidad", 36, [
            "Definición de funciones y paso de parámetros",
            "Ámbito de las variables y recursividad básica",
            "Diseño modular de programas"]),
        Unidad(4, "Arreglos y cadenas", 36, [
            "Arreglos unidimensionales y bidimensionales",
            "Cadenas de caracteres y sus operaciones",
            "Ordenamiento y búsqueda en arreglos"]),
    ]),
    Silabo("ESPOCH-algebra-lineal.pdf", "Álgebra Lineal", 3, None, [
        Unidad(1, "Matrices y determinantes", 32, [
            "Operaciones con matrices",
            "Determinantes y sus propiedades",
            "Matriz inversa"]),
        Unidad(2, "Sistemas de ecuaciones lineales", 32, [
            "Métodos de Gauss y Gauss-Jordan",
            "Regla de Cramer",
            "Aplicaciones de los sistemas lineales"]),
        Unidad(3, "Espacios vectoriales", 32, [
            "Subespacios, base y dimensión",
            "Independencia lineal",
            "Transformaciones lineales"]),
        Unidad(4, "Valores y vectores propios", 32, [
            "Polinomio característico",
            "Diagonalización de matrices",
            "Aplicaciones geométricas"]),
    ]),
    Silabo("ESPOCH-fisica.pdf", "Física", 3, None, [
        Unidad(1, "Estática", 32, [
            "Magnitudes físicas y vectores",
            "Equilibrio de partículas y cuerpos rígidos",
            "Diagramas de cuerpo libre"]),
        Unidad(2, "Cinemática y dinámica", 32, [
            "Movimiento rectilíneo y en el plano",
            "Leyes de Newton y sus aplicaciones",
            "Fricción y dinámica del movimiento circular"]),
        Unidad(3, "Trabajo y energía", 32, [
            "Trabajo y potencia mecánica",
            "Energía cinética y potencial",
            "Conservación de la energía"]),
        Unidad(4, "Electricidad básica", 32, [
            "Carga eléctrica y ley de Coulomb",
            "Campo y potencial eléctrico",
            "Circuitos de corriente continua"]),
    ]),
    Silabo("ESPOCH-estructura-de-datos.pdf", "Estructura de Datos", 4, None, [
        Unidad(1, "Estructuras lineales", 36, [
            "Listas, pilas y colas",
            "Listas enlazadas simples y dobles",
            "Aplicaciones de las estructuras lineales"]),
        Unidad(2, "Recursividad", 36, [
            "Diseño de algoritmos recursivos",
            "Recursión y pilas de llamadas",
            "Análisis de casos recursivos"]),
        Unidad(3, "Árboles", 36, [
            "Árboles binarios y de búsqueda",
            "Recorridos de árboles",
            "Árboles balanceados"]),
        Unidad(4, "Grafos y ordenamiento", 36, [
            "Representación de grafos",
            "Recorridos en anchura y profundidad",
            "Algoritmos de ordenamiento avanzados"]),
    ]),
]

# Documentos inválidos para probar los mensajes de error al adjuntar.
INVALIDOS = [
    Silabo("silabo-sin-horas.pdf", "Física", 3, None, [
        Unidad(1, "Estática", None, ["Equilibrio de partículas"]),
        Unidad(2, "Dinámica", None, ["Leyes de Newton"]),
    ]),
    Silabo("silabo-sin-unidades.pdf", "Física", 3, 128, []),
]


def main():
    os.makedirs(DESTINO, exist_ok=True)
    for s in VALIDOS + INVALIDOS:
        ruta = dibujar(s)
        print(f"OK  {os.path.relpath(ruta)}")


if __name__ == "__main__":
    main()
