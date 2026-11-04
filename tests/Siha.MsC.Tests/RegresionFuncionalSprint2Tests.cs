using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Siha.MsA.Api.Grpc;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Grpc;
using Siha.MsC.Api.Options;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>
/// S2-P10 (US-11): Regresión funcional completa del Sprint 2 (US-07 a US-11).
/// Responsable: Paul Acurio (Product Owner Proxy & AI Lead).
/// 
/// Escenarios de validación extremo a extremo:
/// 1. Caso interno (malla FISEI): extracción con texto limpio, 100% confianza, cálculo de reglas y precedentes.
/// 2. Caso externo (PDF con texto nativo): extracción base, proporción créditos, similitud de contenidos.
/// 3. Caso externo (PDF escaneado): detección de escaneado -> flujo PENDIENTE_OCR_LLM -> confirmación humana -> análisis final.
/// 4. Flujo con controles normativos y precedentes combinados.
/// </summary>
public class RegresionFuncionalSprint2Tests
{
    private readonly DbContextOptions<SihaDbContextC> _dbOptions;

    public RegresionFuncionalSprint2Tests()
    {
        _dbOptions = new DbContextOptionsBuilder<SihaDbContextC>()
            .UseInMemoryDatabase(databaseName: $"SihaDbC_Regresion_Paul_{Guid.NewGuid()}")
            .Options;
    }

