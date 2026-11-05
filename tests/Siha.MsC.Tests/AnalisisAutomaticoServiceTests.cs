using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
            M("Programación Orientada a Objetos", 2), M("Física II", 2), M("Cálculo Integral", 2, 6.2m, false)
        },
        pedidas.Select((p, i) => new SolicitudHomologacion(i + 1, p.Origen, p.Destino)).ToList(),
        new[] { new RevisorCertificado("Ing. Naranjo", "hnaranjo@uta.edu.ec"), new RevisorCertificado("Ing. Vaca", "mvaca@uta.edu.ec") });

    private static async Task<Guid> Sembrar(SihaDbContextC db, CertificadoSiha c)
    {
        var expediente = Guid.NewGuid();
        db.AnalisisExpedientes.Add(new AnalisisExpediente
        {
            ExpedienteId = expediente, CodigoExpediente = "EXP-1", RutaNormativa = "CAU-P-011", TipoTramite = "INTERNO",
            InstitucionOrigen = "UTA", CarreraOrigenCodigo = "TI", CarreraDestinoCodigo = "SW",
            CarreraOrigenNombre = "Tecnologías de la Información", CarreraDestinoNombre = "Software",
            CertificadoJson = JsonSerializer.Serialize(c),
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

    private static async Task<AnalisisAutomaticoResponse> Analizar(SihaDbContextC db, CertificadoSiha c, IReadOnlyList<DestinoAnalisisRequest>? sw = null)
    {
        var expediente = await Sembrar(db, c);
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
        e.Estimacion.Should().Be(AnalisisAutomaticoService.Cumple);
        e.Explicacion.Should().Contain(x => x.StartsWith("✔ Unidad 2") && x.Contains("«Fundamentos de Programación»"));
        e.Explicacion.Should().Contain(x => x.StartsWith("Carga horaria: 192 horas aprobadas frente a 192"));
        e.Conclusion.Should().Contain("recomienda homologar");
        e.MateriasOrigen.Single().PeriodoAcademico.Should().Be("Abr-Sep 2025");
    }

    [Fact]
    public async Task Pocas_horas_dejan_la_materia_a_criterio_del_docente_aunque_los_temas_coincidan()
    {
        await using var db = CrearDb();
        var r = await Analizar(db, Certificado((new[] { "Programación Orientada a Objetos" }, "Programación Orientada a Objetos")));

        var e = r.Evaluaciones.Single();
        e.Contenidos.Should().Be(100m);
        e.CargaHoraria.Should().Be(75m, "144 horas aprobadas frente a 192");
        e.Profundidad.Should().Be(75m, "cada tema tiene 36 horas aprobadas frente a 48");
        e.Estimacion.Should().Be(AnalisisAutomaticoService.CriterioDocente);
        e.Conclusion.Should().Contain("carga horaria (75 %)").And.Contain("criterio de los docentes");
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
        e.Estimacion.Should().Be(AnalisisAutomaticoService.CriterioDocente);
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
        r.Resumen.Should().Contain("1 cumple(n)").And.Contain("1 queda(n) a criterio");
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
        vista.ResultadoFinal.Select(m => (m.CodigoDestino, m.Estimacion)).Should().Equal(
            ("Algoritmos y Lógica de Programación", AnalisisAutomaticoService.Cumple),
            ("Programación Orientada a Objetos", AnalisisAutomaticoService.CriterioDocente));
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
    public async Task Con_dos_aprobaciones_queda_homologada_y_si_discrepan_sin_acuerdo()
    {
        await using var db = CrearDb();
        var expediente = await SolicitudAnalizada(db);
        var solicitudes = new SolicitudesAnalisisService(db);
        const string alg = "Algoritmos y Lógica de Programación", poo = "Programación Orientada a Objetos";

        var uno = await solicitudes.ResponderAsync(expediente, "hnaranjo@uta.edu.ec", "Naranjo", Decidir((alg, true), (poo, false)));
        uno.Respuesta!.EstadoRevision.Should().Be("EN_REVISION");
        var dos = await solicitudes.ResponderAsync(expediente, "mvaca@uta.edu.ec", "Vaca", Decidir((alg, true), (poo, true)));

        dos.Respuesta!.EstadoRevision.Should().Be("RESUELTA");
        dos.Respuesta.ResultadoFinal.Single(m => m.CodigoDestino == alg).Resultado.Should().Be("HOMOLOGADA");
        dos.Respuesta.ResultadoFinal.Single(m => m.CodigoDestino == poo).Resultado.Should().Be("SIN_ACUERDO");
    }

    [Fact]
    public async Task El_docente_debe_decidir_todas_las_materias()
    {
        await using var db = CrearDb();
        var expediente = await SolicitudAnalizada(db);

        var (_, error, codigo) = await new SolicitudesAnalisisService(db)
            .ResponderAsync(expediente, "hnaranjo@uta.edu.ec", "Naranjo", Decidir(("Algoritmos y Lógica de Programación", true)));

        codigo.Should().Be(400);
        error.Should().Contain("Programación Orientada a Objetos");
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
        var certificado = JsonSerializer.Deserialize<CertificadoSiha>(contexto.CertificadoJson)!;
        certificado.Solicitudes.Should().HaveCount(5);
        certificado.Materias.Should().Contain(m => m.Nombre == "Medidas Eléctricas" && !m.Aprobada);
    }
}
