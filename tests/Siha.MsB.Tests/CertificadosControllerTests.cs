using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Moq;
using Siha.Certificados;
using Siha.Contracts;
using Siha.Contracts.Events;
using Siha.MsB.Api.Contracts.Dtos;
using Siha.MsB.Api.Controllers;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Events;
using Siha.MsB.Api.Options;
using Siha.MsB.Api.Services;
using Siha.MsB.Tests.Soporte;

namespace Siha.MsB.Tests;

/// <summary>
/// Registro de la solicitud con un único PDF: el certificado SIHA preaprobado.
/// El operador no digita carreras, materias, fechas ni matrículas.
/// </summary>
public class CertificadosControllerTests : IDisposable
{
    private readonly string _directorio = Pruebas.DirectorioTemporal();
    private readonly SihaDbContextB _db = Pruebas.CrearContextoB();
    private readonly Mock<IExpedienteEventPublisher> _publicador = new();
    private readonly List<SolicitudCreadaEvent> _publicados = new();

    /// <summary>Usuarios de MS A simulados: correo → (rol DOCENTE, activo).</summary>
    private readonly Dictionary<string, (bool Docente, bool Activo)> _usuarios = new()
    {
        ["hnaranjo@uta.edu.ec"] = (true, true),
        ["mvaca@uta.edu.ec"] = (true, true),
        ["jcastro@uta.edu.ec"] = (true, true)
    };

    /// <summary>Materias de la malla vigente por carrera, para la sugerencia del nombre oficial.</summary>
    private readonly Dictionary<string, string[]> _mallas = new();

