using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Siha.MsA.Api.Grpc;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Grpc;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>
/// Pruebas para S2-P08 (Carga de precedentes históricos y consultas exactas)
/// y S2-P09 (No omisión de controles normativos ante precedentes históricos).
/// Responsable: Paul Acurio (Product Owner Proxy & AI Lead).
/// </summary>
public class PrecedentesYControlesNormativosTests
{
    private readonly DbContextOptions<SihaDbContextC> _dbOptions;

    public PrecedentesYControlesNormativosTests()
    {
        _dbOptions = new DbContextOptionsBuilder<SihaDbContextC>()
            .UseInMemoryDatabase(databaseName: $"SihaDbC_Precedentes_Paul_{Guid.NewGuid()}")
            .Options;
    }

    #region S2-P08: Carga de Registros Históricos y Búsqueda Exacta

    [Fact]
    public async Task S2_P08_Carga_precedente_RES_FISEI_2025_031_y_coincidencia_exacta_lo_retorna()
    {
        // Arrange
        using var db = new SihaDbContextC(_dbOptions);
        var service = new PrecedenteService(db);

        // Registrar precedente emblemático RES-FISEI-2025-031: Física I + Física II -> Física General (UTA)
        await service.RegistrarAsync(
            resolucion: "RES-FISEI-2025-031",
            asignaturasOrigen: new[] { ("FIS101", "Física I"), ("FIS102", "Física II") },
            asignaturaDestino: "SW-FISICA",
            institucionOrigen: "Universidad Técnica de Ambato",
            fecha: new DateOnly(2025, 3, 12));

        // Act: Búsqueda con coincidencia exacta (mismos orígenes sin importar orden, mismo destino, misma institución)
        var busquedaExacta = new BuscarPrecedenteRequest(
            CodigosAsignaturasOrigen: new[] { "FIS102", "FIS101" }, // orden invertido pero mismo conjunto
            CodigoAsignaturaDestino: "SW-FISICA",
            InstitucionOrigen: "Universidad Técnica de Ambato");

        var resultados = await service.BuscarAsync(busquedaExacta);

        // Assert (CA1 US-11)
        resultados.Should().HaveCount(1);
        var prec = resultados.Single();
        prec.Resolucion.Should().Be("RES-FISEI-2025-031");
        prec.AsignaturaDestino.Should().Be("SW-FISICA");
        prec.InstitucionOrigen.Should().Be("UNIVERSIDAD TÉCNICA DE AMBATO");
        prec.AsignaturasOrigen.Should().HaveCount(2);
    }

    [Fact]
    public async Task S2_P08_Sin_coincidencia_exacta_devuelve_array_vacio_sin_inventar_parecidos()
    {
        // Arrange
        using var db = new SihaDbContextC(_dbOptions);
        var service = new PrecedenteService(db);

        await service.RegistrarAsync(
            resolucion: "RES-FISEI-2025-031",
            asignaturasOrigen: new[] { ("FIS101", "Física I"), ("FIS102", "Física II") },
            asignaturaDestino: "SW-FISICA",
            institucionOrigen: "Universidad Técnica de Ambato",
            fecha: new DateOnly(2025, 3, 12));

        // Act 1: Solo una de las dos materias de origen (Física I sola no es coincidencia exacta)
        var busquedaParcial = new BuscarPrecedenteRequest(
            CodigosAsignaturasOrigen: new[] { "FIS101" },
            CodigoAsignaturaDestino: "SW-FISICA",
            InstitucionOrigen: "Universidad Técnica de Ambato");

        var resParcial = await service.BuscarAsync(busquedaParcial);

        // Act 2: Misma materia pero de otra institución externa (EPN en vez de UTA)
        var busquedaOtraInstitucion = new BuscarPrecedenteRequest(
            CodigosAsignaturasOrigen: new[] { "FIS101", "FIS102" },
            CodigoAsignaturaDestino: "SW-FISICA",
            InstitucionOrigen: "Escuela Politécnica Nacional");

        var resOtraInst = await service.BuscarAsync(busquedaOtraInstitucion);

        // Assert (CA2 US-11: sin coincidencia -> [] array vacío, nunca aproximados ni resoluciones inventadas)
        resParcial.Should().BeEmpty();
        resOtraInst.Should().BeEmpty();
    }

