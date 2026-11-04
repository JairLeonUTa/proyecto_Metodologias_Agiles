"""Planes analíticos de ejemplo compartidos por el generador de certificados SIHA.

Las mismas unidades se siembran en el catálogo de MS A (DbSeederA.SembrarCatalogoHomologacionAsync):
en Industrial, Física I y Física II son dos asignaturas; en Software, Física I reúne los temas de
ambas en una sola. Por eso Física I de Industrial sola NO alcanza Física I de Software.
"""

FISICA_I_IND = [
    ("Magnitudes físicas y vectores", 48,
     "Sistema internacional de unidades, conversión de unidades, cifras significativas, análisis dimensional, "
     "vectores en el plano y en el espacio, suma de vectores, producto escalar y producto vectorial."),
    ("Cinemática de la partícula", 48,
     "Posición, desplazamiento, velocidad y aceleración; movimiento rectilíneo uniforme y uniformemente variado; "
     "caída libre; movimiento parabólico; movimiento circular uniforme."),
    ("Dinámica de la partícula", 48,
     "Leyes de Newton, diagramas de cuerpo libre, fuerzas de rozamiento estático y cinético, fuerza elástica, "
     "dinámica del movimiento circular."),
    ("Trabajo, energía y cantidad de movimiento", 48,
     "Trabajo de una fuerza, energía cinética y energía potencial, conservación de la energía mecánica, potencia, "
     "impulso, cantidad de movimiento y choques."),
]

FISICA_II_IND = [
    ("Mecánica de fluidos", 48,
     "Densidad y presión, principio de Pascal, principio de Arquímedes y flotación, ecuación de continuidad, "
     "ecuación de Bernoulli."),
    ("Temperatura y calor", 48,
     "Temperatura y escalas termométricas, dilatación térmica, calor específico, cambios de fase, "
     "transferencia de calor, primera ley de la termodinámica."),
    ("Oscilaciones y ondas", 48,
     "Movimiento armónico simple, péndulo simple, ondas mecánicas transversales y longitudinales, sonido, "
     "efecto Doppler."),
    ("Electrostática y circuitos", 48,
     "Carga eléctrica, ley de Coulomb, campo eléctrico, potencial eléctrico, corriente eléctrica, ley de Ohm, "
     "circuitos de corriente continua."),
]

PROGRAMACION_BASICA_IND = [
    ("Introducción y entorno", 48,
     "Estructura de un programa, compilación y depuración; entorno de desarrollo, variables y tipos de datos."),
    ("Estructuras de control", 80,
     "Condicionales, bucles y funciones; paso de parámetros y ámbito de variables."),
    ("Arreglos y cadenas", 64,
     "Recorrido y manipulación básica de arreglos y cadenas de caracteres."),
]

# Universidad de Cuenca (trámite externo)
ESTRUCTURAS_UC = [
    ("Listas y pilas", 32, "Implementación de listas enlazadas, pilas y colas y sus aplicaciones."),
    ("Árboles", 32, "Recorridos en árboles, árboles binarios de búsqueda y su implementación."),
    ("Grafos", 32, "Representación de grafos y algoritmos de recorrido en anchura y profundidad."),
]

PROGRAMACION_UC = [
    ("Introducción y entorno", 32, "Estructura de un programa, compilación y depuración."),
    ("Estructuras de control", 48, "Condicionales, bucles y funciones."),
    ("Arreglos y cadenas", 32, "Recorrido y manipulación básica de arreglos y cadenas."),
]
