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

    /// <summary>
    /// Lee el certificado sin registrar nada (vista previa para el operador). En movilidad externa
    /// también lee los sílabos de la otra universidad que se adjunten (uno por materia aprobada).
    /// </summary>
    [HttpPost("validar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CertificadoLeidoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CertificadoLeidoResponse>> Validar(
        IFormFile? archivo,
        CancellationToken cancellationToken,
        [FromForm] List<IFormFile>? silabos = null,
        [FromForm] List<string>? silabosMaterias = null)
    {
        var lectura = await LeerAsync(archivo, cancellationToken);
        if (lectura.Error is not null) return lectura.Error;

        var silabosLeidos = await LeerSilabosAsync(lectura.Certificado!, silabos, silabosMaterias, cancellationToken);
        if (silabosLeidos.Error is not null) return silabosLeidos.Error;

        var avisos = await AvisosMateriasDestinoAsync(lectura.Certificado!, lectura.Destino!, cancellationToken);
        return Ok(ADto(lectura.Certificado!) with { Silabos = silabosLeidos.Resumen, Avisos = avisos });
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
    public async Task<ActionResult<SolicitudCertificadoResponse>> Registrar(
        IFormFile? archivo,
        CancellationToken cancellationToken,
        [FromForm] List<IFormFile>? silabos = null,
        [FromForm] List<string>? silabosMaterias = null)
    {
        var lectura = await LeerAsync(archivo, cancellationToken);
        if (lectura.Error is not null)
        {
            return lectura.Error;
        }

        return await RegistrarLecturaAsync(lectura, archivo!, silabos, silabosMaterias, cancellationToken);
    }

    // ---------- Registro manual (sin PDF) ----------

    /// <summary>
    /// Opciones para el registro manual: docentes activos que pueden ser revisores y, si se indican las
    /// carreras, las materias de su malla vigente (para sugerir los nombres oficiales).
    /// </summary>
    [HttpGet("~/api/expedientes/manual/opciones")]
    [ProducesResponseType(typeof(OpcionesRegistroManualResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OpcionesRegistroManualResponse>> OpcionesManual(
        [FromQuery] string? carreraOrigen, [FromQuery] string? carreraDestino, CancellationToken cancellationToken)
    {
        try
        {
            var docentes = await _docentes.ListarDocentesAsync(cancellationToken);
            async Task<IReadOnlyList<string>> MateriasDe(string? nombre)
            {
                if (string.IsNullOrWhiteSpace(nombre)) return Array.Empty<string>();
                var carrera = await _docentes.ResolverCarreraAsync(nombre, cancellationToken);
                return carrera is null ? Array.Empty<string>() : await _docentes.ObtenerMateriasAsync(carrera.Codigo, cancellationToken);
            }

            return Ok(new OpcionesRegistroManualResponse(
                docentes.Where(d => d.Valido).Select(d => new DocenteOpcionResponse(d.NombreCompleto, d.Correo)).ToList(),
                await MateriasDe(carreraOrigen),
                await MateriasDe(carreraDestino)));
        }
        catch (VerificacionDocentesNoDisponibleException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = ex.Message });
        }
    }

    /// <summary>Revisa los datos del registro manual sin registrar nada (mismas reglas que el certificado).</summary>
    [HttpPost("~/api/expedientes/manual/validar")]
    [ProducesResponseType(typeof(CertificadoLeidoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CertificadoLeidoResponse>> ValidarManual(
        [FromBody] RegistroManualRequest solicitud, CancellationToken cancellationToken)
    {
        var lectura = await LeerManualAsync(solicitud, cancellationToken);
        if (lectura.Error is not null) return lectura.Error;

        var avisos = await AvisosMateriasDestinoAsync(lectura.Certificado!, lectura.Destino!, cancellationToken);
        return Ok(ADto(lectura.Certificado!) with { Avisos = avisos });
    }

    /// <summary>
    /// Registra y envía una solicitud digitada a mano. Queda igual que una registrada con el certificado:
    /// el agente la analiza y se asigna a los dos docentes revisores.
    /// </summary>
    [HttpPost("~/api/expedientes/manual")]
    [ProducesResponseType(typeof(SolicitudCertificadoResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SolicitudCertificadoResponse>> RegistrarManual(
        [FromBody] RegistroManualRequest solicitud, CancellationToken cancellationToken)
    {
        var lectura = await LeerManualAsync(solicitud, cancellationToken);
        if (lectura.Error is not null) return lectura.Error;

        // El registro manual se guarda como el documento del expediente (JSON con los datos ya validados);
        // MS C lo lee igual que un certificado.
        var c = lectura.Certificado!;
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(c);
        var documento = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "archivo", $"registro-manual-{c.NumeroCertificado}.json")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/json"
        };
        return await RegistrarLecturaAsync(lectura, documento, null, null, cancellationToken);
    }

    /// <summary>Arma el registro manual como el texto del certificado y lo valida con las mismas reglas.</summary>
    private async Task<Lectura> LeerManualAsync(RegistroManualRequest? solicitud, CancellationToken cancellationToken)
    {
        solicitud ??= new RegistroManualRequest(null, null, null, null, null, null, null, null, null, null);
        var correos = new[] { solicitud.Revisor1Correo, solicitud.Revisor2Correo }
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()).ToList();
        IReadOnlyList<DocenteRevisorVerificado> verificados;
        try
        {
            verificados = correos.Count == 0 ? Array.Empty<DocenteRevisorVerificado>() : await _docentes.VerificarAsync(correos, cancellationToken);
        }
        catch (VerificacionDocentesNoDisponibleException ex)
        {
            return Lectura.Fallo(StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = ex.Message }));
        }

        var nombres = verificados
            .Where(d => !string.IsNullOrWhiteSpace(d.NombreCompleto))
            .ToDictionary(d => d.Correo.ToLowerInvariant(), d => d.NombreCompleto);
        var operador = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "operador";
        var numero = $"MANUAL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var lineas = LineasRegistroManual(solicitud, numero, DateOnly.FromDateTime(DateTime.Today), operador, nombres);
        return await ValidarLecturaAsync(LectorCertificadoSiha.LeerTexto(lineas), "registro manual", cancellationToken);
    }

    /// <summary>
    /// Convierte el registro manual en las líneas del certificado SIHA-CH-01, para validarlo con exactamente
    /// las mismas reglas que un PDF. Los campos vacíos quedan vacíos y el lector los reporta.
    /// </summary>
    public static IReadOnlyList<string> LineasRegistroManual(
        RegistroManualRequest r, string numero, DateOnly fecha, string operador, IReadOnlyDictionary<string, string> nombresRevisores)
    {
        static string T(string? v) => string.Join(' ', (v ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var tipo = T(r.TipoTramite);
        var ruta = Normalizar(tipo).Contains("movilidad") ? RutasNormativas.Movilidad : RutasNormativas.CambioCarreraOu;
        string Revisor(string? correo)
        {
            var c = T(correo).ToLowerInvariant();
            if (c.Length == 0) return string.Empty;
            return $"{(nombresRevisores.TryGetValue(c, out var n) ? n : c)} - {c}";
        }

        var lineas = new List<string>
        {
            "CERTIFICADO DE HOMOLOGACIÓN SIHA-IA", "Formato SIHA-CH-01",
            $"Número de certificado: {numero}", $"Fecha de emisión: {fecha:yyyy-MM-dd}",
            $"Emitido por: Registro manual ({T(operador)})",
            $"Estudiante: {T(r.EstudianteNombre)}", $"Cédula: {T(r.Cedula)}",
            $"Tipo de trámite: {tipo}", $"Ruta normativa: {ruta}",
            $"Institución de origen: {T(r.InstitucionOrigen)}",
            $"Carrera de origen: {T(r.CarreraOrigen)}", $"Carrera de destino: {T(r.CarreraDestino)}",
            $"Docente revisor 1: {Revisor(r.Revisor1Correo)}", $"Docente revisor 2: {Revisor(r.Revisor2Correo)}"
        };
        foreach (var m in r.Materias ?? Array.Empty<MateriaManualRequest>())
        {
            lineas.Add($"Materia: {T(m.Nombre)}");
            var nota = m.Nota is null ? string.Empty : m.Nota.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            lineas.Add($"Nivel: {m.Nivel?.ToString() ?? string.Empty} Período académico: {T(m.PeriodoAcademico)} Nota: {nota} Estado: {(m.Aprobada ? "Aprobada" : "Reprobada")}");
        }
        var numeroSolicitud = 0;
        foreach (var s in r.Solicitudes ?? Array.Empty<SolicitudManualRequest>())
        {
            lineas.Add($"Solicitud {++numeroSolicitud}");
            foreach (var o in s.MateriasOrigen ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(o)) lineas.Add($"Materia aprobada: {T(o)}");
            lineas.Add($"Homologar por: {T(s.MateriaDestino)}");
        }
        lineas.Add("FIN DEL CERTIFICADO");
        return lineas;
    }

    private static string Normalizar(string texto) => LectorCertificadoSiha.Normalizar(texto);

    /// <summary>Parte común del registro (certificado PDF o registro manual): crea el expediente y lo envía.</summary>
    private async Task<ActionResult<SolicitudCertificadoResponse>> RegistrarLecturaAsync(
        Lectura lectura, IFormFile documento, List<IFormFile>? silabos, List<string>? silabosMaterias, CancellationToken cancellationToken)
    {
        var c = lectura.Certificado!;
        var silabosLeidos = await LeerSilabosAsync(c, silabos, silabosMaterias, cancellationToken);
        if (silabosLeidos.Error is not null)
        {
            return silabosLeidos.Error;
        }
        var existente = await _db.Expedientes
            .Include(e => e.Asignaturas)
            .FirstOrDefaultAsync(e => e.NumeroCertificado == c.NumeroCertificado, cancellationToken);
        if (existente is not null && EsFormatoAnterior(existente))
        {
            // Registrado con el formato anterior del certificado (sin período académico): está oculto en la
            // lista y no se puede analizar, así que no debe impedir registrar el certificado actual.
            // Se le cambia el número para liberar el índice único y conservar su historial.
            existente.NumeroCertificado = NumeroArchivado(c.NumeroCertificado, existente.Codigo);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Certificado {Certificado}: el expediente {Codigo} del formato anterior se archivó como {Archivado}.",
                c.NumeroCertificado, existente.Codigo, existente.NumeroCertificado);
            existente = null;
        }
        if (existente is not null)
        {
            return Conflict(new
            {
                mensaje = $"El certificado {c.NumeroCertificado} ya fue registrado en el expediente {existente.Codigo}.",
                expedienteId = existente.Id,
                codigo = existente.Codigo
            });
        }

        var almacenados = new List<(DocumentoAlmacenado Documento, string Tipo, string? Materia)>();
        try
        {
            almacenados.Add((await _archivos.GuardarAsync(documento, cancellationToken), TiposDocumentoExpediente.Certificado, null));
            foreach (var adjunto in silabosLeidos.Adjuntos)
            {
                almacenados.Add((await _archivos.GuardarAsync(adjunto.Archivo, cancellationToken),
                    TiposDocumentoExpediente.SilaboExterno, adjunto.Materia));
            }
        }
        catch (AlmacenamientoNoDisponibleException ex)
        {
            foreach (var a in almacenados) _archivos.Eliminar(a.Documento.RutaAlmacenamiento);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = ex.Message });
        }

        // El índice único por (expediente, SHA-256) no permite el mismo contenido dos veces.
        var repetidos = almacenados.GroupBy(a => a.Documento.HashSha256).Where(g => g.Count() > 1).ToList();
        if (repetidos.Count > 0)
        {
            foreach (var a in almacenados) _archivos.Eliminar(a.Documento.RutaAlmacenamiento);
            return UnprocessableEntity(new
            {
                mensaje = "El mismo PDF se adjuntó más de una vez. Cada materia necesita su propio sílabo (y ninguno puede ser el certificado).",
                detalles = repetidos.Select(g => $"Archivos repetidos: {string.Join(", ", g.Select(a => a.Materia ?? "certificado"))}.").ToList()
            });
        }

        var expediente = ConstruirExpediente(c, lectura.Origen!, lectura.Destino!);
        foreach (var (almacenado, tipo, materia) in almacenados)
        {
            expediente.Documentos.Add(new ExpedienteDocumento
            {
                NombreArchivo = Recortar(almacenado.NombreOriginal, 255),
                NombreAlmacenado = almacenado.NombreAlmacenado,
                RutaAlmacenamiento = almacenado.RutaAlmacenamiento,
                HashSha256 = almacenado.HashSha256,
                TamanoBytes = almacenado.TamanoBytes,
                MimeType = almacenado.MimeType,
                EstadoRecepcion = EstadosRecepcionDocumento.Validado,
                FechaCargaUtc = DateTimeOffset.UtcNow,
                Tipo = tipo,
                MateriaOrigen = materia is null ? null : Recortar(materia, 200)
            });
        }

        _db.Expedientes.Add(expediente);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Dos cargas simultáneas del mismo certificado: el índice único decide; se compensan los archivos.
            foreach (var a in almacenados) _archivos.Eliminar(a.Documento.RutaAlmacenamiento);
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
            ADto(c) with { Silabos = silabosLeidos.Resumen },
            registrado.EventoPublicado,
            envio.Mensaje));
    }

    private sealed record Lectura(CertificadoSiha? Certificado, CarreraCatalogo? Origen, CarreraCatalogo? Destino, ActionResult? Error)
    {
        public static Lectura Fallo(ActionResult error) => new(null, null, null, error);
    }

    /// <summary>
    /// Avisa de las materias de destino que no aparecen en la malla vigente de la carrera de destino y
    /// sugiere el nombre oficial más parecido. Usa la misma regla que el agente para decidir si coinciden.
    /// </summary>
    private async Task<IReadOnlyList<AvisoMateriaDestinoResponse>> AvisosMateriasDestinoAsync(
        CertificadoSiha c, CarreraCatalogo destino, CancellationToken cancellationToken)
    {
        var malla = await _docentes.ObtenerMateriasAsync(destino.Codigo, cancellationToken);
        if (malla.Count == 0) return Array.Empty<AvisoMateriaDestinoResponse>();

        return c.Solicitudes
            .Where(s => !NombresMaterias.Existe(s.MateriaDestino, malla))
            .Select(s =>
            {
                var sugerida = NombresMaterias.MasParecida(s.MateriaDestino, malla);
                var sugerencia = sugerida is null ? null : NombresMaterias.Titulo(sugerida);
                var mensaje = sugerencia is null
                    ? $"«{s.MateriaDestino}» no aparece en la malla vigente de {c.CarreraDestino}. Revise que sea el nombre oficial: si se registra así, el agente no podrá compararla."
                    : $"«{s.MateriaDestino}» no aparece en la malla vigente de {c.CarreraDestino}. ¿Quiso decir «{sugerencia}»? Si es así, pida a Secretaría que corrija el certificado: si se registra así, el agente no podrá compararla.";
                return new AvisoMateriaDestinoResponse(s.Numero, s.MateriaDestino, sugerencia, mensaje);
            })
            .ToList();
    }

    private sealed record SilaboAdjunto(string Materia, IFormFile Archivo, SilaboExterno Silabo);

    private sealed record LecturaSilabos(
        IReadOnlyList<SilaboAdjunto> Adjuntos,
        IReadOnlyList<SilaboLeidoResponse> Resumen,
        ActionResult? Error)
    {
        public static LecturaSilabos Fallo(ActionResult error)
            => new(Array.Empty<SilaboAdjunto>(), Array.Empty<SilaboLeidoResponse>(), error);
    }

    /// <summary>
    /// Lee y valida los sílabos de la otra universidad adjuntados junto al certificado (movilidad
    /// externa). Cada archivo viene con la materia aprobada a la que corresponde (misma posición en
    /// <paramref name="materias"/>). Adjuntarlos es opcional: sin sílabo, la materia queda
    /// "Sin sílabo para comparar" y la deciden los docentes.
    /// </summary>
    private async Task<LecturaSilabos> LeerSilabosAsync(
        CertificadoSiha c, List<IFormFile>? silabos, List<string>? materias, CancellationToken cancellationToken)
    {
        silabos ??= new List<IFormFile>();
        materias ??= new List<string>();
        if (silabos.Count == 0 && materias.Count == 0)
        {
            return new LecturaSilabos(Array.Empty<SilaboAdjunto>(), Array.Empty<SilaboLeidoResponse>(), null);
        }

        if (c.TipoTramite != TiposTramite.Externo)
        {
            return LecturaSilabos.Fallo(UnprocessableEntity(new
            {
                mensaje = "Los sílabos de otra universidad solo se adjuntan en una movilidad externa. " +
                          "En un cambio de carrera el agente ya conoce los sílabos oficiales de la UTA.",
                detalles = Array.Empty<string>()
            }));
        }

        if (silabos.Count != materias.Count || materias.Any(string.IsNullOrWhiteSpace))
        {
            return LecturaSilabos.Fallo(BadRequest(new
            {
                mensaje = "Cada sílabo debe indicar a qué materia aprobada corresponde (campos 'silabos' y 'silabosMaterias' en el mismo orden)."
            }));
        }

        // Solo se aceptan sílabos de materias que el certificado pide homologar.
        var pedidas = new Dictionary<string, string>();
        foreach (var m in c.Solicitudes.SelectMany(s => s.MateriasOrigen))
        {
            pedidas.TryAdd(LectorCertificadoSiha.Normalizar(m), m);
        }

        var errores = new List<string>();
        var vistas = new HashSet<string>();
        var adjuntos = new List<SilaboAdjunto>();
        for (var i = 0; i < silabos.Count; i++)
        {
            var materia = materias[i].Trim();
            var clave = LectorCertificadoSiha.Normalizar(materia);
            if (!pedidas.TryGetValue(clave, out var nombreOficial))
            {
                errores.Add($"'{materia}' no es una de las materias aprobadas que el certificado pide homologar.");
                continue;
            }

            if (!vistas.Add(clave))
            {
                errores.Add($"Se adjuntó más de un sílabo para «{nombreOficial}»; solo se acepta uno por materia.");
                continue;
            }

            var validacion = _archivos.ValidarPdf(silabos[i]);
            if (!validacion.Valido)
            {
                errores.Add($"Sílabo de «{nombreOficial}»: {validacion.Error}");
                continue;
            }

            using var memoria = new MemoryStream();
            await silabos[i].CopyToAsync(memoria, cancellationToken);
            var lectura = LectorSilaboExterno.Leer(memoria.ToArray(), nombreOficial);
            if (!lectura.EsValido)
            {
                errores.AddRange(lectura.Errores);
                continue;
            }

            adjuntos.Add(new SilaboAdjunto(nombreOficial, silabos[i], lectura.Silabo!));
        }

        if (errores.Count > 0)
        {
            _logger.LogWarning("Sílabos externos rechazados. Errores={Errores}", string.Join(" | ", errores));
            return LecturaSilabos.Fallo(UnprocessableEntity(new
            {
                mensaje = "No se pudieron leer los sílabos de la otra universidad. Corríjalos o retírelos: sin sílabo, esa materia la deciden los docentes revisores.",
                detalles = errores
            }));
        }

        var resumen = adjuntos
            .Select(a => new SilaboLeidoResponse(a.Materia, a.Silabo.NombreEnSilabo, a.Silabo.Creditos,
                a.Silabo.Unidades.Count, a.Silabo.HorasTotales))
            .ToList();
        return new LecturaSilabos(adjuntos, resumen, null);
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
        return await ValidarLecturaAsync(LectorCertificadoSiha.Leer(memoria.ToArray()), archivo.FileName, cancellationToken);
    }

    /// <summary>
    /// Reglas que dependen del sistema (revisores DOCENTE activos y carreras del catálogo), comunes al
    /// certificado PDF y al registro manual.
    /// </summary>
    private async Task<Lectura> ValidarLecturaAsync(ResultadoLecturaCertificado lectura, string procedencia, CancellationToken cancellationToken)
    {
        if (!lectura.EsValido)
        {
            _logger.LogWarning("Certificado SIHA rechazado. Procedencia={Procedencia} Errores={Errores}",
                procedencia, string.Join(" | ", lectura.Errores));
            return Lectura.Fallo(UnprocessableEntity(Rechazo(lectura.Problemas)));
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

        var problemas = new List<ProblemaCertificado>();
        void Revisor(string mensaje) => problemas.Add(new(SeccionesCertificado.Revisores, TiposProblema.Regla, mensaje));
        foreach (var revisor in lectura.Certificado.Revisores)
        {
            var v = verificados.FirstOrDefault(d => string.Equals(d.Correo, revisor.Correo, StringComparison.OrdinalIgnoreCase));
            if (v is null || !v.Existe)
                Revisor($"El docente revisor {revisor.Nombre} ({revisor.Correo}) no está registrado en el sistema.");
            else if (!v.EsDocente)
                Revisor($"El usuario {revisor.Correo} no tiene el rol DOCENTE; no puede ser revisor.");
            else if (!v.Activo)
                Revisor($"El docente {revisor.Correo} está inactivo; no puede ser revisor.");
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
            problemas.Add(new(SeccionesCertificado.Tramite, TiposProblema.Regla,
                $"La carrera de origen '{lectura.Certificado.CarreraOrigen}' no existe en el catálogo de la UTA."));
        if (destino is null)
            problemas.Add(new(SeccionesCertificado.Tramite, TiposProblema.Regla,
                $"La carrera de destino '{lectura.Certificado.CarreraDestino}' no existe en el catálogo de la UTA."));

        if (problemas.Count > 0)
        {
            return Lectura.Fallo(UnprocessableEntity(Rechazo(problemas)));
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

    /// <summary>
    /// Expediente creado con el formato anterior del certificado SIHA: sus materias no tienen período académico.
    /// La lista de Solicitudes los oculta con la misma regla.
    /// </summary>
    public static bool EsFormatoAnterior(Expediente e)
        => e.Asignaturas.Count == 0 || e.Asignaturas.Any(a => string.IsNullOrWhiteSpace(a.PeriodoAcademico));

    /// <summary>Número con el que queda archivado un expediente del formato anterior (máximo 40 caracteres).</summary>
    public static string NumeroArchivado(string numero, string codigoExpediente)
    {
        var sufijo = $" (anterior {codigoExpediente[^6..]})";
        return (numero.Length + sufijo.Length > 40 ? numero[..(40 - sufijo.Length)] : numero) + sufijo;
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

    /// <summary>
    /// Cuerpo del 422 cuando el certificado no se puede registrar. Además de <c>mensaje</c> y
    /// <c>detalles</c> (como siempre), trae el <c>tipo</c> de rechazo en lenguaje sencillo, cada problema
    /// con su sección y la lista de verificación ✔/✘ por sección del certificado.
    /// </summary>
    internal static RechazoCertificadoResponse Rechazo(IReadOnlyList<ProblemaCertificado> problemas)
    {
        var noEsCertificado = problemas.Any(p => p.Tipo == TiposProblema.NoEsCertificado);
        var vacios = problemas.Any(p => p.Tipo == TiposProblema.CampoVacio);
        var reglas = problemas.Any(p => p.Tipo == TiposProblema.Regla);

        var (tipo, titulo) = noEsCertificado
            ? (TiposProblema.NoEsCertificado, "Este PDF no es el certificado SIHA.")
            : vacios && reglas
                ? ("CAMPOS_Y_REGLAS", "El certificado tiene campos sin llenar y datos que no cumplen las reglas.")
                : vacios
                    ? (TiposProblema.CampoVacio, "El certificado tiene campos sin llenar.")
                    : (TiposProblema.Regla, "El certificado tiene datos que no cumplen las reglas.");

        var mensaje = noEsCertificado
            ? $"{titulo} Solo se acepta el certificado de homologación generado con la plantilla oficial ({LectorCertificadoSiha.CodigoFormato}): no un plan analítico, un PDF escaneado ni una imagen."
            : $"{titulo} Corrija el documento en Secretaría y vuelva a cargarlo.";

        // Si el archivo no es el certificado no tiene sentido revisar sus secciones.
        var secciones = noEsCertificado
            ? new List<SeccionVerificadaResponse>()
            : SeccionesCertificado.Verificables
                .Select(s => new SeccionVerificadaResponse(s,
                    problemas.All(p => p.Seccion != s),
                    problemas.Where(p => p.Seccion == s).Select(p => p.Mensaje).ToList()))
                .ToList();

        return new RechazoCertificadoResponse(
            mensaje,
            tipo,
            problemas.Select(p => p.Mensaje).ToList(),
            problemas.Select(p => new ProblemaCertificadoResponse(p.Seccion, p.Tipo, p.Mensaje)).ToList(),
            secciones);
    }
}
