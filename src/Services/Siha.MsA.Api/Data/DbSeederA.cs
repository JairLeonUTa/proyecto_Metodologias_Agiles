using Microsoft.EntityFrameworkCore;
using Siha.Contracts;
using Siha.MsA.Api.Domain.Entities;
using Siha.MsA.Api.Security;

namespace Siha.MsA.Api.Data;

/// <summary>
/// Siembra inicial de SQL Server DB A: catálogo de roles (US-01), rutas normativas y umbrales
/// por defecto (US-02) y catálogo académico base (US-03). Es idempotente.
/// </summary>
public static class DbSeederA
{
    public static async Task SembrarAsync(
        SihaDbContextA db,
        IPasswordHasher hasher,
        string adminCorreo,
        string adminPassword,
        ILogger logger,
        string? docentesDemoPassword = null)
    {
        await SembrarRolesAsync(db, hasher, adminCorreo, adminPassword, logger);
        await SembrarParametrosAsync(db, logger);
        await SembrarReglasAsync(db, logger);
        await ActualizarReglaCreditosAsync(db, logger);
        await ActualizarReglaRecomendacionAsync(db, logger);
        await ActualizarReglaCreditosObligatoriosAsync(db, logger);
        await SembrarCatalogoAsync(db, logger);
        await SembrarCatalogoUtaAsync(db, logger);
        if (!string.IsNullOrWhiteSpace(docentesDemoPassword))
        {
            await SembrarDocentesDemoAsync(db, hasher, docentesDemoPassword, logger);
        }
    }

    /// <summary>
    /// Docentes revisores que aparecen en los certificados SIHA de ejemplo. Solo se crean si se
    /// configura <c>Seed:DocentePassword</c>; en producción el administrador los registra en Usuarios.
    /// </summary>
    public static readonly (string Correo, string Nombre)[] DocentesDemo =
    {
        ("cparedes@uta.edu.ec", "Ing. Carlos Paredes"),
        ("mvaca@uta.edu.ec", "Ing. María Vaca")
    };

    private static async Task SembrarDocentesDemoAsync(SihaDbContextA db, IPasswordHasher hasher, string password, ILogger logger)
    {
        var rolDocente = await db.Roles.FirstOrDefaultAsync(r => r.Nombre == RolesSistema.Docente);
        if (rolDocente is null) return;

        var existentes = await db.Usuarios.Select(u => u.CorreoInstitucional).ToListAsync();
        var nuevos = DocentesDemo.Where(d => !existentes.Contains(d.Correo)).ToList();
        foreach (var (correo, nombre) in nuevos)
        {
            db.Usuarios.Add(new Usuario
            {
                CorreoInstitucional = correo,
                NombreCompleto = nombre,
                PasswordHash = hasher.Hash(password),
                RolId = rolDocente.Id,
                Activo = true
            });
        }

        if (nuevos.Count == 0) return;
        await db.SaveChangesAsync();
        logger.LogInformation("Docentes revisores de ejemplo creados: {Correos}.", string.Join(", ", nuevos.Select(d => d.Correo)));
    }

