using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Siha.Contracts;
using Siha.MsB.Api.Contracts.Dtos;
using Siha.MsB.Api.Controllers;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Domain;
using Siha.MsB.Api.Domain.Entities;
using Siha.MsB.Tests.Soporte;

namespace Siha.MsB.Tests;

/// <summary>
/// Pruebas de la carga de documentos PDF (S1-S05 / S1-S06) a través de
/// <c>POST /api/expedientes/{id}/documentos</c> y de su consulta con estado de recepción.
/// </summary>
public class DocumentosControllerTests : IDisposable
{
    private readonly string _directorio = Pruebas.DirectorioTemporal();
    private readonly SihaDbContextB _db = Pruebas.CrearContextoB();
    private readonly Expediente _expediente;

    public DocumentosControllerTests()
    {
        _expediente = new Expediente
        {
            Codigo = "EXP-20261018-TEST01",
            TipoTramite = TiposTramite.Interno,
            RutaNormativa = RutasNormativas.CambioCarreraOu,
            CarreraOrigenCodigo = "SW",
            CarreraDestinoCodigo = "TI",
            Estado = ExpedienteEstados.PendienteContenidos
        };

        _db.Expedientes.Add(_expediente);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }
    }

    private DocumentosController CrearControlador(long tamanoMaximoBytes = 5 * 1024 * 1024) =>
        new(
            _db,
            Pruebas.CrearAlmacenamiento(Pruebas.OpcionesArchivos(_directorio, tamanoMaximoBytes)),
            NullLogger<DocumentosController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    [Fact]
    public async Task Carga_un_pdf_valido_y_persiste_los_metadatos_con_hash()
    {
        var bytes = Pruebas.BytesPdf();
        var controlador = CrearControlador();

        var resultado = await controlador.Subir(
            _expediente.Id,
            Pruebas.Archivo(bytes, nombreArchivo: "certificado.pdf"),
            CancellationToken.None);

        var creado = resultado.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = creado.Value.Should().BeOfType<DocumentoResponse>().Subject;

        dto.EstadoRecepcion.Should().Be(EstadosRecepcionDocumento.Validado);
        dto.TamanoBytes.Should().Be(bytes.Length);
        dto.HashSha256.Should().HaveLength(64);
        dto.NombreArchivo.Should().Be("certificado.pdf");
        dto.NombreAlmacenado.Should().NotBe("certificado.pdf");

        var documento = _db.ExpedienteDocumentos.Single(d => d.ExpedienteId == _expediente.Id);
        documento.HashSha256.Should().Be(dto.HashSha256);
        documento.EstadoRecepcion.Should().Be(EstadosRecepcionDocumento.Validado);
        documento.ObservacionValidacion.Should().BeNull();

        // El archivo quedó realmente almacenado bajo el directorio configurado.
        File.Exists(Path.Combine(Path.GetFullPath(_directorio), documento.RutaAlmacenamiento)).Should().BeTrue();
    }

    [Fact]
    public async Task Devuelve_400_y_audita_el_intento_cuando_el_archivo_no_es_un_pdf()
    {
        var contenidoFalso = System.Text.Encoding.ASCII.GetBytes("contenido malicioso");
        var controlador = CrearControlador();

        var resultado = await controlador.Subir(
            _expediente.Id,
            Pruebas.Archivo(contenidoFalso, nombreArchivo: "notas.pdf"),
            CancellationToken.None);

        resultado.Result.Should().BeOfType<BadRequestObjectResult>();

        var documento = _db.ExpedienteDocumentos.Single(d => d.ExpedienteId == _expediente.Id);
        documento.EstadoRecepcion.Should().Be(EstadosRecepcionDocumento.Rechazado);
        documento.ObservacionValidacion.Should().Contain("%PDF");
        documento.RutaAlmacenamiento.Should().BeEmpty();

        Directory.GetFiles(_directorio, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Devuelve_400_cuando_no_se_envia_archivo()
    {
        var controlador = CrearControlador();

        var resultado = await controlador.Subir(_expediente.Id, null, CancellationToken.None);

        resultado.Result.Should().BeOfType<BadRequestObjectResult>();
        _db.ExpedienteDocumentos.Should().BeEmpty();
    }

    [Fact]
    public async Task Devuelve_400_cuando_se_supera_el_tamano_maximo()
    {
        var controlador = CrearControlador(tamanoMaximoBytes: 64);
        var pdfGrande = Pruebas.BytesPdf(new string('y', 500));

        var resultado = await controlador.Subir(
            _expediente.Id,
            Pruebas.Archivo(pdfGrande, nombreArchivo: "grande.pdf"),
            CancellationToken.None);

        resultado.Result.Should().BeOfType<BadRequestObjectResult>();
        Directory.GetFiles(_directorio, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Devuelve_404_cuando_el_expediente_no_existe()
    {
        var controlador = CrearControlador();

        var resultado = await controlador.Subir(Guid.NewGuid(), Pruebas.Archivo(), CancellationToken.None);

        resultado.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Lista_los_metadatos_con_el_estado_de_recepcion()
    {
        var controlador = CrearControlador();
        await controlador.Subir(_expediente.Id, Pruebas.Archivo(), CancellationToken.None);
        await controlador.Subir(
            _expediente.Id,
            Pruebas.Archivo(System.Text.Encoding.ASCII.GetBytes("no pdf"), nombreArchivo: "otro.pdf"),
            CancellationToken.None);

        var resultado = await controlador.Listar(_expediente.Id, null, CancellationToken.None);

        var ok = resultado.Result.Should().BeOfType<OkObjectResult>().Subject;
        var respuesta = ok.Value.Should().BeOfType<DocumentosExpedienteResponse>().Subject;

        respuesta.ExpedienteId.Should().Be(_expediente.Id);
        respuesta.CodigoExpediente.Should().Be(_expediente.Codigo);
        respuesta.TotalDocumentos.Should().Be(2);
        respuesta.DocumentosValidados.Should().Be(1);
        respuesta.DocumentosRechazados.Should().Be(1);
        respuesta.Documentos.Should().Contain(d => d.EstadoRecepcion == EstadosRecepcionDocumento.Validado);
        respuesta.Documentos.Should().Contain(d => d.EstadoRecepcion == EstadosRecepcionDocumento.Rechazado);
    }

    [Fact]
    public async Task Permite_filtrar_los_documentos_por_estado_de_recepcion()
    {
        var controlador = CrearControlador();
        await controlador.Subir(_expediente.Id, Pruebas.Archivo(), CancellationToken.None);
        await controlador.Subir(
            _expediente.Id,
            Pruebas.Archivo(System.Text.Encoding.ASCII.GetBytes("no pdf"), nombreArchivo: "otro.pdf"),
            CancellationToken.None);

        var resultado = await controlador.Listar(
            _expediente.Id, EstadosRecepcionDocumento.Rechazado, CancellationToken.None);

        var respuesta = resultado.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<DocumentosExpedienteResponse>().Subject;

        respuesta.Documentos.Should().HaveCount(1);
        respuesta.Documentos.Should().OnlyContain(d => d.EstadoRecepcion == EstadosRecepcionDocumento.Rechazado);
    }

    [Fact]
    public async Task Devuelve_404_al_listar_documentos_de_un_expediente_inexistente()
    {
        var controlador = CrearControlador();

        var resultado = await controlador.Listar(Guid.NewGuid(), null, CancellationToken.None);

        resultado.Result.Should().BeOfType<NotFoundObjectResult>();
    }
}
