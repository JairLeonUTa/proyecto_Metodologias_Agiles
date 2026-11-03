using FluentAssertions;
using Grpc.Core;
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
/// S2-P03 (US-08): Pruebas de actualización dinámica de reglas vía gRPC y tolerancia a caídas.
/// Responsable: Paul Acurio (PO Proxy & AI Lead).
/// 
/// Escenarios evaluados:
/// 1. Modificar umbral (θ, δ, τ) dinámicamente: la siguiente evaluación del AgenteAnalizador
///    usa de inmediato el nuevo valor y su versión (CA2).
/// 2. Caída/indisponibilidad de MS A: el AgenteAnalizador no inventa reglas ni sustituye silenciosamente,
///    sino que deriva a un bloqueo seguro (ErrorReglasGrpc / PENDIENTE_REGLAS) trazado con el error (CA3).
/// 3. Resiliencia tras restablecimiento de servicio de reglas: recupera el flujo sin intervención manual.
/// </summary>
public class ReglasGrpcDinamicasCaidaTests
{
    private readonly DbContextOptions<SihaDbContextC> _dbOptions;

    public ReglasGrpcDinamicasCaidaTests()
    {
        _dbOptions = new DbContextOptionsBuilder<SihaDbContextC>()
            .UseInMemoryDatabase(databaseName: $"SihaDbC_P03_{Guid.NewGuid()}")
            .Options;
    }

    private async Task<Extraccion> CrearExtraccionConfirmadaAsync(SihaDbContextC db, Guid id)
    {
        var extraccion = new Extraccion
        {
            Id = id,
            ExpedienteId = Guid.NewGuid(),
            CodigoAsignaturaOrigen = "SW-ORIGEN-1",
            NombreAsignaturaOrigen = "Programación",
            Creditos = 4,
            Horas = 120,
            Confianza = 0.95m,
            Estado = EstadosExtraccion.Confirmada,
            ConfirmadoPor = "operador@uta.edu.ec",
            ConfirmadoUtc = DateTimeOffset.UtcNow,
            Unidades = new List<ExtraccionUnidad>
            {
                new() { Numero = 1, Nombre = "Algoritmos", Horas = 60, Contenidos = "Fundamentos" },
                new() { Numero = 2, Nombre = "Estructuras", Horas = 60, Contenidos = "Listas y pilas" }
            }
        };

        db.Extracciones.Add(extraccion);
        await db.SaveChangesAsync();
        return extraccion;
    }

    [Fact]
    public async Task Actualizacion_dinamica_de_reglas_aplica_nuevo_umbral_tau_en_siguiente_evaluacion()
    {
        // Arrange
        using var db = new SihaDbContextC(_dbOptions);
        var extraccionId = Guid.NewGuid();
        await CrearExtraccionConfirmadaAsync(db, extraccionId);

        var extraccionService = new ExtraccionService(db);
        var mockGrpc = new Mock<IReglasGrpcClient>();
        var mockOllama = new Mock<IOllamaEmbeddingService>();

        // Simular embeddings estables
        mockOllama.Setup(o => o.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 1, 0 });

        var reglaCreditos = new ReglaCreditosStrategy();
        var reglaContenido = new ReglaContenidoStrategy();
        var precedenteService = new PrecedenteService(db);

        var agente = new AgenteAnalizadorService(
            db,
            extraccionService,
            mockGrpc.Object,
            reglaCreditos,
            reglaContenido,
            precedenteService,
            mockOllama.Object,
            NullLogger<AgenteAnalizadorService>.Instance);

        var paramsAnalisis = new ParametrosAnalisisCompleto(
            AnosTranscurridos: 2,
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011",
            CodigoAsignaturaDestino: "SW-DESTINO-1",
            InstitucionOrigen: "UTA",
            CreditosDestino: 4,
            UnidadesDestino: new[]
            {
                new UnidadDestinoEmbeddingRequest(1, "Algoritmos D", 60, "Contenido D", new float[] { 1, 0 })
            }
        );

