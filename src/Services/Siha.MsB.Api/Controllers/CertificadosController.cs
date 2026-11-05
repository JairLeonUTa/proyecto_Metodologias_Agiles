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
        var lectura = await LeerAsync(archivo, cancellationToken);
        return lectura.Error ?? Ok(ADto(lectura.Certificado!));
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
        var lectura = await LeerAsync(archivo, cancellationToken);
        if (lectura.Error is not null)
        {
            return lectura.Error;
        }

        var c = lectura.Certificado!;
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

        var expediente = ConstruirExpediente(c, lectura.Origen!, lectura.Destino!);
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
            c.NumeroCertificado, expediente.Id, c.Materias.Count);

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

    private sealed record Lectura(CertificadoSiha? Certificado, CarreraCatalogo? Origen, CarreraCatalogo? Destino, ActionResult? Error)
    {
        public static Lectura Fallo(ActionResult error) => new(null, null, null, error);
    }

    private async Task<Lectura> LeerAsync(
        IFormFile? archivo, CancellationToken cancellationToken)
    {
        var validacion = _archivos.ValidarPdf(archivo);
        if (!validacion.Valido)
        {
            return Lectura.Fallo(BadRequest(new { mensaje = validacion.Error, tamanoMaximoBytes = _archivos.TamanoMaximoBytes }));
        }

        using var memoria = new MemoryStream();
        await archivo!.CopyToAsync(memoria, cancellationToken);
        var lectura = LectorCertificadoSiha.Leer(memoria.ToArray());
        if (!lectura.EsValido)
        {
            _logger.LogWarning("Certificado SIHA rechazado. Archivo={Archivo} Errores={Errores}",
                archivo.FileName, string.Join(" | ", lectura.Errores));
            return Lectura.Fallo(UnprocessableEntity(new
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
            return Lectura.Fallo(StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = ex.Message }));
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

        // Las carreras se escriben con su nombre completo; deben existir en el catálogo de MS A.
        CarreraCatalogo? origen = null, destino = null;
        try
        {
            origen = await _docentes.ResolverCarreraAsync(lectura.Certificado.CarreraOrigen, cancellationToken);
            destino = await _docentes.ResolverCarreraAsync(lectura.Certificado.CarreraDestino, cancellationToken);
        }
        catch (VerificacionDocentesNoDisponibleException ex)
        {
            return Lectura.Fallo(StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = ex.Message }));
        }

        if (origen is null && lectura.Certificado.TipoTramite == TiposTramite.Interno)
            problemas.Add($"La carrera de origen '{lectura.Certificado.CarreraOrigen}' no existe en el catálogo de la UTA.");
        if (destino is null)
            problemas.Add($"La carrera de destino '{lectura.Certificado.CarreraDestino}' no existe en el catálogo de la UTA.");

        if (problemas.Count > 0)
        {
            return Lectura.Fallo(UnprocessableEntity(new
            {
                mensaje = "El certificado no se puede registrar: revise los docentes revisores y las carreras.",
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

        // Movilidad externa: la carrera de otra universidad no está en el catálogo de la UTA.
        origen ??= new CarreraCatalogo("EXTERNA", lectura.Certificado.CarreraOrigen);
        return new Lectura(lectura.Certificado with { Revisores = revisores }, origen, destino, null);
    }

    internal static Expediente ConstruirExpediente(CertificadoSiha c, CarreraCatalogo origen, CarreraCatalogo destino)
    {
        var expediente = new Expediente
        {
            Codigo = $"EXP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            TipoTramite = c.TipoTramite,
            RutaNormativa = c.RutaNormativa,
            CarreraOrigenCodigo = Recortar(origen.Codigo, 20),
            CarreraDestinoCodigo = Recortar(destino.Codigo, 20),
            CarreraOrigenNombre = Recortar(c.CarreraOrigen, 150),
            CarreraDestinoNombre = Recortar(c.CarreraDestino, 150),
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

        var indice = 1;
        foreach (var m in c.Materias.OrderBy(m => m.Nivel).ThenBy(m => m.Nombre))
        {
            var solicitud = c.Solicitudes.FirstOrDefault(s => s.MateriasOrigen.Any(o =>
                LectorCertificadoSiha.Normalizar(o) == LectorCertificadoSiha.Normalizar(m.Nombre)));
            expediente.Asignaturas.Add(new ExpedienteAsignatura
            {
                // El certificado no trae códigos: se identifica por nivel y orden.
                AsignaturaOrigenCodigo = $"N{m.Nivel}-{indice++:00}",
                AsignaturaOrigenNombre = Recortar(m.Nombre, 200),
                NotaOrigen = (double)m.Nota,
                Nivel = m.Nivel,
                PeriodoAcademico = Recortar(m.PeriodoAcademico, 40),
                Aprobada = m.Aprobada,
                AsignaturaDestinoNombre = solicitud is null ? null : Recortar(solicitud.MateriaDestino, 200),
                SolicitudNumero = solicitud?.Numero
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
        c.CarreraOrigen,
        c.CarreraDestino,
        c.Materias.Select(m => new MateriaCursadaResponse(m.Nombre, m.Nivel, m.PeriodoAcademico, m.Nota, m.Aprobada)).ToList(),
        c.Solicitudes.Select(s => new SolicitudHomologacionResponse(s.Numero, s.MateriasOrigen, s.MateriaDestino)).ToList(),
        c.Revisores.Select(r => new RevisorResponse(r.Nombre, r.Correo)).ToList());

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