    private static async Task SembrarRolesAsync(
        SihaDbContextA db, IPasswordHasher hasher, string adminCorreo, string adminPassword, ILogger logger)
    {
        if (await db.Roles.AnyAsync())
        {
            return;
        }

        var roles = new List<Rol>
        {
            new() { Nombre = RolesSistema.Administrador, Descripcion = "Gestiona cuentas, reglas y catálogo." },
            new() { Nombre = RolesSistema.Docente, Descripcion = "Revisa y emite dictámenes de homologación." },
            new() { Nombre = RolesSistema.Operador, Descripcion = "Registra expedientes y carga documentos." }
        };

        db.Roles.AddRange(roles);
        await db.SaveChangesAsync();

        var rolAdmin = roles.First(r => r.Nombre == RolesSistema.Administrador);

        db.Usuarios.Add(new Usuario
        {
            CorreoInstitucional = adminCorreo.Trim().ToLowerInvariant(),
            NombreCompleto = "Administrador SIHA-IA",
            PasswordHash = hasher.Hash(adminPassword),
            RolId = rolAdmin.Id,
            Activo = true
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Semilla de seguridad aplicada: roles y cuenta administradora {Correo}.", adminCorreo);
    }

    private static async Task SembrarParametrosAsync(SihaDbContextA db, ILogger logger)
    {
        if (await db.ParametrosEvaluacion.AnyAsync())
        {
            return;
        }

        db.ParametrosEvaluacion.AddRange(
            new ParametroEvaluacion
            {
                RutaNormativa = RutasNormativas.CambioCarreraOu,
                Theta = 80d,
                Delta = 15d,
                Tau = 75d,
                Version = 1,
                ActualizadoPor = "semilla"
            },
            new ParametroEvaluacion
            {
                // Defaults heurísticos del modelo; no son el mínimo normativo de RA.
                RutaNormativa = RutasNormativas.Movilidad,
                Theta = 80d,
                Delta = 15d,
                Tau = 75d,
                Version = 1,
                ActualizadoPor = "semilla"
            });

        await db.SaveChangesAsync();
        logger.LogInformation("Semilla de umbrales θ, δ, τ aplicada para CAU-P-011 y CAU-P-122.");
    }

    /// <summary>Inicio de la versión 2.0 de RN-01 y RN-13: 80 % en contenidos y 80 % en créditos, sí o sí.</summary>
    public static readonly DateOnly VigenciaReglaCreditos = new(2026, 11, 14);

    /// <summary>
    /// Regla definida por el equipo con el docente: para homologar hay que llegar, sí o sí, al 80 % en
    /// contenidos y al 80 % en créditos (2 créditos frente a 3 = 66,67 %: no se homologa). Versiona RN-01 y
    /// RN-13 a 2.0 cerrando la 1.0 (queda en el historial). Incremental e idempotente: se aplica una sola
    /// vez, también en las bases que ya existían.
    /// </summary>
    private static async Task ActualizarReglaCreditosAsync(SihaDbContextA db, ILogger logger)
    {
        var nuevas = new[]
        {
            (Codigo: "RN-01", Descripcion:
                "Para homologar una materia (CAU-P-011) se exige, sí o sí, al menos 80 % en contenidos y al menos 80 % en créditos. " +
                "Si cualquiera de los dos no llega (por ejemplo, 2 créditos frente a 3 = 66,67 %), la materia no se puede homologar. " +
                "Profundidad y carga horaria se muestran solo como datos informativos.",
                Expresion: "contenido>=80 && creditos>=80"),
            (Codigo: "RN-13", Descripcion:
                "Pcred mide los créditos aprobados frente a los de la materia de destino y forma parte de la regla RN-01: debe llegar al 80 %. " +
                "Requiere créditos de origen y destino documentados; si no se conocen, se calcula con las horas, que es equivalente.",
                Expresion: "Pcred=min(100,sumCredOrigen/CredDestino*100) && Pcred>=80")
        };

        var cambios = 0;
        foreach (var (codigo, descripcion, expresion) in nuevas)
        {
            var versiones = await db.Reglas
                .Where(r => r.Codigo == codigo && r.RutaNormativa == RutasNormativas.CambioCarreraOu)
                .OrderByDescending(r => r.VigenteDesde).ThenByDescending(r => r.Id)
                .ToListAsync();
            if (versiones.Count == 0 || versiones.Any(r => r.Version == "2.0")) continue;

            var anterior = versiones[0];
            var fin = VigenciaReglaCreditos.AddDays(-1);
            if (anterior.VigenteHasta is null || anterior.VigenteHasta > fin) anterior.VigenteHasta = fin;
            db.Reglas.Add(new Regla
            {
                Codigo = codigo,
                RutaNormativa = anterior.RutaNormativa,
                TipoTramite = anterior.TipoTramite,
                EsNormativa = anterior.EsNormativa,
                Descripcion = descripcion,
                Expresion = expresion,
                Version = "2.0",
                Fuente = "Definición del equipo SIHA-IA con el docente (noviembre 2026): 80 % en contenidos y 80 % en créditos",
                VigenteDesde = VigenciaReglaCreditos,
                Activa = true
            });
            cambios++;
        }

        if (cambios == 0) return;
        await db.SaveChangesAsync();
        logger.LogInformation("Reglas RN-01 y RN-13 versionadas a 2.0: 80 % en contenidos y 80 % en créditos.");
    }

    /// <summary>Inicio de RN-01 2.2: los créditos bajo el mínimo bloquean; los contenidos solo aconsejan.</summary>
    public static readonly DateOnly VigenciaReglaCreditosObligatorios = new(2026, 11, 25);

    /// <summary>Texto de RN-01 2.2 (cabe en REGLA.Descripcion, 400 caracteres).</summary>
    public const string DescripcionRn01CreditosObligatorios =
        "Créditos obligatorios (CAU-P-011): si la materia aprobada no llega al 80 % de los créditos de destino, no se puede " +
        "homologar y no se vota. Si llega en créditos pero no al 80 % en contenidos, el agente recomienda no homologar y " +
        "deciden los dos docentes revisores; si no coinciden, decide el administrador con un motivo.";

    /// <summary>
    /// RN-01 2.2 (aclarada con el docente): los créditos por debajo del mínimo bloquean la materia; los
    /// contenidos por debajo solo son una recomendación del agente. Cierra la 2.1. Incremental e idempotente.
    /// </summary>
    private static async Task ActualizarReglaCreditosObligatoriosAsync(SihaDbContextA db, ILogger logger)
    {
        var versiones = await db.Reglas
            .Where(r => r.Codigo == "RN-01" && r.RutaNormativa == RutasNormativas.CambioCarreraOu)
            .OrderByDescending(r => r.VigenteDesde).ThenByDescending(r => r.Id)
            .ToListAsync();
        if (versiones.Count == 0 || versiones.Any(r => r.Version == "2.2")) return;

        var anterior = versiones[0];
        var fin = VigenciaReglaCreditosObligatorios.AddDays(-1);
        if (anterior.VigenteHasta is null || anterior.VigenteHasta > fin) anterior.VigenteHasta = fin;
        db.Reglas.Add(new Regla
        {
            Codigo = "RN-01",
            RutaNormativa = anterior.RutaNormativa,
            TipoTramite = anterior.TipoTramite,
            EsNormativa = anterior.EsNormativa,
            Descripcion = DescripcionRn01CreditosObligatorios,
            Expresion = "bloquea: creditos<80; recomendacion: contenido>=80; decision: docentes",
            Version = "2.2",
            Fuente = "Aclarado con el docente (25-11-2026): los créditos son obligatorios",
            VigenteDesde = VigenciaReglaCreditosObligatorios,
            Activa = true
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Regla RN-01 versionada a 2.2: los créditos bajo el mínimo bloquean.");
    }

    /// <summary>Texto de RN-01 2.1 (cabe en REGLA.Descripcion, 400 caracteres).</summary>
    public const string DescripcionRn01Recomendacion =
        "El agente recomienda homologar (CAU-P-011) si llega al 80 % en contenidos y al 80 % en créditos; si no llega, " +
        "recomienda no homologar y explica por qué. La decisión final es siempre de los dos docentes revisores, que pueden " +
        "homologarla aunque no llegue (por ejemplo, si el tema está en el sílabo de origen con otro nombre).";

    /// <summary>Inicio de la versión 2.1 de RN-01: el agente recomienda y los docentes deciden.</summary>
    public static readonly DateOnly VigenciaReglaRecomendacion = new(2026, 11, 24);

    /// <summary>
    /// RN-01 2.1: el 80 % en contenidos y en créditos es la recomendación del agente, no un rechazo
    /// automático. Aunque una materia no llegue, la deciden los dos docentes revisores (el agente puede no
    /// reconocer un tema que en el sílabo de origen se nombra distinto). Incremental e idempotente.
    /// </summary>
    private static async Task ActualizarReglaRecomendacionAsync(SihaDbContextA db, ILogger logger)
    {
        var versiones = await db.Reglas
            .Where(r => r.Codigo == "RN-01" && r.RutaNormativa == RutasNormativas.CambioCarreraOu)
            .OrderByDescending(r => r.VigenteDesde).ThenByDescending(r => r.Id)
            .ToListAsync();
        if (versiones.Count == 0 || versiones.Any(r => r.Version == "2.1")) return;

        var anterior = versiones[0];
        var fin = VigenciaReglaRecomendacion.AddDays(-1);
        if (anterior.VigenteHasta is null || anterior.VigenteHasta > fin) anterior.VigenteHasta = fin;
        db.Reglas.Add(new Regla
        {
            Codigo = "RN-01",
            RutaNormativa = anterior.RutaNormativa,
            TipoTramite = anterior.TipoTramite,
            EsNormativa = anterior.EsNormativa,
            // Máximo 400 caracteres (columna REGLA.Descripcion).
            Descripcion = DescripcionRn01Recomendacion,
            Expresion = "recomendacion: contenido>=80 && creditos>=80; decision: docentes",
            Version = "2.1",
            Fuente = "Definición del equipo SIHA-IA (noviembre 2026): el agente recomienda y los docentes deciden",
            VigenteDesde = VigenciaReglaRecomendacion,
            Activa = true
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Regla RN-01 versionada a 2.1: el agente recomienda y los docentes deciden.");
    }

    private static async Task SembrarReglasAsync(SihaDbContextA db, ILogger logger)
    {
        if (await db.Reglas.AnyAsync())
        {
            return;
        }

        var vigencia = new DateOnly(2026, 1, 1);

        db.Reglas.AddRange(
            new Regla
            {
                Codigo = "RN-01",
                RutaNormativa = RutasNormativas.CambioCarreraOu,
                // INTERNO es la etiqueta de esta fila, no excluye cambio de universidad EXTERNO por CAU-P-011.
                TipoTramite = TiposTramite.Interno,
                Descripcion = "Para cambio de carrera o universidad (CAU-P-011), contenido, profundidad y carga horaria deben alcanzar cada uno al menos 80 %; no es un umbral de créditos ni theta.",
                Expresion = "contenido>=80 && profundidad>=80 && cargaHoraria>=80",
                Version = "1.0",
                Fuente = "Informe técnico y planificación por sprints SIHA-IA, pp. 3–4, RN-01; CAU-P-011",
                EsNormativa = true,
                VigenteDesde = vigencia,
                Activa = true
            },
            new Regla
            {
                Codigo = "RN-09",
                RutaNormativa = RutasNormativas.CambioCarreraOu,
                TipoTramite = TiposTramite.Interno,
                Descripcion = "Para CAU-P-011 se requieren historial académico, malla vigente, sílabos aprobados certificados, certificado de no impedimentos e identidad.",
                Version = "1.0",
                Fuente = "Informe técnico y planificación por sprints SIHA-IA, p. 4, RN-09; CAU-P-011",
                EsNormativa = true,
                VigenteDesde = vigencia,
                Activa = true
            },
            new Regla
            {
                Codigo = "RN-13",
                RutaNormativa = RutasNormativas.CambioCarreraOu,
                TipoTramite = TiposTramite.Interno,
                Descripcion = "Pcred es solo un dato de apoyo, sin umbral de aceptación ni mínimo legal; requiere créditos de origen y destino documentados y CredDestino positivo.",
                Expresion = "Pcred=min(100,sumCredOrigen/CredDestino*100)",
                Version = "1.0",
                Fuente = "Informe técnico y planificación por sprints SIHA-IA, pp. 3–4, RN-13; modelo de apoyo",
                EsNormativa = false,
                VigenteDesde = vigencia,
                Activa = true
            },
            new Regla
            {
                Codigo = "RN-10",
                RutaNormativa = RutasNormativas.Movilidad,
                TipoTramite = TiposTramite.Externo,
                Descripcion = "Solo para movilidad (CAU-P-122), los documentos requeridos son acuerdo de movilidad aprobado, certificado de notas oficial legalizado e informe final.",
                Version = "1.0",
                Fuente = "Informe técnico y planificación por sprints SIHA-IA, p. 4, RN-10; CAU-P-122",
                EsNormativa = true,
                VigenteDesde = vigencia,
                Activa = true
            },
            new Regla
            {
                Codigo = "RN-14",
                RutaNormativa = RutasNormativas.Movilidad,
                TipoTramite = TiposTramite.Externo,
                Descripcion = "Modelo de cobertura ponderada por horas documentadas hj: cj indica similitud coseno >= tau (0,75; API 75/100). Si faltan horas o sum(hj)<=0, pendiente; no inventar datos. Tau no es un umbral de cobertura.",
                Expresion = "Pcont=sum(hj*cj)/sum(hj)*100; cj=1 si similitudCoseno>=tau/100, 0 en otro caso",
                Version = "1.0",
                Fuente = "Informe técnico y planificación por sprints SIHA-IA, pp. 3–4, RN-14; modelo heurístico",
                EsNormativa = false,
                VigenteDesde = vigencia,
                Activa = true
            },
            new Regla
            {
                Codigo = "RN-02",
                RutaNormativa = RutasNormativas.Movilidad,
                TipoTramite = TiposTramite.Externo,
                Descripcion = "Solo para movilidad (CAU-P-122), se exige al menos 80 % de correspondencia en resultados de aprendizaje; EXTERNO no equivale por sí solo a movilidad.",
                Expresion = "ra>=80",
                Version = "1.0",
                Fuente = "Informe técnico y planificación por sprints SIHA-IA, pp. 3–4, RN-02; CAU-P-122, Art. 20",
                EsNormativa = true,
                VigenteDesde = vigencia,
                Activa = true
            });

        await db.SaveChangesAsync();
        logger.LogInformation("Semilla de seis metadatos de reglas aplicada (RN-01, RN-02, RN-09, RN-10, RN-13 y RN-14; informe SIHA-IA pp. 3–4). No ejecuta evaluación.");
    }

    private static async Task SembrarCatalogoAsync(SihaDbContextA db, ILogger logger)
    {
        if (await db.Carreras.AnyAsync())
        {
            return;
        }

        var carreras = new List<Carrera>
        {
            new() { Codigo = "SW", Nombre = "Ingeniería de Software" },
            new() { Codigo = "TI", Nombre = "Tecnologías de la Información" },
            new() { Codigo = "IND", Nombre = "Ingeniería Industrial" },
            new() { Codigo = "ROB", Nombre = "Robótica y Automatización" },
            new() { Codigo = "TEL", Nombre = "Telecomunicaciones" }
        };

        db.Carreras.AddRange(carreras);
        await db.SaveChangesAsync();

        var software = carreras.First(c => c.Codigo == "SW");
        var malla = new MallaCurricular
        {
            CarreraId = software.Id,
            Version = "2024-A",
            Anio = 2024,
            Fuente = "Rediseño curricular FISEI 2024",
            RutaNormativa = RutasNormativas.CambioCarreraOu,
            Vigente = true
        };

        db.Mallas.Add(malla);
        await db.SaveChangesAsync();

        // Muestra representativa del catálogo (la carga completa de los 43 planes se realiza por el
        // endpoint /api/asignaturas/importar).
        db.Asignaturas.AddRange(
            NuevaAsignatura(malla.Id, "SW101", "Fundamentos de Programación", 4, 1,
                ("Introducción y entorno", 12, "Estructura de un programa, compilación y depuración."),
                ("Estructuras de control", 20, "Condicionales, bucles y funciones."),
                ("Arreglos y cadenas", 16, "Recorrido y manipulación básica.")),
            NuevaAsignatura(malla.Id, "SW201", "Estructuras de Datos", 4, 2,
                ("Listas y pilas", 16, "Implementación y aplicaciones."),
                ("Árboles", 16, "Recorridos y árboles binarios de búsqueda."),
                ("Grafos", 16, "Representación y algoritmos de recorrido.")),
            NuevaAsignatura(malla.Id, "SW301", "Arquitectura de Software", 4, 3,
                ("Estilos arquitectónicos", 16, "Capas, microservicios y eventos."),
                ("Patrones de diseño", 16, "Strategy, Factory y Repository."),
                ("Atributos de calidad", 16, "Rendimiento, seguridad y mantenibilidad.")));

        await db.SaveChangesAsync();
        logger.LogInformation("Semilla de catálogo académico aplicada (5 carreras y malla SW 2024-A).");
    }

    /// <summary>Versión de malla que se crea con los sílabos oficiales publicados por la UTA.</summary>
    public const string VersionMallaUta = "UTA-2022";

    /// <summary>
    /// Conocimiento del agente: mallas y sílabos oficiales de las carreras de la FISEI (Software,
    /// Tecnologías de la Información, Industrial, Telecomunicaciones y Robótica), extraídos de los PDF
    /// publicados en uta.edu.ec (tools/catalogo-uta). Cada asignatura trae nivel, créditos, horas y sus
    /// unidades con temas y horas. Se carga una sola vez por carrera y queda como malla vigente.
    /// </summary>
    private static async Task SembrarCatalogoUtaAsync(SihaDbContextA db, ILogger logger)
    {
        var ensamblado = typeof(DbSeederA).Assembly;
        foreach (var recurso in ensamblado.GetManifestResourceNames().Where(r => r.Contains(".CatalogoUta.") && r.EndsWith(".json")))
        {
            await using var flujo = ensamblado.GetManifestResourceStream(recurso)!;
            var catalogo = await System.Text.Json.JsonSerializer.DeserializeAsync<CatalogoUtaJson>(flujo,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (catalogo is null || catalogo.Asignaturas.Count == 0) continue;

            var carrera = await db.Carreras.FirstOrDefaultAsync(c => c.Codigo == catalogo.Carrera);
            if (carrera is null)
            {
                carrera = new Carrera { Codigo = catalogo.Carrera, Nombre = catalogo.Nombre };
                db.Carreras.Add(carrera);
                await db.SaveChangesAsync();
            }

            // Si la malla UTA-2022 ya existe (una base cargada con una versión anterior del catálogo) no se
            // salta la carrera: se le agregan las materias nuevas que falten, sin tocar las que ya están.
            var malla = await db.Mallas.FirstOrDefaultAsync(m => m.CarreraId == carrera.Id && m.Version == VersionMallaUta);
            var mallaNueva = malla is null;
            if (malla is null)
            {
                foreach (var anterior in await db.Mallas.Where(m => m.CarreraId == carrera.Id && m.Vigente).ToListAsync())
                    anterior.Vigente = false;

                malla = new MallaCurricular
                {
                    CarreraId = carrera.Id,
                    Version = VersionMallaUta,
                    Anio = 2022,
                    Fuente = catalogo.Fuente,
                    RutaNormativa = RutasNormativas.CambioCarreraOu,
                    Vigente = true
                };
                db.Mallas.Add(malla);
                await db.SaveChangesAsync();
            }

            var existentes = (await db.Asignaturas.Select(a => a.Codigo).ToListAsync()).ToHashSet();
            var faltantes = catalogo.Asignaturas.Where(a => !existentes.Contains(a.Codigo)).ToList();
            if (faltantes.Count == 0) continue;

            foreach (var a in faltantes)
            {
                var asignatura = new Asignatura
                {
                    MallaId = malla.Id,
                    Codigo = a.Codigo,
                    Nombre = a.Nombre,
                    Creditos = Math.Clamp(a.Creditos ?? 1, 1, 30),
                    Horas = a.Horas ?? a.Unidades.Sum(u => u.Horas),
                    Semestre = Math.Clamp(a.Nivel ?? 1, 1, 20)
                };
                foreach (var u in a.Unidades)
                {
                    asignatura.Unidades.Add(new UnidadTematica
                    {
                        Numero = u.Numero,
                        Nombre = u.Nombre.Length > 200 ? u.Nombre[..200] : u.Nombre,
                        Horas = Math.Max(1, u.Horas),
                        Contenidos = u.Contenidos
                    });
                }

                var (error, advertencias) = Services.ValidacionCatalogo.Evaluar(asignatura);
                asignatura.PlanCompleto = error is null && advertencias.Count == 0;
                db.Asignaturas.Add(asignatura);
            }

            await db.SaveChangesAsync();
            if (mallaNueva)
                logger.LogInformation("Malla oficial de {Carrera} cargada: {Asignaturas} asignaturas con sus sílabos.",
                    catalogo.Nombre, faltantes.Count);
            else
                logger.LogInformation("Malla oficial de {Carrera}: se agregaron {Asignaturas} asignatura(s) que faltaban.",
                    catalogo.Nombre, faltantes.Count);
        }
    }

    private sealed record CatalogoUtaJson(string Carrera, string Nombre, string Fuente, List<AsignaturaUtaJson> Asignaturas);

    private sealed record AsignaturaUtaJson(
        string Codigo, string Nombre, int? Nivel, int? Creditos, int? Horas, string? Descripcion, List<UnidadUtaJson> Unidades);

    private sealed record UnidadUtaJson(int Numero, string Nombre, int Horas, string Contenidos);

    private static Asignatura NuevaAsignatura(
        int mallaId, string codigo, string nombre, int creditos, int semestre,
        params (string Nombre, int Horas, string Contenidos)[] unidades)
    {
        var asignatura = new Asignatura
        {
            MallaId = mallaId,
            Codigo = codigo,
            Nombre = nombre,
            Creditos = creditos,
            Horas = creditos * Services.CatalogoService.HorasPorCreditoUta,
            Semestre = semestre
        };

        var numero = 1;
        foreach (var unidad in unidades)
        {
            asignatura.Unidades.Add(new UnidadTematica
            {
                Numero = numero++,
                Nombre = unidad.Nombre,
                Horas = unidad.Horas,
                Contenidos = unidad.Contenidos
            });
        }

        var (error, advertencias) = Services.ValidacionCatalogo.Evaluar(asignatura);
        asignatura.PlanCompleto = error is null && advertencias.Count == 0;
        return asignatura;
    }
}