    private sealed class VerificadorFalso(
        Dictionary<string, (bool Docente, bool Activo)> usuarios,
        Dictionary<string, string[]>? mallas = null) : IVerificadorDocentes
    {
        public Task<IReadOnlyList<string>> ObtenerMateriasAsync(string carreraCodigo, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(mallas is not null && mallas.TryGetValue(carreraCodigo, out var m) ? m : Array.Empty<string>());

        public Task<IReadOnlyList<DocenteRevisorVerificado>> VerificarAsync(IEnumerable<string> correos, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocenteRevisorVerificado>>(correos.Select(c => usuarios.TryGetValue(c, out var u)
                ? new DocenteRevisorVerificado(c, true, u.Docente, u.Activo, $"Docente {c.Split('@')[0]}")
                : new DocenteRevisorVerificado(c, false, false, false, string.Empty)).ToList());

        public Task<IReadOnlyList<DocenteRevisorVerificado>> ListarDocentesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocenteRevisorVerificado>>(usuarios.Where(u => u.Value.Docente && u.Value.Activo)
                .Select(u => new DocenteRevisorVerificado(u.Key, true, true, true, $"Docente {u.Key.Split('@')[0]}")).ToList());

        public Task<CarreraCatalogo?> ResolverCarreraAsync(string nombre, CancellationToken cancellationToken = default)
            => Task.FromResult<CarreraCatalogo?>(nombre switch
            {
                "Software" => new CarreraCatalogo("SW", "Ingeniería de Software"),
                "Tecnologías de la Información" => new CarreraCatalogo("TI", "Tecnologías de la Información"),
                "Ingeniería Industrial" => new CarreraCatalogo("IND", "Ingeniería Industrial"),
                _ => null
            });
    }

    public CertificadosControllerTests()
    {
        _publicador
            .Setup(p => p.PublicarAsync(It.IsAny<SolicitudCreadaEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SolicitudCreadaEvent, CancellationToken>((evento, _) => _publicados.Add(evento))
            .ReturnsAsync((true, (string?)null));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorio)) Directory.Delete(_directorio, recursive: true);
    }

    private CertificadosController Controlador()
    {
        var almacenamiento = Pruebas.CrearAlmacenamiento(Pruebas.OpcionesArchivos(_directorio));
        var envio = new ExpedienteEnvioService(_db, almacenamiento, _publicador.Object,
            Options.Create(Pruebas.OpcionesArchivos(_directorio)), Options.Create(new RepublicacionOptions()),
            NullLogger<ExpedienteEnvioService>.Instance);
        return new CertificadosController(_db, almacenamiento, envio, new VerificadorFalso(_usuarios, _mallas), NullLogger<CertificadosController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static IFormFile Certificado(string nombre) => Pruebas.Archivo(
        File.ReadAllBytes(Path.Combine(Pruebas.RaizRepositorio(), "documentos-prueba", "certificados", nombre)),
        nombreArchivo: nombre);

    /// <summary>Certificado V06: movilidad externa desde la ESPOCH, TI → Software.</summary>
    private static IFormFile CertificadoExterno() => Pruebas.Archivo(
        File.ReadAllBytes(Path.Combine(Pruebas.RaizRepositorio(), "documentos-prueba", "certificados-pruebas",
            "V06-movilidad-externa-TI-SW.pdf")),
        nombreArchivo: "V06-movilidad-externa-TI-SW.pdf");

    private static IFormFile Silabo(string nombre) => Pruebas.Archivo(
        File.ReadAllBytes(Path.Combine(Pruebas.RaizRepositorio(), "documentos-prueba", "silabos-externos", nombre)),
        nombreArchivo: nombre);

    private static IEnumerable<string> Detalles(object cuerpo)
        => (IEnumerable<string>)cuerpo.GetType().GetProperty("detalles",
            System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!.GetValue(cuerpo)!;

    [Fact]
    public async Task Un_solo_pdf_crea_el_expediente_completo_y_lo_envia()
    {
        var resultado = await Controlador().Registrar(Certificado("SIHA-TI-SW-cambio-de-carrera.pdf"), CancellationToken.None);

        var respuesta = resultado.Result.Should().BeOfType<AcceptedResult>().Subject.Value
            .Should().BeOfType<SolicitudCertificadoResponse>().Subject;
        respuesta.Publicado.Should().BeTrue();

        var e = respuesta.Expediente;
        e.TipoTramite.Should().Be(TiposTramite.Interno);
        e.RutaNormativa.Should().Be(RutasNormativas.CambioCarreraOu);
        e.CarreraOrigenCodigo.Should().Be("TI");
        e.CarreraDestinoCodigo.Should().Be("SW");
        e.CarreraOrigenNombre.Should().Be("Tecnologías de la Información");
        e.CarreraDestinoNombre.Should().Be("Software");
        e.NumeroCertificado.Should().Be("SIHA-2026-0147");
        e.EstudianteNombre.Should().Be("Pérez Gómez Ana Lucía");
        e.Estado.Should().Be(ExpedienteEstados.PendienteContenidos);
        e.Documentos.Should().ContainSingle();
        e.Asignaturas.Should().HaveCount(11, "el historial trae aprobadas y reprobadas");

        var fundamentos = e.Asignaturas.Single(a => a.NombreOrigen == "Fundamentos de Programación");
        fundamentos.Nivel.Should().Be(1);
        fundamentos.PeriodoAcademico.Should().Be("Abr-Sep 2025");
        fundamentos.Nota.Should().Be(8.9);
        fundamentos.Aprobada.Should().BeTrue();
        fundamentos.AsignaturaDestinoNombre.Should().Be("Algoritmos y Lógica de Programación");
        e.Asignaturas.Single(a => a.NombreOrigen == "Medidas Eléctricas").Aprobada.Should().BeFalse();
        e.Revisores!.Select(r => r.Correo).Should().Equal("hnaranjo@uta.edu.ec", "mvaca@uta.edu.ec");
        e.Revisores!.First().Nombre.Should().Be("Docente hnaranjo", "se usa el nombre registrado en MS A");

        var evento = _publicados.Should().ContainSingle().Subject;
        evento.AsignaturasOrigen.Should().HaveCount(11);
        evento.Documentos.Should().ContainSingle();
    }

    [Fact]
    public async Task Pdf_que_no_es_la_plantilla_se_rechaza_con_422_sin_crear_nada()
    {
        var resultado = await Controlador().Registrar(Certificado("plan-analitico-no-valido.pdf"), CancellationToken.None);

        resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>();
        _db.Expedientes.Should().BeEmpty();
        Directory.GetFiles(_directorio, "*", SearchOption.AllDirectories).Should().BeEmpty();
        _publicados.Should().BeEmpty();
    }

    [Fact]
    public async Task Certificado_con_errores_devuelve_la_lista_de_problemas()
    {
        var resultado = await Controlador().Validar(Certificado("SIHA-con-errores.pdf"), CancellationToken.None);

        var cuerpo = resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject.Value!;
        var detalles = Detalles(cuerpo);
        detalles.Should().Contain(d => d.Contains("primer período académico"));
    }

    [Fact]
    public async Task El_mismo_certificado_no_se_registra_dos_veces()
    {
        await Controlador().Registrar(Certificado("SIHA-TI-SW-cambio-de-carrera.pdf"), CancellationToken.None);

        var segunda = await Controlador().Registrar(Certificado("SIHA-TI-SW-cambio-de-carrera.pdf"), CancellationToken.None);

        segunda.Result.Should().BeOfType<ConflictObjectResult>();
        _db.Expedientes.Should().ContainSingle();
    }

    [Fact]
    public async Task Un_registro_del_formato_anterior_no_impide_registrar_el_certificado_actual()
    {
        await Controlador().Registrar(Certificado("SIHA-TI-SW-cambio-de-carrera.pdf"), CancellationToken.None);
        // Simula el formato anterior del certificado: sus materias no traen período académico.
        var anterior = _db.Expedientes.Include(e => e.Asignaturas).Single();
        foreach (var a in anterior.Asignaturas) a.PeriodoAcademico = null;
        await _db.SaveChangesAsync();

        var segunda = await Controlador().Registrar(Certificado("SIHA-TI-SW-cambio-de-carrera.pdf"), CancellationToken.None);

        segunda.Result.Should().BeOfType<AcceptedResult>();
        _db.Expedientes.Should().HaveCount(2, "el anterior se conserva archivado");
        _db.Expedientes.Count(e => e.NumeroCertificado == "SIHA-2026-0147").Should().Be(1);
        _db.Expedientes.Single(e => e.Id == anterior.Id).NumeroCertificado.Should()
            .StartWith("SIHA-2026-0147 (anterior ");
    }

    [Theory]
    [InlineData("SIHA-2026-0147", "EXP-20261104-E46994")]
    [InlineData("SIHA-UN-NUMERO-DE-CERTIFICADO-MUY-LARGO-0001", "EXP-20261104-E46994")]
    public void El_numero_archivado_cabe_en_la_columna(string numero, string codigo)
        => CertificadosController.NumeroArchivado(numero, codigo).Length.Should().BeLessThanOrEqualTo(40);

    // ---------- Registro manual (sin PDF) ----------

    private static RegistroManualRequest Manual() => new(
        "Torres Vega Martín Andrés", "1805123456", "Cambio de carrera", "Universidad Técnica de Ambato",
        "Tecnologías de la Información", "Software", "hnaranjo@uta.edu.ec", "mvaca@uta.edu.ec",
        new[]
        {
            new MateriaManualRequest("Fundamentos de Programación", 1, "Abr-Sep 2025", 8.9m, true),
            new MateriaManualRequest("Física", 1, "Abr-Sep 2025", 8.1m, true),
            new MateriaManualRequest("Cálculo Integral", 2, "Oct 2025-Mar 2026", 6.2m, false)
        },
        new[]
        {
            new SolicitudManualRequest(new[] { "Fundamentos de Programación" }, "Algoritmos y Lógica de Programación"),
            new SolicitudManualRequest(new[] { "Física" }, "Física")
        });

    [Fact]
    public async Task El_registro_manual_crea_la_solicitud_igual_que_el_certificado()
    {
        var resultado = await Controlador().RegistrarManual(Manual(), CancellationToken.None);

        var respuesta = resultado.Result.Should().BeOfType<AcceptedResult>().Subject.Value
            .Should().BeOfType<SolicitudCertificadoResponse>().Subject;
        respuesta.Publicado.Should().BeTrue();
        var e = respuesta.Expediente;
        e.NumeroCertificado.Should().StartWith("MANUAL-");
        e.TipoTramite.Should().Be(TiposTramite.Interno);
        e.RutaNormativa.Should().Be(RutasNormativas.CambioCarreraOu);
        e.CarreraOrigenCodigo.Should().Be("TI");
        e.CarreraDestinoCodigo.Should().Be("SW");
        e.Asignaturas.Should().HaveCount(3);
        e.Asignaturas.Single(a => a.NombreOrigen == "Física").PeriodoAcademico.Should().Be("Abr-Sep 2025");
        e.Revisores!.Select(r => r.Nombre).Should().Equal("Docente hnaranjo", "Docente mvaca");

        // El documento guardado son los datos validados en JSON: MS C los lee como un certificado.
        var documento = _db.ExpedienteDocumentos.Single();
        documento.MimeType.Should().Be("application/json");
        var guardado = System.Text.Json.JsonSerializer.Deserialize<CertificadoSiha>(
            File.ReadAllBytes(Path.Combine(_directorio, documento.RutaAlmacenamiento)))!;
        guardado.EstudianteNombre.Should().Be("Torres Vega Martín Andrés");
        guardado.Solicitudes.Should().HaveCount(2);
        guardado.EmitidoPor.Should().StartWith("Registro manual");
        _publicados.Should().ContainSingle();
    }

    [Fact]
    public async Task El_registro_manual_con_datos_faltantes_se_rechaza_con_la_lista_de_verificacion()
    {
        var incompleto = Manual() with { Cedula = "", Revisor2Correo = null, CarreraDestino = "SW" };

        var resultado = await Controlador().ValidarManual(incompleto, CancellationToken.None);

        var rechazo = resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject.Value
            .Should().BeOfType<RechazoCertificadoResponse>().Subject;
        rechazo.Secciones.Single(s => s.Seccion == "Estudiante").Ok.Should().BeFalse("falta la cédula");
        rechazo.Secciones.Single(s => s.Seccion == "Revisores").Ok.Should().BeFalse("falta el segundo revisor");
        rechazo.Detalles.Should().Contain(d => d.Contains("siglas") || d.Contains("nombre completo"));
        _db.Expedientes.Should().BeEmpty();
    }

    [Fact]
    public async Task Pedir_una_materia_reprobada_a_mano_se_rechaza_igual_que_en_el_certificado()
    {
        var conReprobada = Manual() with
        {
            Solicitudes = new[] { new SolicitudManualRequest(new[] { "Cálculo Integral" }, "Cálculo Integral") }
        };

        var resultado = await Controlador().RegistrarManual(conReprobada, CancellationToken.None);

        var cuerpo = resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject.Value!;
        Detalles(cuerpo).Should().Contain(d => d.Contains("'Cálculo Integral' está reprobada"));
        _db.Expedientes.Should().BeEmpty();
    }

    [Fact]
    public async Task Las_opciones_del_registro_manual_traen_los_docentes_y_las_materias()
    {
        _mallas["SW"] = new[] { "ALGORITMOS Y LOGICA DE PROGRAMACION", "FISICA" };

        var resultado = await Controlador().OpcionesManual("Tecnologías de la Información", "Software", CancellationToken.None);

        var opciones = resultado.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<OpcionesRegistroManualResponse>().Subject;
        opciones.Docentes.Select(d => d.Correo).Should().BeEquivalentTo("hnaranjo@uta.edu.ec", "mvaca@uta.edu.ec", "jcastro@uta.edu.ec");
        opciones.MateriasDestino.Should().Equal("ALGORITMOS Y LOGICA DE PROGRAMACION", "FISICA");
    }

    [Fact]
    public void Las_lineas_del_registro_manual_quitan_saltos_de_linea_y_usan_la_ruta_del_tramite()
    {
        var r = Manual() with { EstudianteNombre = "Ana\nLucía", TipoTramite = "Movilidad externa" };

        var lineas = CertificadosController.LineasRegistroManual(r, "MANUAL-1", new DateOnly(2026, 11, 24), "Operador",
            new Dictionary<string, string> { ["hnaranjo@uta.edu.ec"] = "Ing. Hernán Naranjo" });

        lineas.Should().Contain("Estudiante: Ana Lucía");
        lineas.Should().Contain("Ruta normativa: CAU-P-122");
        lineas.Should().Contain("Docente revisor 1: Ing. Hernán Naranjo - hnaranjo@uta.edu.ec");
        lineas.Should().Contain("Nivel: 1 Período académico: Abr-Sep 2025 Nota: 8.9 Estado: Aprobada");
        lineas.Last().Should().Be("FIN DEL CERTIFICADO");
    }

    [Fact]
    public async Task Validar_lee_el_certificado_sin_registrar()
    {
        var resultado = await Controlador().Validar(Certificado("SIHA-IND-SW-cambio-de-carrera.pdf"), CancellationToken.None);

        var leido = resultado.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<CertificadoLeidoResponse>().Subject;
        leido.CarreraOrigen.Should().Be("Ingeniería Industrial");
        leido.Materias.Should().Contain(m => m.Nombre == "Física Aplicada" && !m.Aprobada);
        leido.Solicitudes.Should().HaveCount(4);
        _db.Expedientes.Should().BeEmpty();
    }

    [Fact]
    public async Task Carrera_que_no_existe_en_el_catalogo_impide_registrar()
    {
        var lectura = Siha.Certificados.LectorCertificadoSiha.Leer(File.ReadAllBytes(
            Path.Combine(Pruebas.RaizRepositorio(), "documentos-prueba", "certificados", "SIHA-TI-SW-cambio-de-carrera.pdf")));
        lectura.EsValido.Should().BeTrue();
        var verificador = new VerificadorFalso(_usuarios);

        (await verificador.ResolverCarreraAsync("Arquitectura")).Should().BeNull();
    }

    [Fact]
    public async Task Revisor_que_no_es_docente_o_no_existe_impide_registrar()
    {
        _usuarios["mvaca@uta.edu.ec"] = (false, true);
        _usuarios.Remove("hnaranjo@uta.edu.ec");

        var resultado = await Controlador().Registrar(Certificado("SIHA-TI-SW-cambio-de-carrera.pdf"), CancellationToken.None);

        var cuerpo = resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject.Value!;
        var detalles = Detalles(cuerpo);
        detalles.Should().Contain(d => d.Contains("hnaranjo@uta.edu.ec") && d.Contains("no está registrado"));
        detalles.Should().Contain(d => d.Contains("mvaca@uta.edu.ec") && d.Contains("DOCENTE"));
        _db.Expedientes.Should().BeEmpty();
    }

    [Fact]
    public async Task Movilidad_externa_registra_el_certificado_con_sus_silabos()
    {
        var resultado = await Controlador().Registrar(CertificadoExterno(), CancellationToken.None,
            new List<IFormFile> { Silabo("ESPOCH-fisica.pdf") }, new List<string> { "Física" });

        var respuesta = resultado.Result.Should().BeOfType<AcceptedResult>().Subject.Value
            .Should().BeOfType<SolicitudCertificadoResponse>().Subject;
        respuesta.Expediente.TipoTramite.Should().Be(TiposTramite.Externo);

        var silabo = respuesta.Certificado.Silabos.Should().ContainSingle().Subject;
        silabo.Materia.Should().Be("Física");
        silabo.Unidades.Should().Be(4);
        silabo.HorasTotales.Should().Be(128);

        var documentos = _db.ExpedienteDocumentos.ToList();
        documentos.Should().HaveCount(2, "el certificado y un sílabo");
        documentos.Should().ContainSingle(d => d.Tipo == TiposDocumentoExpediente.Certificado && d.MateriaOrigen == null);
        documentos.Should().ContainSingle(d => d.Tipo == TiposDocumentoExpediente.SilaboExterno && d.MateriaOrigen == "Física");

        var evento = _publicados.Should().ContainSingle().Subject;
        evento.Documentos.Should().HaveCount(2);
        evento.Documentos.Should().ContainSingle(d => d.Tipo == TiposDocumentoExpediente.SilaboExterno && d.MateriaOrigen == "Física");
    }

    [Fact]
    public async Task Silabos_en_cambio_de_carrera_se_rechazan()
    {
        var resultado = await Controlador().Registrar(Certificado("SIHA-TI-SW-cambio-de-carrera.pdf"), CancellationToken.None,
            new List<IFormFile> { Silabo("ESPOCH-fisica.pdf") }, new List<string> { "Física" });

        var cuerpo = resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject.Value!;
        cuerpo.GetType().GetProperty("mensaje")!.GetValue(cuerpo)!.ToString()
            .Should().Contain("movilidad externa");
        _db.Expedientes.Should().BeEmpty();
    }

    [Fact]
    public async Task Silabo_de_una_materia_que_no_se_pide_se_rechaza()
    {
        var resultado = await Controlador().Registrar(CertificadoExterno(), CancellationToken.None,
            new List<IFormFile> { Silabo("ESPOCH-fisica.pdf") }, new List<string> { "Química General" });

        var cuerpo = resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject.Value!;
        Detalles(cuerpo).Should().ContainSingle(d => d.Contains("Química General") && d.Contains("no es una de las materias"));
        _db.Expedientes.Should().BeEmpty();
    }

    [Fact]
    public async Task Silabo_sin_horas_se_rechaza_con_mensaje_claro()
    {
        var resultado = await Controlador().Registrar(CertificadoExterno(), CancellationToken.None,
            new List<IFormFile> { Silabo("silabo-sin-horas.pdf") }, new List<string> { "Física" });

        var cuerpo = resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject.Value!;
        Detalles(cuerpo).Should().ContainSingle(d => d.Contains("Física") && d.Contains("no indica las horas"));
        _db.Expedientes.Should().BeEmpty();
        Directory.GetFiles(_directorio, "*", SearchOption.AllDirectories).Should().BeEmpty("no se guarda nada si un sílabo está mal");
    }

    [Fact]
    public async Task Validar_tambien_lee_los_silabos_adjuntos()
    {
        var resultado = await Controlador().Validar(CertificadoExterno(), CancellationToken.None,
            new List<IFormFile> { Silabo("ESPOCH-algebra-lineal.pdf") }, new List<string> { "Álgebra Lineal" });

        var leido = resultado.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<CertificadoLeidoResponse>().Subject;
        leido.Silabos.Should().ContainSingle().Which.Materia.Should().Be("Álgebra Lineal");
        _db.Expedientes.Should().BeEmpty();
        Directory.GetFiles(_directorio, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task El_rechazo_trae_la_lista_de_verificacion_por_seccion()
    {
        var resultado = await Controlador().Validar(Certificado("SIHA-con-errores.pdf"), CancellationToken.None);

        var rechazo = resultado.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject.Value
            .Should().BeOfType<RechazoCertificadoResponse>().Subject;
        rechazo.Tipo.Should().Be(TiposProblema.Regla);
        rechazo.Mensaje.Should().StartWith("El certificado tiene datos que no cumplen las reglas.");
        rechazo.Secciones.Select(s => s.Seccion).Should().Equal(
            "Certificado", "Estudiante", "Trámite", "Revisores", "Historial", "Solicitudes");
        rechazo.Secciones.Single(s => s.Seccion == "Estudiante").Ok.Should().BeFalse("la cédula está mal");
        rechazo.Secciones.Single(s => s.Seccion == "Certificado").Ok.Should().BeTrue();
        rechazo.Problemas.Should().OnlyContain(p => p.Tipo == TiposProblema.Regla);
    }

    [Fact]
    public async Task Un_pdf_que_no_es_el_certificado_no_muestra_secciones()
    {
        var resultado = await Controlador().Validar(Certificado("plan-analitico-no-valido.pdf"), CancellationToken.None);

        var rechazo = (RechazoCertificadoResponse)((UnprocessableEntityObjectResult)resultado.Result!).Value!;
        rechazo.Tipo.Should().Be(TiposProblema.NoEsCertificado);
        rechazo.Mensaje.Should().StartWith("Este PDF no es el certificado SIHA.");
        rechazo.Secciones.Should().BeEmpty();
    }

    [Fact]
    public async Task Campos_sin_llenar_se_resumen_en_un_mensaje_sencillo()
    {
        var pdf = Pruebas.Archivo(File.ReadAllBytes(Path.Combine(Pruebas.RaizRepositorio(), "documentos-prueba",
            "certificados-pruebas", "E20-campos-sin-llenar.pdf")), nombreArchivo: "E20.pdf");

        var rechazo = (RechazoCertificadoResponse)((UnprocessableEntityObjectResult)(await Controlador().Validar(pdf, CancellationToken.None)).Result!).Value!;

        rechazo.Tipo.Should().Be(TiposProblema.CampoVacio);
        rechazo.Mensaje.Should().StartWith("El certificado tiene campos sin llenar.");
        rechazo.Secciones.Where(s => !s.Ok).Select(s => s.Seccion).Should().BeEquivalentTo("Estudiante", "Historial");
    }

    [Fact]
    public async Task Revisor_que_no_existe_aparece_en_la_seccion_revisores()
    {
        _usuarios.Remove("mvaca@uta.edu.ec");

        var rechazo = (RechazoCertificadoResponse)((UnprocessableEntityObjectResult)(await Controlador()
            .Validar(Certificado("SIHA-TI-SW-cambio-de-carrera.pdf"), CancellationToken.None)).Result!).Value!;

        var revisores = rechazo.Secciones.Single(s => s.Seccion == "Revisores");
        revisores.Ok.Should().BeFalse();
        revisores.Problemas.Should().ContainSingle(p => p.Contains("mvaca@uta.edu.ec") && p.Contains("no está registrado"));
    }

    [Fact]
    public async Task La_vista_previa_sugiere_el_nombre_oficial_de_la_materia_de_destino()
    {
        // La malla de Software no tiene "Estructura de Datos" con ese nombre, sino "ESTRUCTURAS DE DATOS".
        _mallas["SW"] = new[]
        {
            "ALGORITMOS Y LOGICA DE PROGRAMACION", "FISICA", "ALGEBRA LINEAL", "ESTRUCTURAS DE DATOS",
            "PROGRAMACION ORIENTADA A OBJETOS"
        };

        var leido = (CertificadoLeidoResponse)((OkObjectResult)(await Controlador()
            .Validar(Certificado("SIHA-TI-SW-cambio-de-carrera.pdf"), CancellationToken.None)).Result!).Value!;

        var aviso = leido.Avisos.Should().ContainSingle().Subject;
        aviso.MateriaDestino.Should().Be("Estructura de Datos");
        aviso.Sugerencia.Should().Be("Estructuras de Datos");
        aviso.Mensaje.Should().Contain("¿Quiso decir «Estructuras de Datos»?");
    }

    [Fact]
    public async Task Si_el_catalogo_no_responde_no_hay_avisos_y_se_puede_registrar()
    {
        var leido = (CertificadoLeidoResponse)((OkObjectResult)(await Controlador()
            .Validar(Certificado("SIHA-TI-SW-cambio-de-carrera.pdf"), CancellationToken.None)).Result!).Value!;

        leido.Avisos.Should().BeEmpty();
    }

    [Fact]
    public async Task Cada_docente_ve_solo_las_solicitudes_que_le_asignaron()
    {
        await Controlador().Registrar(Certificado("SIHA-TI-SW-cambio-de-carrera.pdf"), CancellationToken.None);      // hnaranjo + mvaca
        await Controlador().Registrar(Certificado("SIHA-IND-SW-cambio-de-carrera.pdf"), CancellationToken.None);  // mvaca + jcastro
        var envio = new ExpedienteEnvioService(_db, Pruebas.CrearAlmacenamiento(Pruebas.OpcionesArchivos(_directorio)), _publicador.Object,
            Options.Create(Pruebas.OpcionesArchivos(_directorio)), Options.Create(new RepublicacionOptions()), NullLogger<ExpedienteEnvioService>.Instance);

        ExpedientesController Como(string correo) => new(_db, envio, NullLogger<ExpedientesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Email, correo), new Claim(ClaimTypes.Role, RolesSistema.Docente)
                    }, "prueba"))
                }
            }
        };

        var deNaranjo = (await Como("hnaranjo@uta.edu.ec").Listar(null, CancellationToken.None)).Result
            .Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeAssignableTo<IEnumerable<ExpedienteResponse>>().Subject.ToList();
        var deVaca = (await Como("mvaca@uta.edu.ec").Listar(null, CancellationToken.None)).Result
            .Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeAssignableTo<IEnumerable<ExpedienteResponse>>().Subject.ToList();

        deNaranjo.Should().ContainSingle().Which.NumeroCertificado.Should().Be("SIHA-2026-0147");
        deVaca.Should().HaveCount(2);
        var ajeno = _db.Expedientes.Single(x => x.NumeroCertificado == "SIHA-2026-0148").Id;
        (await Como("hnaranjo@uta.edu.ec").Obtener(ajeno, CancellationToken.None)).Result.Should().BeOfType<NotFoundResult>();
    }
}
