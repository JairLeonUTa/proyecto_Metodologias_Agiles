using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Siha.MsB.Api.Services;
using Siha.MsB.Tests.Soporte;

namespace Siha.MsB.Tests;

/// <summary>
/// Pruebas de la validación de PDF y del almacenamiento seguro (S1-S05 / S1-S06, tarea S1-S03).
/// </summary>
public class ArchivoStorageServiceTests : IDisposable
{
    private readonly string _directorio = Pruebas.DirectorioTemporal();
    private readonly ArchivoStorageService _servicio;

    public ArchivoStorageServiceTests()
    {
        _servicio = Pruebas.CrearAlmacenamiento(Pruebas.OpcionesArchivos(_directorio));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }
    }

    // --- Validación de PDF (positiva) ---------------------------------------------------

    [Fact]
    public void Acepta_un_pdf_con_firma_valida()
    {
        var archivo = Pruebas.Archivo();

        var resultado = _servicio.ValidarPdf(archivo);

        resultado.Valido.Should().BeTrue();
        resultado.Error.Should().BeNull();
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/x-pdf")]
    [InlineData("application/pdf; charset=binary")]
    public void Acepta_los_tipos_mime_de_pdf_admitidos(string contentType)
    {
        var archivo = Pruebas.Archivo(contentType: contentType);

        _servicio.ValidarPdf(archivo).Valido.Should().BeTrue();
    }

    // --- Validación de PDF (negativa) ---------------------------------------------------

    [Fact]
    public void Rechaza_cuando_no_se_recibe_archivo()
    {
        var resultado = _servicio.ValidarPdf(null);

        resultado.Valido.Should().BeFalse();
        resultado.Error.Should().Contain("adjuntar");
    }

    [Fact]
    public void Rechaza_archivos_vacios()
    {
        var archivo = Pruebas.Archivo(Array.Empty<byte>());

        var resultado = _servicio.ValidarPdf(archivo);

        resultado.Valido.Should().BeFalse();
        resultado.Error.Should().Contain("vacío");
    }

    [Fact]
    public void Rechaza_archivos_que_superan_el_tamano_maximo_configurado()
    {
        var opciones = Pruebas.OpcionesArchivos(_directorio, tamanoMaximoBytes: 128);
        var servicio = Pruebas.CrearAlmacenamiento(opciones);
        var archivo = Pruebas.Archivo(Pruebas.BytesPdf(new string('x', 500)));

        var resultado = servicio.ValidarPdf(archivo);

        resultado.Valido.Should().BeFalse();
        resultado.Error.Should().Contain("tamaño máximo");
    }

    [Fact]
    public void Rechaza_extensiones_que_no_sean_pdf()
    {
        var archivo = Pruebas.Archivo(nombreArchivo: "certificado.docx");

        var resultado = _servicio.ValidarPdf(archivo);

        resultado.Valido.Should().BeFalse();
        resultado.Error.Should().Contain(".pdf");
    }

    [Fact]
    public void Rechaza_tipos_mime_incorrectos()
    {
        var archivo = Pruebas.Archivo(contentType: "image/png");

        var resultado = _servicio.ValidarPdf(archivo);

        resultado.Valido.Should().BeFalse();
        resultado.Error.Should().Contain("MIME");
    }

    [Fact]
    public void Rechaza_archivos_sin_la_firma_binaria_pdf()
    {
        var archivo = Pruebas.Archivo(Encoding.ASCII.GetBytes("Esto no es un PDF, es un documento de texto."));

        var resultado = _servicio.ValidarPdf(archivo);

        resultado.Valido.Should().BeFalse();
        resultado.Error.Should().Contain("%PDF");
    }

    [Fact]
    public void Rechaza_la_firma_pdf_falsa_recortada()
    {
        var archivo = Pruebas.Archivo(Encoding.ASCII.GetBytes("%PD"));

        var resultado = _servicio.ValidarPdf(archivo);

        resultado.Valido.Should().BeFalse();
    }

    // --- Cálculo de hash SHA-256 --------------------------------------------------------

    [Fact]
    public void El_hash_sha256_coincide_con_el_valor_conocido()
    {
        using var contenido = new MemoryStream(Encoding.ASCII.GetBytes("abc"));

        var hash = _servicio.CalcularHashSha256(contenido);

        hash.Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void El_hash_de_un_contenido_vacio_es_el_esperado()
    {
        using var contenido = new MemoryStream(Array.Empty<byte>());

        var hash = _servicio.CalcularHashSha256(contenido);

        hash.Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    // --- Almacenamiento seguro ----------------------------------------------------------

    [Fact]
    public async Task Guarda_el_archivo_con_nombre_generado_y_hash_verificable()
    {
        var bytes = Pruebas.BytesPdf();
        var archivo = Pruebas.Archivo(bytes, nombreArchivo: "../../intento-traversal.pdf");

        var almacenado = await _servicio.GuardarAsync(archivo);

        // El nombre enviado por el cliente nunca se usa para escribir en disco.
        almacenado.NombreAlmacenado.Should().NotContain("..");
        almacenado.NombreAlmacenado.Should().EndWith(".pdf");
        almacenado.NombreAlmacenado.Should().NotBe(archivo.FileName);

        // El archivo queda dentro del directorio configurado.
        var raiz = Path.GetFullPath(_directorio);
        Path.GetFullPath(almacenado.RutaFisica).Should().StartWith(raiz);
        File.Exists(almacenado.RutaFisica).Should().BeTrue();

        // El hash corresponde exactamente al contenido escrito.
        var hashEsperado = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(almacenado.RutaFisica)))
            .ToLowerInvariant();
        almacenado.HashSha256.Should().Be(hashEsperado);
        almacenado.TamanoBytes.Should().Be(bytes.Length);

        // La ruta persistida es relativa al directorio configurado.
        almacenado.RutaAlmacenamiento.Should().NotBeNullOrWhiteSpace();
        Path.IsPathRooted(almacenado.RutaAlmacenamiento).Should().BeFalse();
        File.Exists(Path.Combine(raiz, almacenado.RutaAlmacenamiento)).Should().BeTrue();
    }

    [Fact]
    public async Task Dos_cargas_del_mismo_archivo_generan_nombres_distintos_con_el_mismo_hash()
    {
        var bytes = Pruebas.BytesPdf();

        var primera = await _servicio.GuardarAsync(Pruebas.Archivo(bytes));
        var segunda = await _servicio.GuardarAsync(Pruebas.Archivo(bytes));

        primera.NombreAlmacenado.Should().NotBe(segunda.NombreAlmacenado);
        primera.HashSha256.Should().Be(segunda.HashSha256);
    }
}
