using FluentAssertions;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>
/// S2-P04 (US-09): Pruebas normativas de proporciones, prescripción de 10 años y controles previos.
/// Responsable: Paul Acurio (Product Owner Proxy & AI Lead).
/// 
/// Validaciones de los Criterios de Aceptación (US-09):
/// CA1: Pcred correcto:
///      - 4 vs 3 -> 100.00 % (tope máximo de proporción)
///      - 2 vs 3 -> 66.67 %
///      - Denominador nulo (0 créditos destino) -> error controlado (rechazo).
///      - Pcred se muestra SOLO como dato de apoyo (no decide por sí solo la homologación).
/// CA2: Ruta CAU-P-011:
///      - Asignatura cursada hace > 10 años -> categoría VALIDACION_CONOCIMIENTOS (RN-03).
///      - Asignatura cursada hace <= 10 años -> categoría NORMAL.
///      - Otras rutas (ej. CAU-P-122) -> no aplica la regla de 10 años.
/// CA3: Cambio de carrera y origen externo:
///      - 3ra matrícula en malla destino -> bloqueo duro BLOQUEO_3RA_MATRICULA (RN-16).
///      - Créditos externos -> conversión confirmada, alerta de NO aplicar 48h/crédito automático (RN-15).
/// </summary>
public class PruebasNormativasCreditosTests
{
    private readonly IReglaCreditosStrategy _estrategia = new ReglaCreditosStrategy();

    [Fact]
    public void CA1_Proporcion_4_vs_3_alcanza_tope_100_porciento()
    {
        // 4 créditos cursados vs 3 solicitados -> proporción matemática 133.33% -> acotado a 100.00%
        var parametros = new ParametrosCreditos(
            CreditosOrigen: 4,
            CreditosDestino: 3,
            AnosTranscurridos: 3,
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011");

        var resultado = _estrategia.Calcular(parametros);

        resultado.Pcred.Should().Be(100.00m);
        resultado.Categoria.Should().Be("NORMAL");
        resultado.BloqueoDuro.Should().BeFalse();
        resultado.DetalleCalculo.Should().Contain("min(100, 4 / 3 * 100)");
    }

    [Fact]
    public void CA1_Proporcion_2_vs_3_calcula_exactamente_66_67_porciento()
    {
        // 2 créditos cursados vs 3 solicitados -> 66.67%
        var parametros = new ParametrosCreditos(
            CreditosOrigen: 2,
            CreditosDestino: 3,
            AnosTranscurridos: 1,
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011");

        var resultado = _estrategia.Calcular(parametros);

        resultado.Pcred.Should().Be(66.67m);
        resultado.Categoria.Should().Be("NORMAL");
    }

    [Fact]
    public void CA1_Denominador_cero_produce_rechazo_controlado_sin_excepcion_no_controlada()
    {
        // Denominador nulo: créditos destino = 0
        var parametros = new ParametrosCreditos(
            CreditosOrigen: 3,
            CreditosDestino: 0,
            AnosTranscurridos: 2,
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011");

        var accion = () => _estrategia.Calcular(parametros);

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage("*denominador nulo*");
    }

    [Theory]
    [InlineData(11, "VALIDACION_CONOCIMIENTOS", true)]
    [InlineData(15, "VALIDACION_CONOCIMIENTOS", true)]
    [InlineData(10, "NORMAL", false)]
    [InlineData(9, "NORMAL", false)]
    [InlineData(0, "NORMAL", false)]
    public void CA2_Vigencia_anos_en_ruta_CAU_P_011_deriva_a_validacion_conocimientos(
        int anos, string categoriaEsperada, bool debeTenerAlertaRN03)
    {
        var parametros = new ParametrosCreditos(
            CreditosOrigen: 3,
            CreditosDestino: 3,
            AnosTranscurridos: anos,
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011");

        var resultado = _estrategia.Calcular(parametros);

        resultado.Categoria.Should().Be(categoriaEsperada);
        resultado.BloqueoDuro.Should().BeFalse("RN-03 es una derivación a validación, no un bloqueo duro del sistema");

        if (debeTenerAlertaRN03)
        {
            resultado.Alertas.Should().Contain(a => a.Contains("RN-03") && a.Contains("validación de conocimientos"));
        }
        else
        {
            resultado.Alertas.Should().NotContain(a => a.Contains("RN-03"));
        }
    }

    [Fact]
    public void CA2_Ruta_movilidad_CAU_P_122_no_aplica_prescripcion_de_10_anos()
    {
        // En movilidad no aplica la prescripción de 10 años de cambio de carrera
        var parametros = new ParametrosCreditos(
            CreditosOrigen: 4,
            CreditosDestino: 4,
            AnosTranscurridos: 14,
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-122");

        var resultado = _estrategia.Calcular(parametros);

        resultado.Categoria.Should().Be("NORMAL");
        resultado.Alertas.Should().NotContain(a => a.Contains("RN-03"));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(2, false)]
    [InlineData(1, false)]
    public void CA3_Tercera_matricula_en_malla_destino_provoca_bloqueo_duro_RN16(int matricula, bool debeBloquear)
    {
        var parametros = new ParametrosCreditos(
            CreditosOrigen: 4,
            CreditosDestino: 4,
            AnosTranscurridos: 2,
            NumeroMatriculaDestino: matricula,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011");

        var resultado = _estrategia.Calcular(parametros);

        if (debeBloquear)
        {
            resultado.Categoria.Should().Be("BLOQUEO_3RA_MATRICULA");
            resultado.BloqueoDuro.Should().BeTrue();
            resultado.Alertas.Should().Contain(a => a.Contains("RN-16") && a.Contains("3ra matrícula"));
        }
        else
        {
            resultado.BloqueoDuro.Should().BeFalse();
            resultado.Categoria.Should().NotBe("BLOQUEO_3RA_MATRICULA");
        }
    }

    [Fact]
    public void CA3_Creditos_externos_generan_alerta_normativa_RN15_sin_bloqueo()
    {
        var parametros = new ParametrosCreditos(
            CreditosOrigen: 4,
            CreditosDestino: 4,
            AnosTranscurridos: 1,
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: true,
            RutaNormativa: "CAU-P-011");

        var resultado = _estrategia.Calcular(parametros);

        resultado.BloqueoDuro.Should().BeFalse();
        resultado.Alertas.Should().Contain(a => a.Contains("RN-15") && a.Contains("48h/crédito"));
    }

    [Fact]
    public void Jerarquia_normativa_bloqueo_tercera_matricula_prevalece_sobre_validacion_conocimientos()
    {
        // Caso donde ocurren ambos: >10 años Y 3ra matrícula
        var parametros = new ParametrosCreditos(
            CreditosOrigen: 4,
            CreditosDestino: 4,
            AnosTranscurridos: 12,
            NumeroMatriculaDestino: 3,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011");

        var resultado = _estrategia.Calcular(parametros);

        resultado.BloqueoDuro.Should().BeTrue("El bloqueo duro debe impedir la tramitación");
        resultado.Categoria.Should().Be("BLOQUEO_3RA_MATRICULA");
        resultado.Alertas.Should().HaveCount(2, "debe alertar de ambos motivos normativos para total trazabilidad");
    }
}
