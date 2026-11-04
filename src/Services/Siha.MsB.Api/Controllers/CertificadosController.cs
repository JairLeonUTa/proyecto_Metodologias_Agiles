using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siha.Certificados;
using Siha.Contracts;
using Siha.MsB.Api.Contracts.Dtos;
using Siha.MsB.Api.Data;
using Siha.MsB.Api.Domain;
using Siha.MsB.Api.Domain.Entities;
using Siha.MsB.Api.Services;

namespace Siha.MsB.Api.Controllers;

/// <summary>
/// Registro de una solicitud a partir de un único PDF: el certificado de homologación SIHA
/// (formato SIHA-CH-01), que llega preaprobado y trae todos los datos.
///
/// Flujo en una sola llamada: valida el PDF → lee y valida el certificado → crea el expediente con
/// estudiante, trámite, carreras y asignaturas (semestre, créditos, horas, fecha, nota, matrícula) →
/// guarda el PDF → lo envía a MS C. El operador no digita años, materias ni carreras.
/// Si el documento no cumple la plantilla se rechaza con 422 y la lista de errores, sin crear nada.
/// </summary>
[ApiController]
[Route("api/expedientes/certificado")]
[Authorize(Roles = $"{RolesSistema.Operador},{RolesSistema.Administrador}")]
public class CertificadosController : ControllerBase
{
    private readonly SihaDbContextB _db;
    private readonly IArchivoStorageService _archivos;
    private readonly ExpedienteEnvioService _envio;
    private readonly IVerificadorDocentes _docentes;
    private readonly ILogger<CertificadosController> _logger;

    public CertificadosController(
        SihaDbContextB db,
        IArchivoStorageService archivos,
        ExpedienteEnvioService envio,
        IVerificadorDocentes docentes,
        ILogger<CertificadosController> logger)
    {
        _db = db;
        _archivos = archivos;
        _envio = envio;
        _docentes = docentes;
        _logger = logger;
    }

    /// <summary>Lee el certificado sin registrar nada (vista previa para el operador).</summary>
    [HttpPost("validar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CertificadoLeidoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CertificadoLeidoResponse>> Validar(IFormFile? archivo, CancellationToken cancellationToken)
    {
        var (certificado, error) = await LeerAsync(archivo, cancellationToken);
        return error ?? Ok(ADto(certificado!));
    }

    /// <summary>
    /// Registra y envía la solicitud a partir del certificado. 202 con el expediente creado;
    /// 422 si el PDF no cumple la plantilla; 409 si el certificado ya fue registrado.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(SolicitudCertificadoResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SolicitudCertificadoResponse>> Registrar(IFormFile? archivo, CancellationToken cancellationToken)
    {
        var (certificado, error) = await LeerAsync(archivo, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var c = certificado!;
        var existente = await _db.Expedientes.AsNoTracking()
            .FirstOrDefaultAsync(e => e.NumeroCertificado == c.NumeroCertificado, cancellationToken);
        if (existente is not null)
        {
            return Conflict(new
            {
                mensaje = $"El certificado {c.NumeroCertificado} ya fue registrado en el expediente {existente.Codigo}.",
                expedienteId = existente.Id,
                codigo = existente.Codigo
            });
        }

        DocumentoAlmacenado almacenado;
        try
        {
            almacenado = await _archivos.GuardarAsync(archivo!, cancellationToken);
        }
        catch (AlmacenamientoNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = ex.Message });
        }

        var expediente = ConstruirExpediente(c);
        expediente.Documentos.Add(new ExpedienteDocumento
        {
            NombreArchivo = Recortar(almacenado.NombreOriginal, 255),
            NombreAlmacenado = almacenado.NombreAlmacenado,
            RutaAlmacenamiento = almacenado.RutaAlmacenamiento,
            HashSha256 = almacenado.HashSha256,
            TamanoBytes = almacenado.TamanoBytes,
            MimeType = almacenado.MimeType,
            EstadoRecepcion = EstadosRecepcionDocumento.Validado,
            FechaCargaUtc = DateTimeOffset.UtcNow
        });

        _db.Expedientes.Add(expediente);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Dos cargas simultáneas del mismo certificado: el índice único decide; se compensa el archivo.
            _archivos.Eliminar(almacenado.RutaAlmacenamiento);
            return Conflict(new { mensaje = $"El certificado {c.NumeroCertificado} ya fue registrado." });
        }

        _logger.LogInformation(
            "Solicitud creada desde certificado SIHA. Certificado={Certificado} ExpedienteId={ExpedienteId} Asignaturas={Asignaturas}",
            c.NumeroCertificado, expediente.Id, c.Asignaturas.Count);

        var envio = await _envio.EnviarAsync(expediente.Id, ModoEnvio.Envio, cancellationToken);
        var registrado = await _db.Expedientes.AsNoTracking()
            .Include(e => e.Documentos)
            .Include(e => e.Asignaturas)
            .FirstAsync(e => e.Id == expediente.Id, cancellationToken);

        return Accepted(new SolicitudCertificadoResponse(
            ExpedientesController.AExpedienteResponse(registrado),
            ADto(c),
            registrado.EventoPublicado,
            envio.Mensaje));
    }

