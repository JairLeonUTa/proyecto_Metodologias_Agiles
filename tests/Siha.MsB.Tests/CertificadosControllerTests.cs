using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
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
        return new CertificadosController(_db, almacenamiento, envio, NullLogger<CertificadosController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static IFormFile Certificado(string nombre) => Pruebas.Archivo(
        File.ReadAllBytes(Path.Combine(Pruebas.RaizRepositorio(), "documentos-prueba", "certificados", nombre)),
        nombreArchivo: nombre);

    [Fact]
    public async Task Un_solo_pdf_crea_el_expediente_completo_y_lo_envia()
    {
        var resultado = await Controlador().Registrar(Certificado("SIHA-IND-SW-completo.pdf"), CancellationToken.None);

        var respuesta = resultado.Result.Should().BeOfType<AcceptedResult>().Subject.Value
            .Should().BeOfType<SolicitudCertificadoResponse>().Subject;
        respuesta.Publicado.Should().BeTrue();

        var e = respuesta.Expediente;
        e.TipoTramite.Should().Be(TiposTramite.Interno);
        e.RutaNormativa.Should().Be(RutasNormativas.CambioCarreraOu);
        e.CarreraOrigenCodigo.Should().Be("IND");
        e.CarreraDestinoCodigo.Should().Be("SW");
        e.NumeroCertificado.Should().Be("SIHA-2026-0147");
        e.EstudianteNombre.Should().Be("Pérez Gómez Ana Lucía");
        e.Estado.Should().Be(ExpedienteEstados.PendienteContenidos);
        e.Documentos.Should().ContainSingle();

        var fisica = e.Asignaturas.Single(a => a.CodigoOrigen == "IND105");
        fisica.Semestre.Should().Be(1);
        fisica.Creditos.Should().Be(4);
        fisica.Horas.Should().Be(192);
        fisica.FechaAprobacionCertificada.Should().Be(new DateOnly(2024, 2, 16));
        fisica.NumeroMatriculaDestino.Should().Be(1);
        fisica.CodigoDestino.Should().BeNull("el destino lo propone el análisis automático");

        var evento = _publicados.Should().ContainSingle().Subject;
        evento.AsignaturasOrigen.Should().BeEquivalentTo("IND105", "IND108", "IND205");
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
        var detalles = (IEnumerable<string>)cuerpo.GetType().GetProperty("detalles")!.GetValue(cuerpo)!;
        detalles.Should().Contain(d => d.Contains("no es aprobatoria"));
    }

    [Fact]
    public async Task El_mismo_certificado_no_se_registra_dos_veces()
    {
        await Controlador().Registrar(Certificado("SIHA-IND-SW-completo.pdf"), CancellationToken.None);

        var segunda = await Controlador().Registrar(Certificado("SIHA-IND-SW-completo.pdf"), CancellationToken.None);

        segunda.Result.Should().BeOfType<ConflictObjectResult>();
        _db.Expedientes.Should().ContainSingle();
    }

    [Fact]
    public async Task Validar_lee_el_certificado_sin_registrar()
    {
        var resultado = await Controlador().Validar(Certificado("SIHA-UCuenca-SW-externo.pdf"), CancellationToken.None);

        var leido = resultado.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<CertificadoLeidoResponse>().Subject;
        leido.TipoTramite.Should().Be(TiposTramite.Externo);
        leido.InstitucionOrigen.Should().Be("Universidad de Cuenca");
        leido.Asignaturas.Should().HaveCount(2);
        _db.Expedientes.Should().BeEmpty();
    }
}
