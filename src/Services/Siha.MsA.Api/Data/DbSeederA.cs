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
        ILogger logger)
    {
        await SembrarRolesAsync(db, hasher, adminCorreo, adminPassword, logger);
        await SembrarParametrosAsync(db, logger);
        await SembrarReglasAsync(db, logger);
        await SembrarCatalogoAsync(db, logger);
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
                Delta = 10d,
                Tau = 70d,
                Version = 1,
                ActualizadoPor = "semilla"
            },
            new ParametroEvaluacion
            {
                // CAU-P-122, Art. 20: al menos 80 % de correspondencia en resultados de aprendizaje.
                RutaNormativa = RutasNormativas.Movilidad,
                Theta = 80d,
                Delta = 15d,
                Tau = 65d,
                Version = 1,
                ActualizadoPor = "semilla"
            });

        await db.SaveChangesAsync();
        logger.LogInformation("Semilla de umbrales θ, δ, τ aplicada para CAU-P-011 y CAU-P-122.");
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
                TipoTramite = TiposTramite.Interno,
                Descripcion = "Solo se admiten materias aprobadas de la malla de origen vigente.",
                Version = "1.0",
                Fuente = "Resolución CAU-P-011",
                EsNormativa = true,
                VigenteDesde = vigencia,
                Activa = true
            },
            new Regla
            {
                Codigo = "RN-02",
                RutaNormativa = RutasNormativas.CambioCarreraOu,
                TipoTramite = TiposTramite.Interno,
                Descripcion = "La proporción mínima de créditos homologables es un parámetro heurístico configurable.",
                Expresion = "theta>=80",
                Version = "1.0",
                Fuente = "Parámetro heurístico del proyecto",
                EsNormativa = false,
                VigenteDesde = vigencia,
                Activa = true
            },
            new Regla
            {
                Codigo = "RN-03",
                RutaNormativa = RutasNormativas.Movilidad,
                TipoTramite = TiposTramite.Externo,
                Descripcion = "La documentación debe acreditar la aprobación certificada en la institución de origen.",
                Version = "1.0",
                Fuente = "Resolución CAU-P-122",
                EsNormativa = true,
                VigenteDesde = vigencia,
                Activa = true
            },
            new Regla
            {
                Codigo = "RN-04",
                RutaNormativa = RutasNormativas.Movilidad,
                TipoTramite = TiposTramite.Externo,
                Descripcion = "Cobertura temática mínima configurable para sugerir homologación.",
                Expresion = "tau>=65",
                Version = "1.0",
                Fuente = "Parámetro heurístico del proyecto",
                EsNormativa = false,
                VigenteDesde = vigencia,
                Activa = true
            },
            new Regla
            {
                Codigo = "RN-05",
                RutaNormativa = RutasNormativas.Movilidad,
                TipoTramite = TiposTramite.Externo,
                Descripcion = "Para homologar una asignatura se exige al menos 80 % de correspondencia en resultados de aprendizaje.",
                Expresion = "theta>=80",
                Version = "1.0",
                Fuente = "Resolución CAU-P-122, Art. 20",
                EsNormativa = true,
                VigenteDesde = vigencia,
                Activa = true
            });

        await db.SaveChangesAsync();
        logger.LogInformation("Semilla de reglas aplicada (RN-01 a RN-05).");
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
            Semestre = semestre,
            PlanCompleto = true
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

        return asignatura;
    }
}
