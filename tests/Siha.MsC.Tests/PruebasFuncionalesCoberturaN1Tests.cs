using FluentAssertions;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>
/// S2-P07 (US-10): Pruebas funcionales de cobertura combinada N:1 y detección de temario faltante.
/// Responsable: Paul Acurio (Product Owner Proxy & AI Lead).
/// 
/// Validaciones de los Criterios de Aceptación (US-10):
/// CA2: Unidad destino cubierta si max coseno >= 0.75; Pcont ponderado por horas.
///      Caso emblemático del Sprint: 6 de 7 unidades igual duración cubiertas -> 85.71 %,
///      con detección explícita y precisa de la unidad faltante (ej. U.7).
/// CA3: Cobertura N:1 (varias asignaturas origen -> una destino):
///      - Unión matemática de unidades cubiertas sin doble conteo.
///      - RN-19 impide reutilizar una asignatura de origen en dos materias de destino del mismo trámite.
/// </summary>
public class PruebasFuncionalesCoberturaN1Tests
{
    private readonly IReglaContenidoStrategy _reglaContenido = new ReglaContenidoStrategy();

    private static UnidadOrigenEmbedding CrearOrigen(int numero, string nombre, int horas, params float[] emb)
        => new(numero, nombre, horas, $"Contenido de {nombre}", emb);

    private static UnidadDestinoEmbedding CrearDestino(int numero, string nombre, int horas, params float[] emb)
        => new(numero, nombre, horas, $"Contenido de {nombre}", emb);

    [Fact]
    public void Cobertura_6_de_7_unidades_identifica_exactamente_unidad_7_faltante_y_pcont_85_71()
    {
        // Caso exacto de la ficha técnica US-10 y Mockup M-05:
        // 7 unidades de destino (cada una de 12 horas, total 84 horas).
        // 6 unidades cubiertas por el origen con similitud >= 0.75.
        // Unidad 7 (Archivos y persistencia) tiene similitud 0.42 < 0.75 -> Faltante.
        var origen = new[]
        {
            CrearOrigen(1, "U.1 Lógica de programación", 12, 1, 0, 0, 0, 0, 0, 0),
            CrearOrigen(2, "U.2 Datos y operadores", 12, 0, 1, 0, 0, 0, 0, 0),
            CrearOrigen(3, "U.3 Condicionales y bucles", 12, 0, 0, 1, 0, 0, 0, 0),
            CrearOrigen(4, "U.4 Subprogramas", 12, 0, 0, 0, 1, 0, 0, 0),
            CrearOrigen(5, "U.5 Vectores y matrices", 12, 0, 0, 0, 0, 1, 0, 0),
            CrearOrigen(6, "U.6 Recursividad", 12, 0, 0, 0, 0, 0, 1, 0)
        };

        var destino = new[]
        {
            CrearDestino(1, "U.1 Algoritmos y diagramas de flujo", 12, 1, 0, 0, 0, 0, 0, 0), // Cubierta por O1
            CrearDestino(2, "U.2 Variables y tipos de datos", 12, 0, 1, 0, 0, 0, 0, 0),       // Cubierta por O2
            CrearDestino(3, "U.3 Estructuras de control", 12, 0, 0, 1, 0, 0, 0, 0),           // Cubierta por O3
            CrearDestino(4, "U.4 Funciones", 12, 0, 0, 0, 1, 0, 0, 0),                       // Cubierta por O4
            CrearDestino(5, "U.5 Arreglos", 12, 0, 0, 0, 0, 1, 0, 0),                        // Cubierta por O5
            CrearDestino(6, "U.6 Cadenas de caracteres", 12, 0, 0, 0, 0, 0, 1, 0),            // Cubierta por O6
            // U.7 ortogonal a todos los orígenes (dimensión 7) -> Similitud = 0.0 < 0.75
            CrearDestino(7, "U.7 Archivos y persistencia", 12, 0, 0, 0, 0, 0, 0, 1)
        };

        var param = new ParametrosContenido(origen, destino, Tau: 75.0, RutaNormativa: "CAU-P-011");

        // Act
        var res = _reglaContenido.Calcular(param);

        // Assert
        // Pcont = (6 * 12) / (7 * 12) * 100 = 72 / 84 * 100 = 85.71 %
        res.Pcont.Should().Be(85.71m);
        res.UnidadesCubiertas.Should().HaveCount(6);
        res.UnidadesFaltantes.Should().HaveCount(1);

        var faltante = res.UnidadesFaltantes.Single();
        faltante.Numero.Should().Be(7);
        faltante.Nombre.Should().Be("U.7 Archivos y persistencia");
        faltante.Cubierta.Should().BeFalse();
        faltante.SimilitudMaxima.Should().BeLessThan(75.0);
    }

