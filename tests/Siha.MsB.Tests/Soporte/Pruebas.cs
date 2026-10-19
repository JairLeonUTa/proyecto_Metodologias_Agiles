using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Options;
using Siha.MsB.Api.Services;

namespace Siha.MsB.Tests.Soporte;

/// <summary>Utilidades compartidas por las pruebas del Microservicio B (S1-S03).</summary>
public static class Pruebas
{
    /// <summary>Contenido mínimo de un PDF válido (firma binaria %PDF).</summary>
    public const string CabeceraPdf = "%PDF-1.7\n";

    public static byte[] BytesPdf(string cuerpo = "1 0 obj << /Type /Catalog >> endobj\n%%EOF\n")
        => System.Text.Encoding.ASCII.GetBytes(CabeceraPdf + cuerpo);

    /// <summary>Crea un archivo de formulario (multipart) con el contenido indicado.</summary>
    public static IFormFile Archivo(
        byte[]? contenido = null,
        string nombreArchivo = "certificado-notas.pdf",
        string contentType = "application/pdf",
        string campo = "archivo")
    {
        var bytes = contenido ?? BytesPdf();
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, campo, nombreArchivo)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    /// <summary>Directorio temporal aislado para cada prueba de almacenamiento.</summary>
    public static string DirectorioTemporal()
    {
        var ruta = Path.Combine(Path.GetTempPath(), "siha-msb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    public static ArchivosOptions OpcionesArchivos(string rutaAlmacenamiento, long tamanoMaximoBytes = 5 * 1024 * 1024)
        => new()
        {
            TamanoMaximoBytes = tamanoMaximoBytes,
            RutaAlmacenamiento = rutaAlmacenamiento,
            ExtensionPermitida = ".pdf",
            MimePermitidos = new[] { "application/pdf", "application/x-pdf" }
        };

    public static ArchivoStorageService CrearAlmacenamiento(ArchivosOptions opciones)
        => new(Options.Create(opciones), NullLogger<ArchivoStorageService>.Instance);

    /// <summary>Contexto de DB B respaldado por el proveedor InMemory de EF Core.</summary>
    public static SihaDbContextB CrearContextoB(string? nombreBaseDatos = null)
    {
        var options = new DbContextOptionsBuilder<SihaDbContextB>()
            .UseInMemoryDatabase(nombreBaseDatos ?? Guid.NewGuid().ToString("N"))
            .Options;

        return new SihaDbContextB(options);
    }

    /// <summary>
    /// Localiza la raíz del repositorio (carpeta que contiene SIHA-IA.sln) subiendo desde el
    /// directorio de salida de las pruebas. Se usa para validar la configuración de Ocelot.
    /// </summary>
    public static string RaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "SIHA-IA.sln")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new InvalidOperationException(
            $"No se encontró SIHA-IA.sln subiendo desde '{AppContext.BaseDirectory}'.");
    }
}