    #endregion

    #region S2-P09: No Omisión de Controles Normativos con Precedente Favorable

    [Fact]
    public async Task S2_P09_Precedente_favorable_con_materia_mas_de_10_anos_DEBE_derivar_a_validacion_conocimientos()
    {
        // Arrange (CA3 US-11 y S2-P09:
        // Caso: Existe un precedente histórico favorable idéntico RES-FISEI-2025-031,
        // PERO la materia de origen fue aprobada hace 12 años (> 10 años).
        // La regla RN-03 exige que obligatoriamente se derive a VALIDACION_CONOCIMIENTOS y no se auto-apruebe).
        using var db = new SihaDbContextC(_dbOptions);
        var precedenteService = new PrecedenteService(db);

        await precedenteService.RegistrarAsync(
            resolucion: "RES-FISEI-2025-031",
            asignaturasOrigen: new[] { ("SW-ORIGEN-1", "Programación Básica") },
            asignaturaDestino: "SW-DESTINO-1",
            institucionOrigen: "UTA",
            fecha: new DateOnly(2025, 3, 12));

        var extraccionId = Guid.NewGuid();
        var extraccion = new Extraccion
        {
            Id = extraccionId,
            ExpedienteId = Guid.NewGuid(),
            CodigoAsignaturaOrigen = "SW-ORIGEN-1",
            NombreAsignaturaOrigen = "Programación Básica",
            Creditos = 4,
            Horas = 120,
            Confianza = 0.95m,
            Estado = EstadosExtraccion.Confirmada,
            ConfirmadoPor = "docente@uta.edu.ec",
            ConfirmadoUtc = DateTimeOffset.UtcNow,
            Unidades = new List<ExtraccionUnidad>
            {
                new() { Numero = 1, Nombre = "Algoritmos", Horas = 60, Contenidos = "Fundamentos" },
                new() { Numero = 2, Nombre = "Estructuras", Horas = 60, Contenidos = "Ciclos" }
            }
        };
        db.Extracciones.Add(extraccion);
        await db.SaveChangesAsync();

        var mockGrpc = new Mock<IReglasGrpcClient>();
        mockGrpc.Setup(g => g.ObtenerConfiguracionAsync("CAU-P-011", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguracionRuta
            {
                RutaNormativa = "CAU-P-011",
                ParametrosConfigurados = true,
                Parametros = new ParametrosEvaluacionMensaje { Theta = 80, Delta = 15, Tau = 75, Version = 1 }
            });

        var mockOllama = new Mock<IOllamaEmbeddingService>();
        mockOllama.Setup(o => o.GenerarEmbeddingAsync(It.Is<string>(s => s.Contains("Fundamentos") || s.Contains("Algoritmos")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 1, 0 });
        mockOllama.Setup(o => o.GenerarEmbeddingAsync(It.Is<string>(s => s.Contains("Ciclos") || s.Contains("Estructuras")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0, 1 });

        var agente = new AgenteAnalizadorService(
            db,
            new ExtraccionService(db),
            mockGrpc.Object,
            new ReglaCreditosStrategy(),
            new ReglaContenidoStrategy(),
            precedenteService,
            mockOllama.Object,
            NullLogger<AgenteAnalizadorService>.Instance);

        var parametros = new ParametrosAnalisisCompleto(
            AnosTranscurridos: 12, // > 10 AÑOS!
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011",
            CodigoAsignaturaDestino: "SW-DESTINO-1",
            InstitucionOrigen: "UTA",
            CreditosDestino: 4,
            UnidadesDestino: new[]
            {
                new UnidadDestinoEmbeddingRequest(1, "Algoritmos", 60, "Fundamentos", new float[] { 1, 0 }),
                new UnidadDestinoEmbeddingRequest(2, "Estructuras", 60, "Ciclos", new float[] { 0, 1 })
            }
        );

        // Act
        var resultado = await agente.AnalizarAsync(extraccionId, parametros);

        // Assert
        // 1. El precedente histórico EXISTE y se recupera con éxito para consulta
        resultado.Precedentes.Should().HaveCount(1);
        resultado.Precedentes.Single().Resolucion.Should().Be("RES-FISEI-2025-031");

        // 2. Sin embargo, el estado final NO se auto-homologa: se deriva a validación de conocimientos por RN-03
        resultado.EstadoFinal.Should().Be(EstadoEvaluacionFinal.RequiereValidacionConocimientos);
        resultado.AlertasGlobales.Should().Contain(a => a.Contains("RN-03") && a.Contains("validación de conocimientos"));
        resultado.Creditos.Categoria.Should().Be("VALIDACION_CONOCIMIENTOS");
    }

    [Fact]
    public async Task S2_P09_Precedente_favorable_con_tercera_matricula_NO_omite_bloqueo_duro_RN16()
    {
        // Arrange (Precedente existe pero el estudiante está en 3ra matrícula -> Bloqueo duro)
        using var db = new SihaDbContextC(_dbOptions);
        var precedenteService = new PrecedenteService(db);

        await precedenteService.RegistrarAsync(
            resolucion: "RES-FISEI-2025-031",
            asignaturasOrigen: new[] { ("MAT101", "Cálculo I") },
            asignaturaDestino: "SW-CALCULO",
            institucionOrigen: "UTA",
            fecha: new DateOnly(2025, 3, 12));

        var extraccionId = Guid.NewGuid();
        db.Extracciones.Add(new Extraccion
        {
            Id = extraccionId,
            ExpedienteId = Guid.NewGuid(),
            CodigoAsignaturaOrigen = "MAT101",
            NombreAsignaturaOrigen = "Cálculo I",
            Creditos = 4,
            Horas = 120,
            Confianza = 0.90m,
            Estado = EstadosExtraccion.Confirmada,
            ConfirmadoPor = "docente@uta.edu.ec",
            ConfirmadoUtc = DateTimeOffset.UtcNow,
            Unidades = new List<ExtraccionUnidad>
            {
                new() { Numero = 1, Nombre = "Límites", Horas = 60, Contenidos = "Derivadas" }
            }
        });
        await db.SaveChangesAsync();

        var mockGrpc = new Mock<IReglasGrpcClient>();
        mockGrpc.Setup(g => g.ObtenerConfiguracionAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            new ExtraccionService(db),
            mockGrpc.Object,
            new ReglaCreditosStrategy(),
            new ReglaContenidoStrategy(),
            precedenteService,
            mockOllama.Object,
            NullLogger<AgenteAnalizadorService>.Instance);

        var parametros = new ParametrosAnalisisCompleto(
            AnosTranscurridos: 2,
            NumeroMatriculaDestino: 3, // 3ra MATRÍCULA!
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011",
            CodigoAsignaturaDestino: "SW-CALCULO",
            InstitucionOrigen: "UTA",
            CreditosDestino: 4,
            UnidadesDestino: new[]
            {
                new UnidadDestinoEmbeddingRequest(1, "Límites", 60, "Derivadas", new float[] { 1, 0 })
            }
        );

        // Act
        var resultado = await agente.AnalizarAsync(extraccionId, parametros);

        // Assert
        resultado.Precedentes.Should().HaveCount(1, "el precedente se muestra como referencia histórica");
        resultado.EstadoFinal.Should().Be(EstadoEvaluacionFinal.BloqueoTerceraMatricula, "el bloqueo duro prevalece sobre el precedente");
        resultado.Creditos.BloqueoDuro.Should().BeTrue();
    }

    #endregion
}