    [Fact]
    public void Cobertura_combinada_N_a_1_Fisica_I_y_Fisica_II_homologan_Fisica_sin_doble_conteo()
    {
        // Escenario N:1:
        // Origen proviene de dos materias:
        // Materia A (Física I): Mecánica (U1) y Dinámica (U2)
        // Materia B (Física II): Electromagnetismo (U3) y Óptica (U4)
        // Destino (Física General): Mecánica (D1), Dinámica (D2), Electromagnetismo (D3), Óptica (D4)
        var origen = new[]
        {
            CrearOrigen(1, "Mecánica Newtoneana (Física I)", 40, 1, 0, 0, 0),
            CrearOrigen(2, "Dinámica y Energía (Física I)", 40, 0, 1, 0, 0),
            CrearOrigen(3, "Electrostática y Magnetismo (Física II)", 40, 0, 0, 1, 0),
            CrearOrigen(4, "Óptica Ondulatoria (Física II)", 40, 0, 0, 0, 1)
        };

        var destino = new[]
        {
            CrearDestino(1, "Mecánica Clásica", 30, 1, 0, 0, 0),
            CrearDestino(2, "Dinámica", 30, 0, 1, 0, 0),
            CrearDestino(3, "Campos Electromagnéticos", 30, 0, 0, 1, 0),
            CrearDestino(4, "Óptica General", 30, 0, 0, 0, 1)
        };

        var param = new ParametrosContenido(origen, destino, Tau: 75.0, RutaNormativa: "CAU-P-011");

        // Act
        var res = _reglaContenido.Calcular(param);

        // Assert
        res.Pcont.Should().Be(100.00m);
        res.UnidadesCubiertas.Should().HaveCount(4);
        res.UnidadesFaltantes.Should().BeEmpty();
        res.ViolacionRN19.Should().BeFalse();
    }

    [Fact]
    public void Cobertura_N_a_1_sin_doble_conteo_cuando_dos_unidades_origen_cubren_la_misma_destino()
    {
        // Dos unidades de origen (U1 y U2) son muy similares a la misma unidad destino D1
        // Se debe tomar la máxima similitud y contar las horas de D1 una sola vez.
        var origen = new[]
        {
            CrearOrigen(1, "Álgebra Lineal Básica", 30, 1, 0),
            CrearOrigen(2, "Álgebra Lineal Avanzada", 40, 0.95f, 0.31f) // Coseno con D1 ~ 0.95
        };

        var destino = new[]
        {
            CrearDestino(1, "Álgebra Lineal", 50, 1, 0),       // Cubierta por U1 y U2
            CrearDestino(2, "Cálculo Integral", 50, 0, 1)      // Sin origen que la cubra
        };

        var param = new ParametrosContenido(origen, destino, Tau: 75.0, RutaNormativa: "CAU-P-011");

        // Act
        var res = _reglaContenido.Calcular(param);

        // Assert
        // Horas totales destino = 100
        // Horas cubiertas = 50 (NO se duplica a 100 por haber 2 orígenes)
        res.Pcont.Should().Be(50.00m);
        res.UnidadesCubiertas.Should().HaveCount(1);
        res.UnidadesFaltantes.Should().HaveCount(1);
        res.UnidadesFaltantes.Single().Numero.Should().Be(2);
    }

    [Fact]
    public void RN19_impide_reutilizar_materia_origen_en_dos_destinos_mismo_expediente()
    {
        // U1 del origen es idéntica a D1 y D2 (dos materias de destino distintas cubiertas por la misma de origen)
        var origen = new[] { CrearOrigen(1, "Programación General", 60, 1, 0, 0) };
        var destino = new[]
        {
            CrearDestino(1, "Programación I", 30, 1, 0, 0),
            CrearDestino(2, "Programación II", 30, 1, 0, 0)
        };

        var param = new ParametrosContenido(origen, destino, Tau: 75.0, RutaNormativa: "CAU-P-011", ValidarRN19: true);

        // Act
        var res = _reglaContenido.Calcular(param);

        // Assert (RN-19: no reutilizar origen en dos destinos del mismo trámite)
        res.ViolacionRN19.Should().BeTrue();
        res.UnidadesOrigenDuplicadas.Should().Contain(1);
        res.DetalleCalculo.Should().Contain("RN-19 VIOLADO");
    }
}