    private async Task<(CertificadoSiha? Certificado, ActionResult? Error)> LeerAsync(
        IFormFile? archivo, CancellationToken cancellationToken)
    {
        var validacion = _archivos.ValidarPdf(archivo);
        if (!validacion.Valido)
        {
            return (null, BadRequest(new { mensaje = validacion.Error, tamanoMaximoBytes = _archivos.TamanoMaximoBytes }));
        }

        using var memoria = new MemoryStream();
        await archivo!.CopyToAsync(memoria, cancellationToken);
        var lectura = LectorCertificadoSiha.Leer(memoria.ToArray());
        if (!lectura.EsValido)
        {
            _logger.LogWarning("Certificado SIHA rechazado. Archivo={Archivo} Errores={Errores}",
                archivo.FileName, string.Join(" | ", lectura.Errores));
            return (null, UnprocessableEntity(new
            {
                mensaje = "El PDF no cumple la plantilla oficial del certificado SIHA (SIHA-CH-01). Corrija el documento en Secretaría y vuelva a cargarlo.",
                detalles = lectura.Errores
            }));
        }

        // Los dos revisores deben existir en la base de usuarios (MS A) con rol DOCENTE y activos.
        IReadOnlyList<DocenteRevisorVerificado> verificados;
        try
        {
            verificados = await _docentes.VerificarAsync(lectura.Certificado!.Revisores.Select(r => r.Correo), cancellationToken);
        }
        catch (VerificacionDocentesNoDisponibleException ex)
        {
            _logger.LogError(ex, "No se pudo verificar los docentes revisores.");
            return (null, StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = ex.Message }));
        }

        var problemas = new List<string>();
        foreach (var revisor in lectura.Certificado.Revisores)
        {
            var v = verificados.FirstOrDefault(d => string.Equals(d.Correo, revisor.Correo, StringComparison.OrdinalIgnoreCase));
            if (v is null || !v.Existe)
                problemas.Add($"El docente revisor {revisor.Nombre} ({revisor.Correo}) no está registrado en el sistema.");
            else if (!v.EsDocente)
                problemas.Add($"El usuario {revisor.Correo} no tiene el rol DOCENTE; no puede ser revisor.");
            else if (!v.Activo)
                problemas.Add($"El docente {revisor.Correo} está inactivo; no puede ser revisor.");
        }

        if (problemas.Count > 0)
        {
            return (null, UnprocessableEntity(new
            {
                mensaje = "Los docentes revisores del certificado deben ser usuarios activos con rol DOCENTE.",
                detalles = problemas
            }));
        }

        // Se usa el nombre registrado en el sistema para el docente.
        var revisores = lectura.Certificado.Revisores
            .Select(r => r with
            {
                Nombre = verificados.First(d => string.Equals(d.Correo, r.Correo, StringComparison.OrdinalIgnoreCase)).NombreCompleto is { Length: > 0 } n ? n : r.Nombre
            })
            .ToList();

        return (lectura.Certificado with { Revisores = revisores }, null);
    }

    internal static Expediente ConstruirExpediente(CertificadoSiha c)
    {
        var expediente = new Expediente
        {
            Codigo = $"EXP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            TipoTramite = c.TipoTramite,
            RutaNormativa = c.RutaNormativa,
            CarreraOrigenCodigo = c.CarreraOrigenCodigo,
            CarreraDestinoCodigo = c.CarreraDestinoCodigo,
            InstitucionOrigen = Recortar(c.InstitucionOrigen, 150),
            Estado = ExpedienteEstados.PendienteDocumentos,
            CorrelationId = Guid.NewGuid(),
            NumeroCertificado = Recortar(c.NumeroCertificado, 40),
            EstudianteNombre = Recortar(c.EstudianteNombre, 200),
            EstudianteIdentificacion = Recortar(c.EstudianteIdentificacion, 20),
            Revisor1Nombre = Recortar(c.Revisores[0].Nombre, 200),
            Revisor1Correo = Recortar(c.Revisores[0].Correo, 150),
            Revisor2Nombre = Recortar(c.Revisores[1].Nombre, 200),
            Revisor2Correo = Recortar(c.Revisores[1].Correo, 150),
            Observaciones = $"Creado desde el certificado SIHA {c.NumeroCertificado} emitido el {c.FechaEmision:yyyy-MM-dd} por {c.EmitidoPor}."
        };

        foreach (var a in c.Asignaturas)
        {
            expediente.Asignaturas.Add(new ExpedienteAsignatura
            {
                AsignaturaOrigenCodigo = a.Codigo,
                AsignaturaOrigenNombre = a.Nombre,
                // El destino lo propone el análisis automático por contenidos (MS C).
                AsignaturaDestinoCodigo = null,
                NotaOrigen = (double)a.Nota,
                FechaAprobacionCertificada = a.FechaAprobacion,
                // El certificado SIHA ya excluye las terceras matrículas: no se registra matrícula.
                NumeroMatriculaDestino = null,
                Semestre = a.Semestre,
                Creditos = a.Creditos,
                Horas = a.HorasTotales
            });
        }

        return expediente;
    }

    internal static CertificadoLeidoResponse ADto(CertificadoSiha c) => new(
        c.NumeroCertificado,
        c.FechaEmision,
        c.EmitidoPor,
        c.EstudianteNombre,
        c.EstudianteIdentificacion,
        c.TipoTramite,
        c.RutaNormativa,
        c.InstitucionOrigen,
        c.CarreraOrigenCodigo,
        c.CarreraOrigenNombre,
        c.CarreraDestinoCodigo,
        c.CarreraDestinoNombre,
        c.Asignaturas.Select(a => new AsignaturaCertificadaResponse(
            a.Codigo, a.Nombre, a.Semestre, a.Creditos, a.HorasTotales, a.FechaAprobacion, a.Nota,
            a.Unidades.Select(u => new UnidadCertificadaResponse(u.Numero, u.Nombre, u.Horas, u.Contenidos)).ToList()))
            .ToList(),
        c.Revisores.Select(r => new RevisorResponse(r.Nombre, r.Correo)).ToList());

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
