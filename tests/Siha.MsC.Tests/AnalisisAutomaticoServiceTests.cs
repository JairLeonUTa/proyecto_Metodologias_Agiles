using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Siha.Contracts.Events;
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
/// Homologación automática por contenidos con el certificado SIHA.
/// Embeddings deterministas: cada tema es un eje distinto, así se sabe exactamente qué cubre qué.
/// Caso guía: Física I de Software (SW105) reúne los 8 temas de Física I y Física II de Industrial.
/// </summary>
public class AnalisisAutomaticoServiceTests
{
    private static readonly string[] TemasFisicaI =
        { "Magnitudes y vectores", "Cinemática", "Dinámica", "Trabajo y energía" };
    private static readonly string[] TemasFisicaII =
        { "Fluidos", "Temperatura y calor", "Oscilaciones y ondas", "Electrostática" };
    private static readonly string[] TemasProgramacion =
        { "Introducción y entorno", "Estructuras de control", "Arreglos y cadenas" };
    private static readonly string[] TemasEstructuras = { "Listas y pilas", "Árboles", "Grafos" };

    private static readonly string[] Todos =
        TemasFisicaI.Concat(TemasFisicaII).Concat(TemasProgramacion).Concat(TemasEstructuras).ToArray();

    private static readonly DateOnly Reciente = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2);

    [Fact]
    public async Task Fisica_I_y_II_de_industrial_juntas_homologan_Fisica_I_de_software()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, ("IND105", "Física I", TemasFisicaI), ("IND205", "Física II", TemasFisicaII),
            ("IND108", "Programación Básica", TemasProgramacion));

        var r = await Servicio(db).AnalizarAsync(expediente, Solicitud(
            ("IND105", Reciente, 1), ("IND205", Reciente, 1), ("IND108", Reciente, 1)));

        var fisica = r.Propuestas.Single(p => p.CodigoDestino == "SW105");
        fisica.Origenes.Select(o => o.Codigo).Should().BeEquivalentTo("IND105", "IND205");
        fisica.Pcont.Should().Be(100m);
        fisica.AlcanzaContenido.Should().BeTrue();
        fisica.EstadoFinal.Should().Be(EstadoEvaluacionFinal.ListaParaDictamen);
        fisica.Unidades.Should().HaveCount(8).And.OnlyContain(u => u.Cubierta);
        fisica.Unidades.Single(u => u.Nombre == "Fluidos").CodigoOrigen.Should().Be("IND205");
        fisica.Unidades.Single(u => u.Nombre == "Cinemática").CodigoOrigen.Should().Be("IND105");

        r.Propuestas.Single(p => p.CodigoDestino == "SW101").Origenes.Single().Codigo.Should().Be("IND108");
        r.Propuestas.Should().NotContain(p => p.CodigoDestino == "SW201");
        r.SinCoberturaSuficiente.Should().BeEmpty();
        r.OrigenesSinAsignar.Should().BeEmpty();
    }

    [Fact]
    public async Task Solo_Fisica_I_de_industrial_no_alcanza_Fisica_I_de_software()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, ("IND105", "Física I", TemasFisicaI), ("IND108", "Programación Básica", TemasProgramacion));

        var r = await Servicio(db).AnalizarAsync(expediente, Solicitud(("IND105", Reciente, 1), ("IND108", Reciente, 1)));

        r.Propuestas.Should().ContainSingle(p => p.CodigoDestino == "SW101");
        r.Propuestas.Should().NotContain(p => p.CodigoDestino == "SW105");

        var parcial = r.SinCoberturaSuficiente.Single(p => p.CodigoDestino == "SW105");
        parcial.Pcont.Should().Be(50m);
        parcial.AlcanzaContenido.Should().BeFalse();
        parcial.EstadoFinal.Should().Be(EstadoEvaluacionFinal.ContenidoInsuficiente);
        parcial.Alertas.First().Should().Contain("cubre el 50 %").And.Contain("a criterio del docente")
            .And.Contain("Fluidos");
        parcial.Unidades.Where(u => !u.Cubierta).Select(u => u.Nombre).Should().BeEquivalentTo(TemasFisicaII);
        r.OrigenesSinAsignar.Select(o => o.Codigo).Should().Equal("IND105");
    }

    [Fact]
    public async Task Los_textos_de_pantalla_no_mencionan_los_umbrales()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, ("IND105", "Física I", TemasFisicaI));

        var r = await Servicio(db).AnalizarAsync(expediente, Solicitud(("IND105", Reciente, 1)));

        var textos = r.Alertas.Concat(r.SinCoberturaSuficiente.SelectMany(p => p.Alertas)).ToList();
        textos.Should().NotBeEmpty();
        textos.Should().NotContain(t => t.Contains('θ') || t.Contains('δ') || t.Contains('τ') || t.Contains("theta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Una_materia_aprobada_no_se_usa_para_dos_destinos_RN19()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, ("IND108", "Programación Básica", TemasProgramacion));
        var solicitud = Solicitud(("IND108", Reciente, 1)) with
        {
            AsignaturasDestino = new[]
            {
                Destino("SW101", "Fundamentos de Programación", 1, TemasProgramacion),
                Destino("TI101", "Programación I", 1, TemasProgramacion)
            }
        };

        var r = await Servicio(db).AnalizarAsync(expediente, solicitud);

        r.Propuestas.Should().ContainSingle();
        r.Propuestas.Single().Origenes.Single().Codigo.Should().Be("IND108");
    }

    [Fact]
    public async Task Materia_aprobada_hace_mas_de_10_anos_requiere_validacion_de_conocimientos()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, ("IND108", "Programación Básica", TemasProgramacion));
        var antigua = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-11);

        var r = await Servicio(db).AnalizarAsync(expediente, Solicitud(("IND108", antigua, 1)));

        var p = r.Propuestas.Single();
        p.Origenes.Single().AnosTranscurridos.Should().Be(11);
        p.EstadoFinal.Should().Be(EstadoEvaluacionFinal.RequiereValidacionConocimientos);
    }

    [Fact]
    public async Task Tercera_matricula_bloquea_la_propuesta()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, ("IND108", "Programación Básica", TemasProgramacion));

        var r = await Servicio(db).AnalizarAsync(expediente, Solicitud(("IND108", Reciente, 3)));

        r.Propuestas.Single().EstadoFinal.Should().Be(EstadoEvaluacionFinal.BloqueoTerceraMatricula);
    }

    [Fact]
    public async Task Materias_sin_confirmar_impiden_el_analisis()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, ("IND108", "Programación Básica", TemasProgramacion));
        (await db.Extracciones.SingleAsync()).Estado = EstadosExtraccion.PendienteConfirmacion;
        await db.SaveChangesAsync();

        var accion = () => Servicio(db).AnalizarAsync(expediente, Solicitud(("IND108", Reciente, 1)));

        (await accion.Should().ThrowAsync<AnalisisAutomaticoException>()).Which.CodigoHttp.Should().Be(409);
    }

    [Fact]
    public async Task Sin_servicio_de_reglas_no_se_inventan_umbrales()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, ("IND108", "Programación Básica", TemasProgramacion));
        var grpc = new Mock<IReglasGrpcClient>();
        grpc.Setup(g => g.ObtenerConfiguracionAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ReglasGrpcException("caído"));

        var accion = () => Servicio(db, grpc.Object).AnalizarAsync(expediente, Solicitud(("IND108", Reciente, 1)));

        (await accion.Should().ThrowAsync<AnalisisAutomaticoException>()).Which.CodigoHttp.Should().Be(503);
    }

    [Fact]
    public async Task Umbral_de_contenido_lo_decide_MS_A_por_gRPC()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, ("IND105", "Física I", TemasFisicaI));

        // Si el administrador bajara θ a 50, Física I sola sí alcanzaría SW105.
        var r = await Servicio(db, Reglas(theta: 50)).AnalizarAsync(expediente, Solicitud(("IND105", Reciente, 1)));

        r.Propuestas.Should().ContainSingle(p => p.CodigoDestino == "SW105" && p.Pcont == 50m);
    }

    [Fact]
    public async Task El_certificado_SIHA_crea_una_extraccion_confirmada_por_materia()
    {
        using var temp = new Soporte.DirectorioTemporal();
        var pdf = File.ReadAllBytes(RutaEjemplo("SIHA-IND-SW-completo.pdf"));
        temp.CrearArchivo("cert.pdf", pdf);
        await using var db = CrearDb();
        var opciones = Options.Create(new AlmacenOptions { RutaAlmacenamiento = temp.Ruta });
        var servicio = new ProcesarSolicitudCreadaService(db, new AlmacenDocumentos(opciones),
            new ExtractorPdfService(opciones, NullLogger<ExtractorPdfService>.Instance),
            NullLogger<ProcesarSolicitudCreadaService>.Instance,
            new LectorCertificadosAlmacen(opciones));
        var documento = new DocumentoReferencia
        {
            DocumentoId = Guid.NewGuid(),
            RutaAlmacenamiento = "cert.pdf",
            HashSha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant(),
            TamanoBytes = pdf.Length
        };
        var evento = new SolicitudCreadaEvent
        {
            ExpedienteId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            TipoTramite = "INTERNO",
            RutaNormativa = "CAU-P-011",
            DocumentoIds = new[] { documento.DocumentoId },
            Documentos = new[] { documento },
            AsignaturasOrigen = new[] { "IND105", "IND108", "IND205" }
        };

        await servicio.ProcesarAsync(evento, Guid.NewGuid());

        var extracciones = await db.Extracciones.Include(e => e.Unidades).OrderBy(e => e.CodigoAsignaturaOrigen).ToListAsync();
        extracciones.Select(e => e.CodigoAsignaturaOrigen).Should().Equal("IND105", "IND108", "IND205");
        extracciones.Should().OnlyContain(e => e.Estado == EstadosExtraccion.Confirmada && e.Confianza == 1.00m
                                               && e.ConfirmadoPor == "CERTIFICADO_SIHA" && e.DocumentoId == documento.DocumentoId);
        extracciones[0].Creditos.Should().Be(4);
        extracciones[0].Horas.Should().Be(192);
        extracciones[0].Unidades.Should().HaveCount(4);
        extracciones[2].Unidades.Select(u => u.Nombre).Should().Contain("Mecánica de fluidos");
        extracciones[0].Nota.Should().Be(8.60m);
        extracciones[0].FechaAprobacion.Should().Be(new DateOnly(2024, 2, 16));
        var contexto = await db.AnalisisExpedientes.SingleAsync();
        contexto.CarreraDestinoCodigo.Should().Be("SW");
        contexto.Revisor1Correo.Should().Be("hnaranjo@uta.edu.ec");
        contexto.Revisor2Correo.Should().Be("mvaca@uta.edu.ec");
        contexto.Estado.Should().Be(EstadosAnalisis.Pendiente);
    }

    // ---------- solicitud completa: agente + dos docentes ----------

    private sealed class CatalogoFalso(IReadOnlyList<DestinoAnalisisRequest> malla) : ICatalogoGrpcCliente
    {
        public Task<IReadOnlyList<DestinoAnalisisRequest>> ObtenerMallaAsync(string carreraCodigo, CancellationToken cancellationToken = default)
            => Task.FromResult(malla);
    }

    private static async Task<Guid> SembrarSolicitud(SihaDbContextC db, params (string Codigo, string Nombre, string[] Temas)[] materias)
    {
        var expediente = await Sembrar(db, materias);
        db.AnalisisExpedientes.Add(new AnalisisExpediente
        {
            ExpedienteId = expediente, CodigoExpediente = "EXP-1", RutaNormativa = "CAU-P-011", TipoTramite = "INTERNO",
            InstitucionOrigen = "UTA", CarreraOrigenCodigo = "IND", CarreraDestinoCodigo = "SW",
            Revisor1Nombre = "Ing. Naranjo", Revisor1Correo = "hnaranjo@uta.edu.ec",
            Revisor2Nombre = "Ing. Vaca", Revisor2Correo = "mvaca@uta.edu.ec"
        });
        await db.SaveChangesAsync();
        return expediente;
    }

    private static AnalisisAutomaticoService ServicioConCatalogo(SihaDbContextC db, IReadOnlyList<DestinoAnalisisRequest>? malla = null)
    {
        var precedentes = new Mock<IPrecedenteService>();
        precedentes.Setup(p => p.BuscarAsync(It.IsAny<BuscarPrecedenteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PrecedenteResponse>());
        return new AnalisisAutomaticoService(db, Reglas(), new ReglaCreditosStrategy(), new ReglaContenidoStrategy(),
            precedentes.Object, Embeddings(), NullLogger<AnalisisAutomaticoService>.Instance, null,
            new CatalogoFalso(malla ?? Solicitud().AsignaturasDestino.ToList()));
    }

    private static DictamenRequest Decidir(params (string Codigo, bool Aprueba)[] d)
        => new(d.Select(x => new DecisionMateriaRequest(x.Codigo, x.Aprueba, null)).ToList(), "Revisado");

    [Fact]
    public async Task El_agente_trae_la_malla_por_gRPC_analiza_y_guarda_la_estimacion()
    {
        await using var db = CrearDb();
        var expediente = await SembrarSolicitud(db, ("IND105", "Física I", TemasFisicaI), ("IND108", "Programación Básica", TemasProgramacion));

        await ServicioConCatalogo(db).AnalizarYGuardarAsync(expediente);

        var solicitudes = new SolicitudesAnalisisService(db);
        var vista = (await solicitudes.ObtenerAsync(expediente, "admin@uta.edu.ec", esDocente: false))!;
        vista.Estado.Should().Be(EstadosAnalisis.Analizado);
        vista.Resultado!.Propuestas.Should().ContainSingle(p => p.CodigoDestino == "SW101");
        vista.ResultadoFinal.Should().HaveCount(2);
        vista.ResultadoFinal.Single(m => m.CodigoDestino == "SW101").Estimacion.Should().Be(SolicitudesAnalisisService.Cumple);
        vista.ResultadoFinal.Single(m => m.CodigoDestino == "SW105").Estimacion.Should().Be(SolicitudesAnalisisService.CriterioDocente);
        vista.EstadoRevision.Should().Be("PENDIENTE");
        vista.PuedeResponder.Should().BeFalse("el administrador no es revisor");
    }

    [Fact]
    public async Task Solo_los_dos_docentes_asignados_ven_y_responden_la_solicitud()
    {
        await using var db = CrearDb();
        var expediente = await SembrarSolicitud(db, ("IND108", "Programación Básica", TemasProgramacion));
        await ServicioConCatalogo(db).AnalizarYGuardarAsync(expediente);
        var solicitudes = new SolicitudesAnalisisService(db);

        (await solicitudes.ObtenerAsync(expediente, "otro@uta.edu.ec", esDocente: true)).Should().BeNull();
        (await solicitudes.ObtenerAsync(expediente, "mvaca@uta.edu.ec", esDocente: true))!.PuedeResponder.Should().BeTrue();
        var (_, error, codigo) = await solicitudes.ResponderAsync(expediente, "otro@uta.edu.ec", "Otro", Decidir(("SW101", true)));
        codigo.Should().Be(404);
        error.Should().Contain("no está asignada");
    }

    [Fact]
    public async Task Con_dos_aprobaciones_queda_homologada_y_si_discrepan_sin_acuerdo()
    {
        await using var db = CrearDb();
        var expediente = await SembrarSolicitud(db, ("IND105", "Física I", TemasFisicaI), ("IND108", "Programación Básica", TemasProgramacion));
        await ServicioConCatalogo(db).AnalizarYGuardarAsync(expediente);
        var solicitudes = new SolicitudesAnalisisService(db);

        var uno = await solicitudes.ResponderAsync(expediente, "hnaranjo@uta.edu.ec", "Naranjo", Decidir(("SW101", true), ("SW105", false)));
        uno.Respuesta!.EstadoRevision.Should().Be("EN_REVISION");
        var dos = await solicitudes.ResponderAsync(expediente, "mvaca@uta.edu.ec", "Vaca", Decidir(("SW101", true), ("SW105", true)));

        var final = dos.Respuesta!;
        final.EstadoRevision.Should().Be("RESUELTA");
        final.ResultadoFinal.Single(m => m.CodigoDestino == "SW101").Resultado.Should().Be("HOMOLOGADA");
        final.ResultadoFinal.Single(m => m.CodigoDestino == "SW105").Resultado.Should().Be("SIN_ACUERDO");
        final.Revisores.Should().OnlyContain(r => r.Respondio);
    }

    [Fact]
    public async Task El_docente_debe_decidir_todas_las_materias()
    {
        await using var db = CrearDb();
        var expediente = await SembrarSolicitud(db, ("IND105", "Física I", TemasFisicaI), ("IND108", "Programación Básica", TemasProgramacion));
        await ServicioConCatalogo(db).AnalizarYGuardarAsync(expediente);

        var (_, error, codigo) = await new SolicitudesAnalisisService(db)
            .ResponderAsync(expediente, "hnaranjo@uta.edu.ec", "Naranjo", Decidir(("SW101", true)));

        codigo.Should().Be(400);
        error.Should().Contain("SW105");
    }

    [Fact]
    public async Task Si_la_carrera_de_destino_no_tiene_malla_queda_en_error_con_el_motivo()
    {
        await using var db = CrearDb();
        var expediente = await SembrarSolicitud(db, ("IND108", "Programación Básica", TemasProgramacion));

        await ServicioConCatalogo(db, Array.Empty<DestinoAnalisisRequest>()).AnalizarYGuardarAsync(expediente);

        var contexto = await db.AnalisisExpedientes.SingleAsync();
        contexto.Estado.Should().Be(EstadosAnalisis.Error);
        contexto.Mensaje.Should().Contain("no tiene una malla");
    }

    // ---------- soporte ----------

    private static string RutaEjemplo(string nombre)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "documentos-prueba")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "documentos-prueba", "certificados", nombre);
    }

    private static SihaDbContextC CrearDb()
        => new(new DbContextOptionsBuilder<SihaDbContextC>()
            .UseInMemoryDatabase($"siha-msc-automatico-{Guid.NewGuid():N}").Options);

    private static async Task<Guid> Sembrar(SihaDbContextC db, params (string Codigo, string Nombre, string[] Temas)[] materias)
    {
        var expediente = Guid.NewGuid();
        foreach (var (codigo, nombre, temas) in materias)
        {
            db.Extracciones.Add(new Extraccion
            {
                ExpedienteId = expediente,
                CodigoAsignaturaOrigen = codigo,
                NombreAsignaturaOrigen = nombre,
                Creditos = 4,
                Horas = 192,
                Confianza = 1m,
                Estado = EstadosExtraccion.Confirmada,
                ConfirmadoPor = "CERTIFICADO_SIHA",
                ConfirmadoUtc = DateTimeOffset.UtcNow,
                Unidades = temas.Select((t, i) => new ExtraccionUnidad
                {
                    Numero = i + 1, Nombre = t, Horas = 192 / temas.Length, Contenidos = t
                }).ToList()
            });
        }

        await db.SaveChangesAsync();
        return expediente;
    }

    private static AnalisisAutomaticoRequest Solicitud(params (string Codigo, DateOnly Fecha, int Matricula)[] origen) => new(
        "CAU-P-011",
        "INTERNO",
        "Universidad Técnica de Ambato",
        origen.Select(o => new OrigenAnalisisRequest(o.Codigo, o.Fecha, o.Matricula)).ToList(),
        new[]
        {
            Destino("SW101", "Fundamentos de Programación", 1, TemasProgramacion),
            Destino("SW105", "Física I", 1, TemasFisicaI.Concat(TemasFisicaII).ToArray()),
            Destino("SW201", "Estructuras de Datos", 2, TemasEstructuras)
        });

    private static DestinoAnalisisRequest Destino(string codigo, string nombre, int semestre, string[] temas) => new(
        codigo, nombre, 4, semestre,
        temas.Select((t, i) => new UnidadCatalogoRequest(i + 1, t, 24, t)).ToList());

    private static IReglasGrpcClient Reglas(double theta = 80, double tau = 75)
    {
        var grpc = new Mock<IReglasGrpcClient>();
        grpc.Setup(g => g.ObtenerConfiguracionAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguracionRuta
            {
                RutaNormativa = "CAU-P-011",
                ParametrosConfigurados = true,
                Parametros = new ParametrosEvaluacionMensaje { Theta = theta, Delta = 15, Tau = tau, Version = 1 }
            });
        return grpc.Object;
    }

    /// <summary>Cada tema es un eje: mismo tema → coseno 1; temas distintos → coseno 0.</summary>
    private static IOllamaEmbeddingService Embeddings()
    {
        var ollama = new Mock<IOllamaEmbeddingService>();
        ollama.Setup(o => o.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string texto, CancellationToken _) =>
            {
                var v = new float[Todos.Length];
                v[Array.IndexOf(Todos, texto)] = 1f;
                return v;
            });
        return ollama.Object;
    }

    private static AnalisisAutomaticoService Servicio(SihaDbContextC db, IReglasGrpcClient? reglas = null)
    {
        var precedentes = new Mock<IPrecedenteService>();
        precedentes.Setup(p => p.BuscarAsync(It.IsAny<BuscarPrecedenteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PrecedenteResponse>());
        return new AnalisisAutomaticoService(db, reglas ?? Reglas(), new ReglaCreditosStrategy(), new ReglaContenidoStrategy(),
            precedentes.Object, Embeddings(), NullLogger<AnalisisAutomaticoService>.Instance);
    }
}
