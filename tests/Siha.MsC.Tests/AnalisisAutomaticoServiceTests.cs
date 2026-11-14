using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Siha.Certificados;
using Siha.Contracts.Events;
using Siha.MsA.Api.Grpc;
using Siha.MsC.Api.Contracts.Dtos;
using Siha.MsC.Api.Data;
using Siha.MsC.Api.Domain.Entities;
using Siha.MsC.Api.Grpc;
using Siha.MsC.Api.Options;
using Siha.MsC.Api.Services;

namespace Siha.MsC.Tests;

/// <summary>
/// Agente de homologación con certificado SIHA: compara cada materia pedida con los sílabos oficiales
/// (contenidos, profundidad y carga horaria al 80 %) y lo explica. Embeddings deterministas: cada tema
/// es un eje, así se sabe exactamente qué unidad cubre a cuál.
/// </summary>
public class AnalisisAutomaticoServiceTests
{
    private static readonly List<string> Temas = new();

    private static UnidadCatalogoRequest U(int n, string tema, int horas) => new(n, tema, horas, $"Temas de {tema}");

    private static DestinoAnalisisRequest A(string codigo, string nombre, int nivel, int creditos, params UnidadCatalogoRequest[] u)
        => new(codigo, nombre, creditos, nivel, u);

    // Tecnologías de la Información (origen)
    private static readonly List<DestinoAnalisisRequest> MallaTi = new()
    {
        A("TI-FP", "FUNDAMENTOS DE PROGRAMACION", 1, 4, U(1, "Algoritmos", 48), U(2, "Control", 48), U(3, "Funciones", 48), U(4, "Arreglos", 48)),
        A("TI-FIS", "FISICA", 1, 3, U(1, "Estatica", 36), U(2, "Dinamica", 36), U(3, "Energia", 36), U(4, "Electricidad", 36)),
        A("TI-POO", "PROGRAMACION ORIENTADA A OBJETOS", 2, 3, U(1, "Clases", 36), U(2, "Herencia", 36), U(3, "Polimorfismo", 36), U(4, "Excepciones", 36)),
        A("TI-F1", "FISICA I", 1, 3, U(1, "Estatica", 72), U(2, "Dinamica", 72)),
        A("TI-F2", "FISICA II", 2, 3, U(1, "Energia", 72), U(2, "Electricidad", 72)),
        // Ejemplo del docente: mismos temas y horas que Física de Software, pero 2 créditos frente a 3.
        A("TI-FB", "FISICA BASICA", 2, 2, U(1, "Estatica", 36), U(2, "Dinamica", 36), U(3, "Energia", 36), U(4, "Electricidad", 36)),
    };

    // Software (destino)
    private static readonly List<DestinoAnalisisRequest> MallaSw = new()
    {
        A("SW-ALG", "ALGORITMOS Y LOGICA DE PROGRAMACION", 1, 4, U(1, "Algoritmos", 48), U(2, "Control", 48), U(3, "Funciones", 48), U(4, "Arreglos", 48)),
        A("SW-FIS", "FISICA", 1, 3, U(1, "Estatica", 36), U(2, "Dinamica", 36), U(3, "Energia", 36), U(4, "Electricidad", 36)),
        A("SW-POO", "PROGRAMACION ORIENTADA A OBJETOS", 2, 4, U(1, "Clases", 48), U(2, "Herencia", 48), U(3, "Polimorfismo", 48), U(4, "Excepciones", 48)),
    };

    private static MateriaCursada M(string nombre, int nivel, decimal nota = 8.5m, bool aprobada = true)
        => new(nombre, nivel, nivel == 1 ? "Abr-Sep 2025" : "Oct 2025-Mar 2026", nota, aprobada);

    private static CertificadoSiha Certificado(params (string[] Origen, string Destino)[] pedidas) => new(
        "SIHA-1", new DateOnly(2026, 10, 30), "Secretaría", "Ana", "1804567890", "INTERNO", "CAU-P-011", "UTA",
        "Tecnologías de la Información", "Software",
        new[]
        {
            M("Fundamentos de Programación", 1), M("Física", 1), M("Física I", 1),
            M("Programación Orientada a Objetos", 2), M("Física II", 2), M("Física Básica", 2), M("Cálculo Integral", 2, 6.2m, false)
        },
        pedidas.Select((p, i) => new SolicitudHomologacion(i + 1, p.Origen, p.Destino)).ToList(),
        new[] { new RevisorCertificado("Ing. Naranjo", "hnaranjo@uta.edu.ec"), new RevisorCertificado("Ing. Vaca", "mvaca@uta.edu.ec") });

    private static async Task<Guid> Sembrar(SihaDbContextC db, CertificadoSiha c, IReadOnlyList<SilaboExterno>? silabos = null)
    {
        var expediente = Guid.NewGuid();
        db.AnalisisExpedientes.Add(new AnalisisExpediente
        {
            ExpedienteId = expediente, CodigoExpediente = "EXP-1", RutaNormativa = c.RutaNormativa, TipoTramite = c.TipoTramite,
            InstitucionOrigen = c.InstitucionOrigen, CarreraOrigenCodigo = c.TipoTramite == "EXTERNO" ? "EXTERNA" : "TI", CarreraDestinoCodigo = "SW",
            CarreraOrigenNombre = c.CarreraOrigen, CarreraDestinoNombre = "Software",
            CertificadoJson = JsonSerializer.Serialize(c),
            SilabosExternosJson = silabos is null ? null : JsonSerializer.Serialize(silabos),
            Revisor1Nombre = "Ing. Naranjo", Revisor1Correo = "hnaranjo@uta.edu.ec",
            Revisor2Nombre = "Ing. Vaca", Revisor2Correo = "mvaca@uta.edu.ec"
        });
        await db.SaveChangesAsync();
        return expediente;
    }