        // Paso 1: MS A configurado con τ = 75 (Versión 1)
        mockGrpc.Setup(g => g.ObtenerConfiguracionAsync("CAU-P-011", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguracionRuta
            {
                RutaNormativa = "CAU-P-011",
                ParametrosConfigurados = true,
                Parametros = new ParametrosEvaluacionMensaje { Theta = 80, Delta = 15, Tau = 75, Version = 1 }
            });

        var res1 = await agente.AnalizarAsync(extraccionId, paramsAnalisis);
        res1.Contenido.TauUsado.Should().Be(75);
        res1.DetalleTrazabilidad.Should().Contain("v1");

        // Paso 2: Actualización dinámica en MS A a τ = 85 (Versión 2)
        mockGrpc.Setup(g => g.ObtenerConfiguracionAsync("CAU-P-011", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguracionRuta
            {
                RutaNormativa = "CAU-P-011",
                ParametrosConfigurados = true,
                Parametros = new ParametrosEvaluacionMensaje { Theta = 80, Delta = 15, Tau = 85, Version = 2 }
            });

        var res2 = await agente.AnalizarAsync(extraccionId, paramsAnalisis);

        // Assert (CA2: la nueva evaluación usa de inmediato el nuevo valor y su versión)
        res2.Contenido.TauUsado.Should().Be(85);
        res2.DetalleTrazabilidad.Should().Contain("v2");
        res2.DetalleTrazabilidad.Should().Contain("τ=85");
    }

    [Fact]
    public async Task Detencion_de_servicio_reglas_provoca_bloqueo_seguro_sin_inventar_parametros()
    {
        // Arrange (S2-P03 & CA3 US-08: Si gRPC falla -> ErrorReglasGrpc con error trazado)
        using var db = new SihaDbContextC(_dbOptions);
        var extraccionId = Guid.NewGuid();
        await CrearExtraccionConfirmadaAsync(db, extraccionId);

        var extraccionService = new ExtraccionService(db);
        var mockGrpc = new Mock<IReglasGrpcClient>();
        var mockOllama = new Mock<IOllamaEmbeddingService>();

        // Simular caída de MS A (servidor gRPC apagado / inaccesible)
        mockGrpc.Setup(g => g.ObtenerConfiguracionAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ReglasGrpcException("Servidor Microservicio A inalcanzable (Unavailable)", StatusCode.Unavailable));

        var agente = new AgenteAnalizadorService(
            db,
            extraccionService,
            mockGrpc.Object,
            new ReglaCreditosStrategy(),
            new ReglaContenidoStrategy(),
            new PrecedenteService(db),
            mockOllama.Object,
            NullLogger<AgenteAnalizadorService>.Instance);

        var paramsAnalisis = new ParametrosAnalisisCompleto(
            AnosTranscurridos: 1,
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011",
            CodigoAsignaturaDestino: "SW-DESTINO-1",
            InstitucionOrigen: "UTA",
            CreditosDestino: 4,
            UnidadesDestino: Array.Empty<UnidadDestinoEmbeddingRequest>()
        );

        // Act
        var resultado = await agente.AnalizarAsync(extraccionId, paramsAnalisis);

        // Assert
        resultado.EstadoFinal.Should().Be(EstadoEvaluacionFinal.ErrorReglasGrpc);
        resultado.AlertasGlobales.Should().Contain(a => a.Contains("No se pudo obtener configuración de reglas"));
        resultado.AlertasGlobales.Should().Contain(a => a.Contains("Unavailable"));
    }

    [Fact]
    public async Task Restablecimiento_de_MS_A_permite_procesar_nuevamente_con_exito()
    {
        // Arrange
        using var db = new SihaDbContextC(_dbOptions);
        var extraccionId = Guid.NewGuid();
        await CrearExtraccionConfirmadaAsync(db, extraccionId);

        var extraccionService = new ExtraccionService(db);
        var mockGrpc = new Mock<IReglasGrpcClient>();
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

        var paramsAnalisis = new ParametrosAnalisisCompleto(
            AnosTranscurridos: 1,
            NumeroMatriculaDestino: 1,
            EsCreditoExterno: false,
            RutaNormativa: "CAU-P-011",
            CodigoAsignaturaDestino: "SW-DESTINO-1",
            InstitucionOrigen: "UTA",
            CreditosDestino: 4,
            UnidadesDestino: new[]
            {
                new UnidadDestinoEmbeddingRequest(1, "Algoritmos D", 60, "Contenido D", new float[] { 1, 0 })
            }
        );

        // 1. MS A caído
        mockGrpc.Setup(g => g.ObtenerConfiguracionAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ReglasGrpcException("MS A caído", StatusCode.Unavailable));

        var fallo = await agente.AnalizarAsync(extraccionId, paramsAnalisis);
        fallo.EstadoFinal.Should().Be(EstadoEvaluacionFinal.ErrorReglasGrpc);

        // 2. MS A restablecido
        mockGrpc.Setup(g => g.ObtenerConfiguracionAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguracionRuta
            {
                RutaNormativa = "CAU-P-011",
                ParametrosConfigurados = true,
                Parametros = new ParametrosEvaluacionMensaje { Theta = 80, Delta = 15, Tau = 75, Version = 5 }
            });

        var recuperado = await agente.AnalizarAsync(extraccionId, paramsAnalisis);

        // Assert
        recuperado.EstadoFinal.Should().Be(EstadoEvaluacionFinal.ListaParaDictamen);
        recuperado.DetalleTrazabilidad.Should().Contain("v5");
    }
}