    [Fact]
    public async Task Regresion_Caso1_Interno_Catalogo_ExtraccionLimpia_y_AnalisisCompleto()
    {
        // 1. Extractor procesa texto oficial del catálogo interno
        using var db = new SihaDbContextC(_dbOptions);
        var extractor = new ExtractorPdfService(
            Options.Create(new AlmacenOptions { RutaAlmacenamiento = "archivos" }),
            NullLogger<ExtractorPdfService>.Instance);

        var textoSyllabus = @"
            UNIVERSIDAD TÉCNICA DE AMBATO
            Asignatura: Programación Orientada a Objetos
            Código: SW201
            Créditos: 4
            Horas totales: 120
            
            Unidad 1: Clases y Objetos (30 horas)
            Conceptos de Clases y objetos, atributos, métodos y encapsulamiento.
            Unidad 2: Herencia y Polimorfismo (40 horas)
            Herencia simple, clases abstractas y polimorfismo en tiempo de ejecución.
            Unidad 3: Manejo de Excepciones (20 horas)
            Excepciones verificadas, bloques try/catch y manejo de errores.
            Unidad 4: Colecciones y Genéricos (30 horas)
            Colecciones, listas, mapas y tipos genéricos.
        ";

        var resExtraccion = await extractor.ExtraerDeTextoAsync(textoSyllabus, tieneCapaTexto: true);
        resExtraccion.Confianza.Should().Be(1.00m);
        resExtraccion.EstadoSugerido.Should().Be(EstadosExtraccion.Confirmada);

        // 2. Persistir extracción en DB C
        var extraccionId = Guid.NewGuid();
        var entity = new Extraccion
        {
            Id = extraccionId,
            ExpedienteId = Guid.NewGuid(),
            CodigoAsignaturaOrigen = resExtraccion.CodigoAsignaturaOrigen,
            NombreAsignaturaOrigen = resExtraccion.NombreAsignaturaOrigen,
            Creditos = resExtraccion.Creditos,
            Horas = resExtraccion.Horas,
            Confianza = resExtraccion.Confianza,
            Estado = EstadosExtraccion.Confirmada,
            ConfirmadoPor = "sistema@uta.edu.ec",
            ConfirmadoUtc = DateTimeOffset.UtcNow,
            Unidades = resExtraccion.Unidades.Select(u => new ExtraccionUnidad
            {
                Numero = u.Numero,
                Nombre = u.Nombre,
                Horas = u.Horas,
                Contenidos = u.Contenidos
            }).ToList()
        };
        db.Extracciones.Add(entity);
        await db.SaveChangesAsync();

        // 3. Orquestador AgenteAnalizador ejecuta reglas gRPC + créditos + contenidos + precedentes
        var mockGrpc = new Mock<IReglasGrpcClient>();
        mockGrpc.Setup(g => g.ObtenerConfiguracionAsync("CAU-P-011", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguracionRuta
            {
                RutaNormativa = "CAU-P-011",
                ParametrosConfigurados = true,
                Parametros = new ParametrosEvaluacionMensaje { Theta = 80, Delta = 15, Tau = 75, Version = 1 }
            });

        var mockOllama = new Mock<IOllamaEmbeddingService>();
        // Asignar vectores únicos por unidad para garantizar cobertura 1:1
        mockOllama.Setup(o => o.GenerarEmbeddingAsync(It.Is<string>(s => s.Contains("Clases")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 1, 0, 0, 0 });
        mockOllama.Setup(o => o.GenerarEmbeddingAsync(It.Is<string>(s => s.Contains("Herencia")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0, 1, 0, 0 });
        mockOllama.Setup(o => o.GenerarEmbeddingAsync(It.Is<string>(s => s.Contains("Excepciones")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0, 0, 1, 0 });
        mockOllama.Setup(o => o.GenerarEmbeddingAsync(It.Is<string>(s => s.Contains("Colecciones")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0, 0, 0, 1 });

        var agente = new AgenteAnalizadorService(
            db,
            new ExtraccionService(db),
            mockGrpc.Object,
            new ReglaCreditosStrategy(),
            new ReglaContenidoStrategy(),
            new PrecedenteService(db),
            mockOllama.Object,
            NullLogger<AgenteAnalizadorService>.Instance);

        var param = new ParametrosAnalisisCompleto(
            AnosTranscurridos: 1,
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011",
            CodigoAsignaturaDestino: "SW201",
            InstitucionOrigen: "UTA",
            CreditosDestino: 4,
            UnidadesDestino: new[]
            {
                new UnidadDestinoEmbeddingRequest(1, "Clases y Objetos", 30, "POO", new float[] { 1, 0, 0, 0 }),
                new UnidadDestinoEmbeddingRequest(2, "Herencia y Polimorfismo", 40, "POO", new float[] { 0, 1, 0, 0 }),
                new UnidadDestinoEmbeddingRequest(3, "Manejo de Excepciones", 20, "POO", new float[] { 0, 0, 1, 0 }),
                new UnidadDestinoEmbeddingRequest(4, "Colecciones y Genéricos", 30, "POO", new float[] { 0, 0, 0, 1 })
            }
        );

        var resultado = await agente.AnalizarAsync(extraccionId, param);

        // Assert
        resultado.Creditos.Pcred.Should().Be(100.00m);
        resultado.Contenido.Pcont.Should().Be(100.00m);
        resultado.Contenido.UnidadesCubiertas.Should().HaveCount(4);
        resultado.EstadoFinal.Should().Be(EstadoEvaluacionFinal.ListaParaDictamen);
        resultado.DetalleTrazabilidad.Should().Contain("θ=80");
    }

    [Fact]
    public async Task Regresion_Caso2_Externo_Escaneado_Deteccion_ConfirmacionHumana_y_Evaluacion()
    {
        // 1. Detección de PDF escaneado (sin capa de texto)
        using var db = new SihaDbContextC(_dbOptions);
        var extractor = new ExtractorPdfService(
            Options.Create(new AlmacenOptions { RutaAlmacenamiento = "archivos" }),
            NullLogger<ExtractorPdfService>.Instance);

        var resEscaneado = await extractor.ExtraerDeTextoAsync("", tieneCapaTexto: false, codigoAsignaturaFallback: "EXT-FIS");
        resEscaneado.EsEscaneado.Should().BeTrue();
        resEscaneado.EstadoSugerido.Should().Be(EstadosExtraccion.PendienteOcrLlm);

        // 2. Persistir en DB C en estado PendienteConfirmacion (simulando paso por OCR/LLM)
        var extraccionId = Guid.NewGuid();
        var entity = new Extraccion
        {
            Id = extraccionId,
            ExpedienteId = Guid.NewGuid(),
            CodigoAsignaturaOrigen = "EXT-FIS",
            NombreAsignaturaOrigen = "Física para Ingenieros",
            Creditos = null, // OCR con baja confianza
            Horas = null,
            Confianza = 0.50m,
            Estado = EstadosExtraccion.PendienteConfirmacion
        };
        db.Extracciones.Add(entity);
        await db.SaveChangesAsync();

        // 3. Confirmación humana por el Operador (S2-J02 / US-07)
        var extraccionService = new ExtraccionService(db);
        var confirmacion = new ConfirmarExtraccionRequest(
            Creditos: 3,
            Horas: 96,
            Unidades: new[]
            {
                new UnidadExtraccionRequest(1, "Cinemática", 48, "Movimiento en 1D y 2D"),
                new UnidadExtraccionRequest(2, "Dinámica", 48, "Leyes de Newton")
            },
            Observacion: "Corregido tras revisión visual de documento escaneado por operador");

        var resConfirmacion = await extraccionService.ConfirmarAsync(
            extraccionId, confirmacion, "operador@uta.edu.ec");

        resConfirmacion.Resultado.Should().Be(ResultadoConfirmacionExtraccion.Confirmada);
        resConfirmacion.Extraccion!.Estado.Should().Be(EstadosExtraccion.Confirmada);
        resConfirmacion.Extraccion.Creditos.Should().Be(3);
        resConfirmacion.Extraccion.Horas.Should().Be(96);

        // 4. Agente analiza con los datos ya confirmados
        var mockGrpc = new Mock<IReglasGrpcClient>();
        mockGrpc.Setup(g => g.ObtenerConfiguracionAsync("CAU-P-011", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguracionRuta
            {
                RutaNormativa = "CAU-P-011",
                ParametrosConfigurados = true,
                Parametros = new ParametrosEvaluacionMensaje { Theta = 80, Delta = 15, Tau = 75, Version = 1 }
            });

        var mockOllama = new Mock<IOllamaEmbeddingService>();
        mockOllama.Setup(o => o.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 1, 0 });

        var agente = new AgenteAnalizadorService(
            db,
            extraccionService,
            mockGrpc.Object,
            new ReglaCreditosStrategy(),
            new ReglaContenidoStrategy(),
            new PrecedenteService(db),
            mockOllama.Object,
            NullLogger<AgenteAnalizadorService>.Instance);

        var param = new ParametrosAnalisisCompleto(
            AnosTranscurridos: 3,
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: true, // Crédito externo!
            RutaNormativa: "CAU-P-011",
            CodigoAsignaturaDestino: "SW-FISICA",
            InstitucionOrigen: "Universidad Externa",
            CreditosDestino: 4,
            UnidadesDestino: new[]
            {
                new UnidadDestinoEmbeddingRequest(1, "Cinemática y Dinámica", 96, "Mecánica", new float[] { 1, 0 })
            }
        );

        var resultado = await agente.AnalizarAsync(extraccionId, param);

        // Assert
        // Pcred = min(100, 3/4 * 100) = 75.00%
        resultado.Creditos.Pcred.Should().Be(75.00m);
        resultado.Creditos.Alertas.Should().Contain(a => a.Contains("RN-15") && a.Contains("externos"));
        resultado.EstadoFinal.Should().Be(EstadoEvaluacionFinal.ListaParaDictamen);
    }
}