    private sealed class CatalogoFalso(IReadOnlyList<DestinoAnalisisRequest> sw, IReadOnlyList<DestinoAnalisisRequest> ti) : ICatalogoGrpcCliente
    {
        public Task<IReadOnlyList<DestinoAnalisisRequest>> ObtenerMallaAsync(string carrera, CancellationToken cancellationToken = default)
            => Task.FromResult(carrera == "SW" ? sw : carrera == "TI" ? ti : Array.Empty<DestinoAnalisisRequest>());
    }

    /// <summary>"Estatica. Temas de Estatica" → eje propio; mismo tema → coseno 1, distinto → 0.</summary>
    private static IOllamaEmbeddingService Embeddings()
    {
        var ollama = new Mock<IOllamaEmbeddingService>();
        ollama.Setup(o => o.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string texto, CancellationToken _) =>
            {
                var tema = texto.Split('.')[0];
                lock (Temas)
                {
                    if (!Temas.Contains(tema)) Temas.Add(tema);
                    var v = new float[64];
                    v[Temas.IndexOf(tema)] = 1f;
                    return v;
                }
            });
        return ollama.Object;
    }

    private static IReglasGrpcClient Reglas(double theta = 80)
    {
        var grpc = new Mock<IReglasGrpcClient>();
        grpc.Setup(g => g.ObtenerConfiguracionAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfiguracionRuta
            {
                RutaNormativa = "CAU-P-011",
                ParametrosConfigurados = true,
                Parametros = new ParametrosEvaluacionMensaje { Theta = theta, Delta = 15, Tau = 75, Version = 1 }
            });
        return grpc.Object;
    }

    private static AnalisisAutomaticoService Agente(SihaDbContextC db, IReadOnlyList<DestinoAnalisisRequest>? sw = null, double theta = 80)
    {
        var precedentes = new Mock<IPrecedenteService>();
        precedentes.Setup(p => p.BuscarAsync(It.IsAny<BuscarPrecedenteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PrecedenteResponse>());
        return new AnalisisAutomaticoService(db, Reglas(theta), precedentes.Object, Embeddings(),
            NullLogger<AnalisisAutomaticoService>.Instance, null, new CatalogoFalso(sw ?? MallaSw, MallaTi));
    }

    private static SihaDbContextC CrearDb()
        => new(new DbContextOptionsBuilder<SihaDbContextC>().UseInMemoryDatabase($"siha-agente-{Guid.NewGuid():N}").Options);

    private static async Task<AnalisisAutomaticoResponse> Analizar(
        SihaDbContextC db, CertificadoSiha c, IReadOnlyList<DestinoAnalisisRequest>? sw = null, IReadOnlyList<SilaboExterno>? silabos = null)
    {
        var expediente = await Sembrar(db, c, silabos);
        await Agente(db, sw).AnalizarYGuardarAsync(expediente);
        var contexto = await db.AnalisisExpedientes.SingleAsync();
        contexto.Mensaje.Should().BeNull();
        return JsonSerializer.Deserialize<AnalisisAutomaticoResponse>(contexto.ResultadoJson!, AnalisisAutomaticoService.Json)!;
    }

    [Fact]
    public async Task Materia_equivalente_cumple_y_explica_cada_unidad()
    {
        await using var db = CrearDb();
        var r = await Analizar(db, Certificado((new[] { "Fundamentos de Programación" }, "Algoritmos y Lógica de Programación")));

        var e = r.Evaluaciones.Single();
        e.CodigoDestino.Should().Be("SW-ALG");
        e.Contenidos.Should().Be(100m);
        e.Profundidad.Should().Be(100m);
        e.CargaHoraria.Should().Be(100m);
        e.PorcentajeCreditos.Should().Be(100m);
        e.CreditosOrigen.Should().Be(4);
        e.Estimacion.Should().Be(AnalisisAutomaticoService.Cumple);
        e.Explicacion.Should().Contain(x => x.StartsWith("✔ Unidad 2") && x.Contains("«Fundamentos de Programación»"));
        e.Explicacion.Should().Contain("Créditos: 4 crédito(s) aprobado(s) frente a 4 de la materia de destino = 100 %.");
        e.Explicacion.Should().Contain(x => x.StartsWith("Carga horaria (dato informativo, no decide): 192 horas aprobadas frente a 192"));
        e.Conclusion.Should().Contain("Cumple el 80 % en contenidos y en créditos").And.Contain("se puede homologar");
        e.MateriasOrigen.Single().PeriodoAcademico.Should().Be("Abr-Sep 2025");
    }

    [Fact]
    public async Task Ejemplo_del_docente_2_creditos_frente_a_3_no_se_homologa_aunque_los_temas_coincidan()
    {
        await using var db = CrearDb();
        var r = await Analizar(db, Certificado((new[] { "Física Básica" }, "Física")));

        var e = r.Evaluaciones.Single();
        e.Contenidos.Should().Be(100m, "cubre todos los temas");
        e.CargaHoraria.Should().Be(100m, "tiene las mismas horas");
        e.PorcentajeCreditos.Should().Be(66.67m, "2 créditos frente a 3");
        e.Estimacion.Should().Be(AnalisisAutomaticoService.NoCumple);
        e.Conclusion.Should().StartWith("No se puede homologar").And.Contain("créditos (66,67 %)");
        e.Conclusion.Should().NotContain("contenidos (");
    }

    [Fact]
    public async Task Pocos_creditos_no_se_homologa_aunque_los_temas_coincidan()
    {
        await using var db = CrearDb();
        var r = await Analizar(db, Certificado((new[] { "Programación Orientada a Objetos" }, "Programación Orientada a Objetos")));

        var e = r.Evaluaciones.Single();
        e.Contenidos.Should().Be(100m);
        e.PorcentajeCreditos.Should().Be(75m, "3 créditos aprobados frente a 4");
        e.CargaHoraria.Should().Be(75m, "dato informativo: 144 horas frente a 192");
        e.Profundidad.Should().Be(75m, "dato informativo: cada tema tiene 36 horas frente a 48");
        e.Estimacion.Should().Be(AnalisisAutomaticoService.NoCumple);
        e.Conclusion.Should().Contain("créditos (75 %)").And.Contain("exige al menos el 80 % en contenidos y en créditos");
    }

    [Fact]
    public async Task Profundidad_y_carga_horaria_solo_informan_no_deciden()
    {
        await using var db = CrearDb();
        // Destino con más horas por unidad (64 h) pero los mismos 4 créditos que lo aprobado.
        var sw = MallaSw.Where(a => a.Codigo != "SW-ALG")
            .Append(A("SW-ALG", "ALGORITMOS Y LOGICA DE PROGRAMACION", 1, 4, U(1, "Algoritmos", 64), U(2, "Control", 64), U(3, "Funciones", 64), U(4, "Arreglos", 64)))
            .ToList();

        var r = await Analizar(db, Certificado((new[] { "Fundamentos de Programación" }, "Algoritmos y Lógica de Programación")), sw);

        var e = r.Evaluaciones.Single();
        e.Profundidad.Should().Be(75m);
        e.CargaHoraria.Should().Be(75m);
        e.PorcentajeCreditos.Should().Be(100m);
        e.Estimacion.Should().Be(AnalisisAutomaticoService.Cumple, "contenidos y créditos llegan al 80 %");
    }

    [Fact]
    public async Task Sin_creditos_en_el_silabo_se_comparan_las_horas()
    {
        await using var db = CrearDb();
        var sinCreditos = new SilaboExterno("Física", "Física", null, 96,
            new[] { ("Estatica", 24), ("Dinamica", 24), ("Energia", 24), ("Electricidad", 24) }
                .Select((u, i) => new UnidadSilabo(i + 1, u.Item1, u.Item2, $"Temas de {u.Item1}")).ToList());

        var r = await Analizar(db, CertificadoExterno((new[] { "Física" }, "Física")), silabos: new[] { sinCreditos });

        var e = r.Evaluaciones.Single();
        e.CreditosPorHoras.Should().BeTrue();
        e.PorcentajeCreditos.Should().Be(66.67m, "96 horas frente a 144");
        e.Estimacion.Should().Be(AnalisisAutomaticoService.NoCumple);
        e.Explicacion.Should().Contain(x => x.StartsWith("Créditos: no se conocen los créditos") && x.Contains("se comparan las horas"));
    }

    [Fact]
    public async Task Materias_que_no_tienen_que_ver_no_se_parecen()
    {
        await using var db = CrearDb();
        var r = await Analizar(db, Certificado((new[] { "Física" }, "Algoritmos y Lógica de Programación")));

        var e = r.Evaluaciones.Single();
        e.Contenidos.Should().Be(0m);
        e.Unidades.Should().OnlyContain(u => !u.Cubierta);
        e.Explicacion.Should().Contain(x => x.StartsWith("✘ Unidad 1") && x.Contains("no aparece en lo aprobado"));
        e.Estimacion.Should().Be(AnalisisAutomaticoService.NoCumple);
        e.Conclusion.Should().Contain("contenidos (0 %)");
    }

    [Fact]
    public async Task Fisica_I_y_II_juntas_cubren_Fisica_de_software()
    {
        await using var db = CrearDb();
        var r = await Analizar(db, Certificado((new[] { "Física I", "Física II" }, "Física")));

        var e = r.Evaluaciones.Single();
        e.MateriasOrigen.Select(o => o.Nombre).Should().Equal("Física I", "Física II");
        e.Contenidos.Should().Be(100m);
        e.Unidades.Single(u => u.Numero == 4).MateriaOrigen.Should().Be("Física II");
        e.Estimacion.Should().Be(AnalisisAutomaticoService.Cumple);
    }

    [Fact]
    public async Task Requisitos_del_instructivo_se_explican()
    {
        await using var db = CrearDb();
        var r = await Analizar(db, Certificado(
            (new[] { "Fundamentos de Programación" }, "Algoritmos y Lógica de Programación"),
            (new[] { "Programación Orientada a Objetos" }, "Programación Orientada a Objetos")));

        r.Requisitos.Should().Contain(q => q.Regla.StartsWith("Aprobó al menos el primer período") && q.Cumple);
        r.Requisitos.Should().Contain(q => q.Regla == "Solo se piden materias aprobadas" && q.Explicacion.Contains("Cálculo Integral"));
        r.Requisitos.Should().Contain(q => q.Regla.StartsWith("Tiene al menos una materia") && q.Cumple && q.Explicacion.Contains("1 de 2"));
        r.Resumen.Should().Contain("1 cumple(n) el 80 % en contenidos y créditos").And.Contain("1 no se puede(n) homologar");
        r.Requisitos.Should().Contain(q => q.Regla == "Equivalencia mínima del 80 % en contenidos y en créditos" && !q.Cumple
            && q.Explicacion.Contains("no se puede homologar (contenidos 100 %, créditos 75 %)"));
        string.Join(" ", r.Requisitos.Select(q => q.Explicacion)).Should().NotContainAny("θ", "δ", "τ");
    }

    [Fact]
    public async Task Materia_de_destino_que_no_existe_en_la_malla_queda_a_criterio_del_docente()
    {
        await using var db = CrearDb();
        var r = await Analizar(db, Certificado((new[] { "Física" }, "Mecánica Cuántica")));

        var e = r.Evaluaciones.Single();
        e.Estimacion.Should().Be(AnalisisAutomaticoService.CriterioDocente);
        e.Explicacion.Should().Contain(x => x.Contains("no existe en la malla vigente de Software"));
    }

    [Fact]
    public async Task Materia_sin_unidades_en_el_catalogo_se_trata_como_sin_silabo()
    {
        await using var db = CrearDb();
        var sw = MallaSw.Append(A("SW-ETI", "ETICA PROFESIONAL", 1, 2)).ToList();
        var r = await Analizar(db, Certificado(
            (new[] { "Fundamentos de Programación" }, "Algoritmos y Lógica de Programación"),
            (new[] { "Física" }, "Ética Profesional")), sw);

        var e = r.Evaluaciones.Single(x => x.MateriaDestino == "Ética Profesional");
        e.SinSilabo.Should().BeTrue();
        e.Unidades.Should().BeEmpty();
        e.Explicacion.Should().Contain(x => x.Contains("no tiene unidades publicadas"));
        e.Conclusion.Should().StartWith("Sin sílabo para comparar");

        // No cuenta como que falló el 80 %: no se pudo medir.
        r.Requisitos.Single(q => q.Regla.StartsWith("Equivalencia mínima")).Cumple.Should().BeTrue();
        r.Requisitos.Single(q => q.Regla.StartsWith("Equivalencia mínima")).Explicacion.Should().Contain("sin sílabo para comparar");
        r.Resumen.Should().Contain("1 cumple(n)").And.Contain("1 no tiene(n) sílabo para comparar").And.NotContain("a criterio");
    }

    [Fact]
    public async Task Movilidad_externa_sin_silabos_no_muestra_porcentajes()
    {
        await using var db = CrearDb();
        var c = Certificado((new[] { "Física" }, "Física")) with
        {
            TipoTramite = "EXTERNO", RutaNormativa = "CAU-P-122", InstitucionOrigen = "Escuela Politécnica Nacional",
            CarreraOrigen = "Ingeniería en Computación"
        };
        var r = await Analizar(db, c);

        var e = r.Evaluaciones.Single();
        e.SinSilabo.Should().BeTrue();
        e.Estimacion.Should().Be(AnalisisAutomaticoService.CriterioDocente);
        r.Requisitos.Single(q => q.Regla.StartsWith("Equivalencia mínima")).Cumple.Should().BeTrue();
        r.Requisitos.Single(q => q.Regla.StartsWith("Tiene al menos una materia")).Explicacion.Should().Contain("no tiene sílabos para comparar");
        r.Resumen.Should().Be("Se analizaron 1 materia(s) pedida(s): 1 no tiene(n) sílabo para comparar y la(s) deciden los docentes.");
        string.Join(" ", r.Requisitos.Select(q => q.Explicacion)).Should().NotContain("contenidos 0 %");
    }

    private static CertificadoSiha CertificadoExterno(params (string[] Origen, string Destino)[] pedidas)
        => Certificado(pedidas) with
        {
            TipoTramite = "EXTERNO", RutaNormativa = "CAU-P-122",
            InstitucionOrigen = "Escuela Politécnica Nacional", CarreraOrigen = "Ingeniería en Computación"
        };

    private static SilaboExterno Silabo(string materia, params (string Tema, int Horas)[] unidades)
        => new(materia, materia, 3, unidades.Sum(u => u.Horas),
            unidades.Select((u, i) => new UnidadSilabo(i + 1, u.Tema, u.Horas, $"Temas de {u.Tema}")).ToList());

    [Fact]
    public async Task Movilidad_externa_con_silabo_adjunto_se_compara_con_porcentajes()
    {
        await using var db = CrearDb();
        var r = await Analizar(db, CertificadoExterno((new[] { "Física" }, "Física")),
            silabos: new[] { Silabo("Física", ("Estatica", 36), ("Dinamica", 36), ("Energia", 36), ("Electricidad", 36)) });

        var e = r.Evaluaciones.Single();
        e.SinSilabo.Should().BeFalse("el sílabo adjuntado reemplaza a la malla de origen");
        e.Contenidos.Should().Be(100m);
        e.Profundidad.Should().Be(100m);
        e.CargaHoraria.Should().Be(100m);
        e.Estimacion.Should().Be(AnalisisAutomaticoService.Cumple);
        e.MateriasOrigen.Single().EncontradaEnMalla.Should().BeTrue();
        e.MateriasOrigen.Single().Horas.Should().Be(144);
        e.Explicacion.Should().Contain(x => x.Contains("sílabo de Escuela Politécnica Nacional adjuntado"));
        r.Resumen.Should().Contain("1 cumple(n)");
    }

    [Fact]
    public async Task Movilidad_externa_con_silabo_incompleto_no_se_puede_homologar()
    {
        await using var db = CrearDb();
        var r = await Analizar(db, CertificadoExterno((new[] { "Física" }, "Física")),
            silabos: new[] { Silabo("Física", ("Estatica", 36), ("Dinamica", 36)) });

        var e = r.Evaluaciones.Single();
        e.SinSilabo.Should().BeFalse();
        e.Contenidos.Should().Be(50m, "el sílabo externo solo cubre 2 de las 4 unidades de destino");
        e.Estimacion.Should().Be(AnalisisAutomaticoService.NoCumple);
        e.Conclusion.Should().Contain("contenidos (50 %)");
        e.Explicacion.Should().Contain(x => x.StartsWith("✘ Unidad 3"));
    }

    [Fact]
    public async Task Movilidad_externa_con_silabo_solo_para_una_materia_mantiene_las_otras_sin_silabo()
    {
        await using var db = CrearDb();
        var r = await Analizar(db, CertificadoExterno(
                (new[] { "Física" }, "Física"),
                (new[] { "Fundamentos de Programación" }, "Algoritmos y Lógica de Programación")),
            silabos: new[] { Silabo("Física", ("Estatica", 36), ("Dinamica", 36), ("Energia", 36), ("Electricidad", 36)) });

        r.Evaluaciones.Single(e => e.MateriaDestino == "Física").Estimacion.Should().Be(AnalisisAutomaticoService.Cumple);
        var sinSilabo = r.Evaluaciones.Single(e => e.MateriaDestino == "Algoritmos y Lógica de Programación");
        sinSilabo.SinSilabo.Should().BeTrue();
        sinSilabo.Explicacion.Should().Contain(x => x.Contains("no se adjuntó su sílabo"));
        r.Requisitos.Single(q => q.Regla.StartsWith("Equivalencia mínima")).Cumple.Should().BeTrue(
            "la materia sin sílabo no cuenta como que falló el mínimo");
    }

    // ---------- agente preparándose: reintento automático ----------

    private static AnalisisAutomaticoService AgenteSinIa(SihaDbContextC db)
    {
        var ollama = new Mock<IOllamaEmbeddingService>();
        ollama.Setup(o => o.GenerarEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((float[]?)null); // Ollama todavía descargando el modelo
        var precedentes = new Mock<IPrecedenteService>();
        return new AnalisisAutomaticoService(db, Reglas(), precedentes.Object, ollama.Object,
            NullLogger<AnalisisAutomaticoService>.Instance, null, new CatalogoFalso(MallaSw, MallaTi));
    }

    [Fact]
    public async Task Si_la_IA_no_esta_lista_queda_preparandose_y_no_en_error()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, Certificado((new[] { "Física" }, "Física")));

        await AgenteSinIa(db).AnalizarYGuardarAsync(expediente);

        var c = await db.AnalisisExpedientes.SingleAsync();
        c.Estado.Should().Be(EstadosAnalisis.Preparando);
        c.Mensaje.Should().Contain("El agente se está preparando").And.Contain("no hace falta hacer nada");
        c.Intentos.Should().Be(1);
        c.ProximoIntentoUtc.Should().BeCloseTo(DateTimeOffset.UtcNow.AddSeconds(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Cuando_la_IA_ya_esta_lista_el_reintento_completa_el_analisis()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, Certificado((new[] { "Física" }, "Física")));
        await AgenteSinIa(db).AnalizarYGuardarAsync(expediente);

        await Agente(db).AnalizarYGuardarAsync(expediente);

        var c = await db.AnalisisExpedientes.SingleAsync();
        c.Estado.Should().Be(EstadosAnalisis.Analizado);
        c.Mensaje.Should().BeNull();
        c.Intentos.Should().Be(0);
        c.ProximoIntentoUtc.Should().BeNull();
    }

    [Fact]
    public async Task Tras_el_maximo_de_intentos_queda_en_error_con_un_mensaje_claro()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, Certificado((new[] { "Física" }, "Física")));
        var contexto = await db.AnalisisExpedientes.SingleAsync();
        contexto.Estado = EstadosAnalisis.Preparando;
        contexto.Intentos = AnalisisAutomaticoService.MaximoIntentos - 1;
        await db.SaveChangesAsync();

        await AgenteSinIa(db).AnalizarYGuardarAsync(expediente);

        contexto = await db.AnalisisExpedientes.SingleAsync();
        contexto.Estado.Should().Be(EstadosAnalisis.Error);
        contexto.Mensaje.Should().Contain("Reintentar el análisis");
        contexto.ProximoIntentoUtc.Should().BeNull();
    }

    [Fact]
    public async Task Reintentar_a_mano_vuelve_a_contar_desde_cero()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, Certificado((new[] { "Física" }, "Física")));
        var contexto = await db.AnalisisExpedientes.SingleAsync();
        contexto.Estado = EstadosAnalisis.Error;
        contexto.Intentos = AnalisisAutomaticoService.MaximoIntentos;
        await db.SaveChangesAsync();

        await AgenteSinIa(db).AnalizarYGuardarAsync(expediente, manual: true);

        contexto = await db.AnalisisExpedientes.SingleAsync();
        contexto.Estado.Should().Be(EstadosAnalisis.Preparando);
        contexto.Intentos.Should().Be(1);
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(5, 480)]
    [InlineData(6, 600)]
    [InlineData(14, 600)]
    public void La_espera_entre_reintentos_crece_hasta_diez_minutos(int intento, int segundos)
        => AnalisisAutomaticoService.EsperaReintento(intento).Should().Be(TimeSpan.FromSeconds(segundos));

    [Fact]
    public async Task El_servicio_de_fondo_solo_reintenta_las_que_ya_cumplieron_su_espera()
    {
        var nombre = $"siha-reintento-{Guid.NewGuid():N}";
        var servicios = new ServiceCollection();
        servicios.AddDbContext<SihaDbContextC>(o => o.UseInMemoryDatabase(nombre));
        servicios.AddScoped<IAnalisisAutomaticoService>(sp => Agente(sp.GetRequiredService<SihaDbContextC>()));
        await using var proveedor = servicios.BuildServiceProvider();

        Guid listo, esperando;
        using (var scope = proveedor.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SihaDbContextC>();
            listo = await Sembrar(db, Certificado((new[] { "Física" }, "Física")));
            esperando = await Sembrar(db, Certificado((new[] { "Física" }, "Física")));
            foreach (var c in db.AnalisisExpedientes)
            {
                c.Estado = EstadosAnalisis.Preparando;
                c.Intentos = 1;
                c.ProximoIntentoUtc = c.ExpedienteId == listo ? DateTimeOffset.UtcNow.AddSeconds(-1) : DateTimeOffset.UtcNow.AddMinutes(5);
            }
            await db.SaveChangesAsync();
        }

        var reintentadas = await ReintentoAnalisisService.ReintentarPendientesAsync(
            proveedor.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);

        reintentadas.Should().Be(1);
        using var verificacion = proveedor.CreateScope();
        var dbV = verificacion.ServiceProvider.GetRequiredService<SihaDbContextC>();
        (await dbV.AnalisisExpedientes.SingleAsync(a => a.ExpedienteId == listo)).Estado.Should().Be(EstadosAnalisis.Analizado);
        (await dbV.AnalisisExpedientes.SingleAsync(a => a.ExpedienteId == esperando)).Estado.Should().Be(EstadosAnalisis.Preparando);
    }

    [Fact]
    public async Task Sin_malla_de_destino_queda_en_error_con_el_motivo()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, Certificado((new[] { "Física" }, "Física")));

        await Agente(db, Array.Empty<DestinoAnalisisRequest>()).AnalizarYGuardarAsync(expediente);

        var contexto = await db.AnalisisExpedientes.SingleAsync();
        contexto.Estado.Should().Be(EstadosAnalisis.Error);
        contexto.Mensaje.Should().Contain("no tiene malla");
    }

    // ---------- docentes revisores ----------

    private static DictamenRequest Decidir(params (string Materia, bool Aprueba)[] d)
        => new(d.Select(x => new DecisionMateriaRequest(x.Materia, x.Aprueba, null)).ToList(), "Revisado");

    private static async Task<Guid> SolicitudAnalizada(SihaDbContextC db)
    {
        var expediente = await Sembrar(db, Certificado(
            (new[] { "Fundamentos de Programación" }, "Algoritmos y Lógica de Programación"),
            (new[] { "Programación Orientada a Objetos" }, "Programación Orientada a Objetos")));
        await Agente(db).AnalizarYGuardarAsync(expediente);
        return expediente;
    }

    [Fact]
    public async Task La_solicitud_muestra_historial_estimacion_y_revisores()
    {
        await using var db = CrearDb();
        var expediente = await SolicitudAnalizada(db);

        var vista = (await new SolicitudesAnalisisService(db).ObtenerAsync(expediente, "admin@uta.edu.ec", esDocente: false))!;
        vista.CarreraOrigen.Should().Be("Tecnologías de la Información");
        vista.Historial.Should().Contain(m => m.Nombre == "Cálculo Integral" && !m.Aprobada);
        vista.ResultadoFinal.Select(m => (m.CodigoDestino, m.Estimacion, m.Resultado)).Should().Equal(
            ("Algoritmos y Lógica de Programación", AnalisisAutomaticoService.Cumple, "PENDIENTE"),
            ("Programación Orientada a Objetos", AnalisisAutomaticoService.NoCumple, "NO_HOMOLOGADA"));
        vista.PuedeResponder.Should().BeFalse("el administrador no es revisor");
    }

    [Fact]
    public async Task Solo_los_dos_docentes_asignados_ven_y_responden()
    {
        await using var db = CrearDb();
        var expediente = await SolicitudAnalizada(db);
        var solicitudes = new SolicitudesAnalisisService(db);

        (await solicitudes.ObtenerAsync(expediente, "otro@uta.edu.ec", esDocente: true)).Should().BeNull();
        (await solicitudes.ObtenerAsync(expediente, "mvaca@uta.edu.ec", esDocente: true))!.PuedeResponder.Should().BeTrue();
        var (_, _, codigo) = await solicitudes.ResponderAsync(expediente, "otro@uta.edu.ec", "Otro",
            Decidir(("Algoritmos y Lógica de Programación", true)));
        codigo.Should().Be(404);
    }

    [Fact]
    public async Task Con_dos_aprobaciones_queda_homologada_y_la_que_no_cumple_queda_no_homologada_sin_votos()
    {
        await using var db = CrearDb();
        var expediente = await SolicitudAnalizada(db);
        var solicitudes = new SolicitudesAnalisisService(db);
        const string alg = "Algoritmos y Lógica de Programación", poo = "Programación Orientada a Objetos";

        // Solo se vota la que cumple; POO (75 % en créditos) no se vota.
        var uno = await solicitudes.ResponderAsync(expediente, "hnaranjo@uta.edu.ec", "Naranjo", Decidir((alg, true)));
        uno.Error.Should().BeNull();
        uno.Respuesta!.EstadoRevision.Should().Be("EN_REVISION");
        var dos = await solicitudes.ResponderAsync(expediente, "mvaca@uta.edu.ec", "Vaca", Decidir((alg, true)));

        dos.Respuesta!.EstadoRevision.Should().Be("RESUELTA");
        dos.Respuesta.ResultadoFinal.Single(m => m.CodigoDestino == alg).Resultado.Should().Be("HOMOLOGADA");
        var noCumple = dos.Respuesta.ResultadoFinal.Single(m => m.CodigoDestino == poo);
        noCumple.Resultado.Should().Be("NO_HOMOLOGADA");
        (noCumple.Aprobaciones, noCumple.Rechazos).Should().Be((0, 0));
    }

    [Fact]
    public async Task Si_los_docentes_discrepan_queda_sin_acuerdo()
    {
        await using var db = CrearDb();
        var expediente = await SolicitudAnalizada(db);
        var solicitudes = new SolicitudesAnalisisService(db);
        const string alg = "Algoritmos y Lógica de Programación";

        await solicitudes.ResponderAsync(expediente, "hnaranjo@uta.edu.ec", "Naranjo", Decidir((alg, true)));
        var dos = await solicitudes.ResponderAsync(expediente, "mvaca@uta.edu.ec", "Vaca", Decidir((alg, false)));

        dos.Respuesta!.ResultadoFinal.Single(m => m.CodigoDestino == alg).Resultado.Should().Be("SIN_ACUERDO");
    }

    [Fact]
    public async Task No_se_puede_votar_una_materia_que_no_llega_al_80()
    {
        await using var db = CrearDb();
        var expediente = await SolicitudAnalizada(db);

        var (respuesta, error, codigo) = await new SolicitudesAnalisisService(db).ResponderAsync(expediente, "hnaranjo@uta.edu.ec", "Naranjo",
            Decidir(("Algoritmos y Lógica de Programación", true), ("Programación Orientada a Objetos", true)));

        respuesta.Should().BeNull();
        codigo.Should().Be(400);
        error.Should().Contain("Programación Orientada a Objetos").And.Contain("no se puede(n) homologar");
    }

    [Fact]
    public async Task Si_ninguna_materia_cumple_la_solicitud_queda_resuelta_sin_votos()
    {
        await using var db = CrearDb();
        var expediente = await Sembrar(db, Certificado((new[] { "Física Básica" }, "Física")));
        await Agente(db).AnalizarYGuardarAsync(expediente);

        var vista = (await new SolicitudesAnalisisService(db).ObtenerAsync(expediente, "hnaranjo@uta.edu.ec", esDocente: true))!;

        vista.EstadoRevision.Should().Be("RESUELTA");
        vista.PuedeResponder.Should().BeFalse("no hay nada que decidir: el reglamento no permite homologarla");
        vista.ResultadoFinal.Single().Resultado.Should().Be("NO_HOMOLOGADA");
    }

    // ---------- tabla RESULTADO_HOMOLOGACION ----------

    [Fact]
    public async Task El_analisis_deja_una_fila_por_materia_con_sus_porcentajes_y_creditos()
    {
        await using var db = CrearDb();
        await SolicitudAnalizada(db);

        var filas = await db.ResultadosHomologacion.OrderBy(f => f.NumeroSolicitud).ToListAsync();
        filas.Should().HaveCount(2);

        var alg = filas[0];
        alg.MateriasOrigen.Should().Be("Fundamentos de Programación");
        alg.MateriaDestino.Should().Be("Algoritmos y Lógica de Programación");
        alg.CodigoDestino.Should().Be("SW-ALG");
        alg.TipoTramite.Should().Be("INTERNO");
        alg.PorcentajeContenidos.Should().Be(100m);
        alg.CreditosOrigen.Should().Be(4);
        alg.CreditosDestino.Should().Be(4);
        alg.PorcentajeCreditos.Should().Be(100m);
        alg.MinimoExigido.Should().Be(80m);
        alg.EstimacionAgente.Should().Be(AnalisisAutomaticoService.Cumple);
        alg.ResultadoFinal.Should().Be("PENDIENTE");

        var poo = filas[1];
        poo.CreditosOrigen.Should().Be(3);
        poo.CreditosDestino.Should().Be(4);
        poo.PorcentajeCreditos.Should().Be(75m, "3 créditos aprobados frente a 4");
        poo.HorasOrigen.Should().Be(144);
        poo.HorasDestino.Should().Be(192);
        poo.EstimacionAgente.Should().Be(AnalisisAutomaticoService.NoCumple);
        poo.ResultadoFinal.Should().Be("NO_HOMOLOGADA", "no llega al 80 % en créditos");
    }

    [Fact]
    public async Task La_decision_de_los_docentes_se_refleja_en_la_tabla()
    {
        await using var db = CrearDb();
        var expediente = await SolicitudAnalizada(db);
        var solicitudes = new SolicitudesAnalisisService(db);
        const string alg = "Algoritmos y Lógica de Programación", poo = "Programación Orientada a Objetos";

        await solicitudes.ResponderAsync(expediente, "hnaranjo@uta.edu.ec", "Naranjo", Decidir((alg, true)));
        (await db.ResultadosHomologacion.SingleAsync(f => f.MateriaDestino == alg)).Aprobaciones.Should().Be(1);

        await solicitudes.ResponderAsync(expediente, "mvaca@uta.edu.ec", "Vaca", Decidir((alg, true)));

        var filaAlg = await db.ResultadosHomologacion.SingleAsync(f => f.MateriaDestino == alg);
        filaAlg.Aprobaciones.Should().Be(2);
        filaAlg.ResultadoFinal.Should().Be("HOMOLOGADA");
        var filaPoo = await db.ResultadosHomologacion.SingleAsync(f => f.MateriaDestino == poo);
        (filaPoo.Aprobaciones, filaPoo.Rechazos).Should().Be((0, 0), "los docentes no votan la que no cumple");
        filaPoo.ResultadoFinal.Should().Be("NO_HOMOLOGADA");
        (await db.ResultadosHomologacion.CountAsync()).Should().Be(2, "las filas se actualizan, no se duplican");
    }

    [Fact]
    public async Task Volver_a_analizar_no_duplica_las_filas()
    {
        await using var db = CrearDb();
        var expediente = await SolicitudAnalizada(db);

        await Agente(db).AnalizarYGuardarAsync(expediente);

        (await db.ResultadosHomologacion.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Movilidad_externa_guarda_los_creditos_del_silabo_adjunto()
    {
        await using var db = CrearDb();
        await Analizar(db, CertificadoExterno((new[] { "Física" }, "Física"), (new[] { "Fundamentos de Programación" }, "Algoritmos y Lógica de Programación")),
            silabos: new[] { Silabo("Física", ("Estatica", 36), ("Dinamica", 36), ("Energia", 36), ("Electricidad", 36)) });

        var fisica = await db.ResultadosHomologacion.SingleAsync(f => f.MateriaDestino == "Física");
        fisica.TipoTramite.Should().Be("EXTERNO");
        fisica.InstitucionOrigen.Should().Be("Escuela Politécnica Nacional");
        fisica.SinSilabo.Should().BeFalse();
        fisica.CreditosOrigen.Should().Be(3);
        fisica.PorcentajeCreditos.Should().Be(100m);

        var sinSilabo = await db.ResultadosHomologacion.SingleAsync(f => f.MateriaDestino == "Algoritmos y Lógica de Programación");
        sinSilabo.SinSilabo.Should().BeTrue();
        sinSilabo.CreditosOrigen.Should().BeNull("sin sílabo no se conocen los créditos de la otra universidad");
        sinSilabo.PorcentajeCreditos.Should().BeNull();
    }

    [Fact]
    public async Task El_docente_debe_decidir_todas_las_materias_que_cumplen()
    {
        await using var db = CrearDb();
        // Las dos cumplen el 80 % en contenidos y créditos: hay que decidir ambas.
        var expediente = await Sembrar(db, Certificado(
            (new[] { "Fundamentos de Programación" }, "Algoritmos y Lógica de Programación"),
            (new[] { "Física" }, "Física")));
        await Agente(db).AnalizarYGuardarAsync(expediente);

        var (_, error, codigo) = await new SolicitudesAnalisisService(db)
            .ResponderAsync(expediente, "hnaranjo@uta.edu.ec", "Naranjo", Decidir(("Algoritmos y Lógica de Programación", true)));

        codigo.Should().Be(400);
        error.Should().Contain("Física");
    }

    // ---------- llegada del certificado ----------

    [Fact]
    public async Task El_certificado_SIHA_guarda_el_contexto_para_el_agente_sin_extracciones()
    {
        using var temp = new Soporte.DirectorioTemporal();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!Directory.Exists(Path.Combine(dir!.FullName, "documentos-prueba"))) dir = dir.Parent;
        var pdf = File.ReadAllBytes(Path.Combine(dir.FullName, "documentos-prueba", "certificados", "SIHA-TI-SW-cambio-de-carrera.pdf"));
        temp.CrearArchivo("cert.pdf", pdf);
        await using var db = CrearDb();
        var opciones = Options.Create(new AlmacenOptions { RutaAlmacenamiento = temp.Ruta });
        var servicio = new ProcesarSolicitudCreadaService(db, new AlmacenDocumentos(opciones),
            new ExtractorPdfService(opciones, NullLogger<ExtractorPdfService>.Instance),
            NullLogger<ProcesarSolicitudCreadaService>.Instance, new LectorCertificadosAlmacen(opciones));
        var documento = new DocumentoReferencia
        {
            DocumentoId = Guid.NewGuid(), RutaAlmacenamiento = "cert.pdf",
            HashSha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant(), TamanoBytes = pdf.Length
        };
        var evento = new SolicitudCreadaEvent
        {
            ExpedienteId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), CodigoExpediente = "EXP-1",
            TipoTramite = "INTERNO", RutaNormativa = "CAU-P-011", CarreraOrigenCodigo = "TI", CarreraDestinoCodigo = "SW",
            DocumentoIds = new[] { documento.DocumentoId }, Documentos = new[] { documento }
        };

        await servicio.ProcesarAsync(evento, Guid.NewGuid());

        (await db.Extracciones.CountAsync()).Should().Be(0, "el certificado no trae contenidos: el agente usa los sílabos");
        var contexto = await db.AnalisisExpedientes.SingleAsync();
        contexto.CarreraOrigenCodigo.Should().Be("TI");
        contexto.CarreraDestinoNombre.Should().Be("Software");
        contexto.Revisor1Correo.Should().Be("hnaranjo@uta.edu.ec");
        contexto.SilabosExternosJson.Should().BeNull();
        var certificado = JsonSerializer.Deserialize<CertificadoSiha>(contexto.CertificadoJson)!;
        certificado.Solicitudes.Should().HaveCount(5);
        certificado.Materias.Should().Contain(m => m.Nombre == "Medidas Eléctricas" && !m.Aprobada);
    }

    [Fact]
    public async Task El_silabo_externo_adjunto_se_guarda_para_el_agente_y_no_pasa_por_el_extractor()
    {
        using var temp = new Soporte.DirectorioTemporal();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!Directory.Exists(Path.Combine(dir!.FullName, "documentos-prueba"))) dir = dir.Parent;
        var cert = File.ReadAllBytes(Path.Combine(dir.FullName, "documentos-prueba", "certificados-pruebas", "V06-movilidad-externa-TI-SW.pdf"));
        var silabo = File.ReadAllBytes(Path.Combine(dir.FullName, "documentos-prueba", "silabos-externos", "ESPOCH-fisica.pdf"));
        temp.CrearArchivo("cert.pdf", cert);
        temp.CrearArchivo("silabo.pdf", silabo);
        await using var db = CrearDb();
        var opciones = Options.Create(new AlmacenOptions { RutaAlmacenamiento = temp.Ruta });
        var servicio = new ProcesarSolicitudCreadaService(db, new AlmacenDocumentos(opciones),
            new ExtractorPdfService(opciones, NullLogger<ExtractorPdfService>.Instance),
            NullLogger<ProcesarSolicitudCreadaService>.Instance, new LectorCertificadosAlmacen(opciones));
        var documentos = new[]
        {
            new DocumentoReferencia
            {
                DocumentoId = Guid.NewGuid(), RutaAlmacenamiento = "cert.pdf",
                HashSha256 = Convert.ToHexString(SHA256.HashData(cert)).ToLowerInvariant(), TamanoBytes = cert.Length,
                Tipo = Siha.Contracts.TiposDocumentoExpediente.Certificado
            },
            new DocumentoReferencia
            {
                DocumentoId = Guid.NewGuid(), RutaAlmacenamiento = "silabo.pdf",
                HashSha256 = Convert.ToHexString(SHA256.HashData(silabo)).ToLowerInvariant(), TamanoBytes = silabo.Length,
                Tipo = Siha.Contracts.TiposDocumentoExpediente.SilaboExterno, MateriaOrigen = "Física"
            }
        };
        var evento = new SolicitudCreadaEvent
        {
            ExpedienteId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), CodigoExpediente = "EXP-2",
            TipoTramite = "EXTERNO", RutaNormativa = "CAU-P-122", CarreraOrigenCodigo = "EXTERNA", CarreraDestinoCodigo = "SW",
            DocumentoIds = documentos.Select(d => d.DocumentoId).ToArray(), Documentos = documentos
        };

        await servicio.ProcesarAsync(evento, Guid.NewGuid());

        (await db.Extracciones.CountAsync()).Should().Be(0, "el sílabo externo no debe generar una extracción del flujo anterior");
        var contexto = await db.AnalisisExpedientes.SingleAsync();
        contexto.SilabosExternosJson.Should().NotBeNull();
        var silabos = JsonSerializer.Deserialize<List<SilaboExterno>>(contexto.SilabosExternosJson!)!;
        var fisica = silabos.Should().ContainSingle().Subject;
        fisica.Materia.Should().Be("Física");
        fisica.Unidades.Should().HaveCount(4);
        fisica.HorasTotales.Should().Be(128);
    }
}
