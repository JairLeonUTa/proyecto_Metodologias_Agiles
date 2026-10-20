using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Siha.MsA.Api.Contracts.Dtos;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;
using Siha.MsA.Api.Services;

namespace Siha.MsA.Tests;

/// <summary>
/// Validación de la importación del catálogo con datos REALES de los planes analíticos oficiales
/// (carpeta ddd, 64 PDF FISEI 2026; inventario en docs/catalogo/catalogo-estructurado-2026.json).
/// Verifica que la importación conserve los códigos UTA reales, créditos, horas, unidades y la
/// fuente, y que los duplicados se rechacen. Tarea de validación S1 (Paul Acurio, 20-oct-2026).
/// </summary>
public class CatalogoImportacionRealTests
{
    private static SihaDbContextA CrearContexto()
    {
        var options = new DbContextOptionsBuilder<SihaDbContextA>()
            .UseInMemoryDatabase($"siha-tests-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SihaDbContextA(options);
    }

    private static async Task<(SihaDbContextA Db, int MallaId)> PrepararAsync(string fuente)
    {
        var db = CrearContexto();
        var carrera = new Carrera { Codigo = "SF", Nombre = "Ingeniería de Software" };
        db.Carreras.Add(carrera);
        var malla = new MallaCurricular
        {
            Carrera = carrera,
            Version = "2026-DDD",
            Anio = 2026,
            Fuente = fuente
        };
        db.Mallas.Add(malla);
        await db.SaveChangesAsync();
        return (db, malla.Id);
    }

    /// <summary>Plan real: UTA-FISEI-SF-MP-UP-04-01 (PlanAlitico METODOLOGIAS AGILES.pdf, p. 10).</summary>
    private static AsignaturaImportItemRequest MetodologiasAgilesReales() => new(
        "UTA-FISEI-SF-MP-UP-04-01", "METODOLOGIAS AGILES", 4, 192, 4,
        new List<UnidadTematicaRequest>
        {
            new(1, "Evolución de software Paradigmas tradicionales Modelos Tradicionales", 48, ""),
            new(2, "Filosofía Ágil Filosofía y enfoques ágiles Beneficios", 48, ""),
            new(3, "XP, Programación Extrema Definición XP Utilización de Metodología XP", 48, ""),
            new(4, "Herramientas para el manejo de Scrum Beneficios Oportunidades Limitaciones", 48, "")
        });

    /// <summary>Plan real: UTA-FISEI-TI-MP-UP-06-01 (PlanAlitico ADMINISTRACION DE BASE DE DATOS.pdf, p. 10).</summary>
    private static AsignaturaImportItemRequest AdministracionBdReales() => new(
        "UTA-FISEI-TI-MP-UP-06-01", "ADMINISTRACION DE BASE DE DATOS", 3, 144, 6,
        new List<UnidadTematicaRequest>
        {
            new(1, "Conceptos de Administración de Base de Datos", 36, ""),
            new(2, "Autenticación, roles y privilegios", 36, ""),
            new(3, "Monitoreo de bases de datos", 36, ""),
            new(4, "Estrategias avanzadas de respaldo y recuperación", 36, "")
        });

    [Fact]
    public async Task Conserva_codigo_uta_creditos_horas_y_unidades_de_un_plan_real()
    {
        const string fuente = "Planes analíticos oficiales FISEI 2026 (carpeta ddd, PDF firmados)";
        var (db, mallaId) = await PrepararAsync(fuente);
        var servicio = new CatalogoService(db);

        var request = new ImportarCatalogoRequest(mallaId, new List<AsignaturaImportItemRequest>
        {
            MetodologiasAgilesReales()
        });

        var (ok, error, resultado) = await servicio.ImportarAsync(request);

        ok.Should().BeTrue();
        error.Should().BeNull();
        resultado!.Total.Should().Be(1);
        resultado.Importadas.Should().Be(1);
        resultado.Rechazadas.Should().Be(0);
        resultado.Errores.Should().BeEmpty();

        var importada = await db.Asignaturas
            .Include(a => a.Unidades)
            .Include(a => a.Malla)
            .SingleAsync();

        importada.Codigo.Should().Be("UTA-FISEI-SF-MP-UP-04-01");
        importada.Codigo.Length.Should().BeLessOrEqualTo(30, "el DTO exige MaxLength(30)");
        importada.Nombre.Should().Be("METODOLOGIAS AGILES");
        importada.Creditos.Should().Be(4);
        importada.Horas.Should().Be(192);
        importada.Horas.Should().Be(importada.Creditos * CatalogoService.HorasPorCreditoUta);
        importada.Semestre.Should().Be(4);
        importada.PlanCompleto.Should().BeTrue();

        importada.Unidades.Should().HaveCount(4);
        importada.Unidades.Select(u => u.Horas).Should().AllBeEquivalentTo(48);
        importada.Unidades.Sum(u => u.Horas).Should().Be(importada.Horas);
        importada.Malla!.Fuente.Should().Be(fuente, "la fuente del catálogo vive en la malla");
    }

    [Fact]
    public async Task Conserva_planes_reales_de_distinta_carrera_y_carga()
    {
        var (db, mallaId) = await PrepararAsync("ddd");
        var servicio = new CatalogoService(db);

        var request = new ImportarCatalogoRequest(mallaId, new List<AsignaturaImportItemRequest>
        {
            AdministracionBdReales(),
            new("UTA-FISEI-TI-MP-UP-09-02", "DESARROLLO DE PROYECTOS", 6, 288, 9,
                new List<UnidadTematicaRequest>
                {
                    new(1, "Lineamientos para el desarrollo Formatos", 57, ""),
                    new(2, "Marco teórico de la investigación Funciones Antecedentes", 72, ""),
                    new(3, "Planificación detallada Desarrollo o tutoría del proyecto", 96, ""),
                    new(4, "Comprobación de la hipótesis Filosofía Métodos", 63, "")
                })
        });

        var (ok, _, resultado) = await servicio.ImportarAsync(request);

        ok.Should().BeTrue();
        resultado!.Importadas.Should().Be(2);
        resultado.Rechazadas.Should().Be(0);

        var bd = await db.Asignaturas.Include(a => a.Unidades)
            .SingleAsync(a => a.Codigo == "UTA-FISEI-TI-MP-UP-06-01");
        bd.Creditos.Should().Be(3);
        bd.Horas.Should().Be(144);
        bd.Unidades.Should().HaveCount(4);
        bd.Unidades.Sum(u => u.Horas).Should().Be(144);
        bd.PlanCompleto.Should().BeTrue();

        var proyectos = await db.Asignaturas.Include(a => a.Unidades)
            .SingleAsync(a => a.Codigo == "UTA-FISEI-TI-MP-UP-09-02");
        proyectos.Creditos.Should().Be(6);
        proyectos.Horas.Should().Be(288);
        proyectos.Semestre.Should().Be(9);
        proyectos.Unidades.Sum(u => u.Horas).Should().Be(288);
        proyectos.PlanCompleto.Should().BeTrue();
    }

    [Fact]
    public async Task Rechaza_el_codigo_real_duplicado_dentro_del_lote()
    {
        var (db, mallaId) = await PrepararAsync("ddd");
        var servicio = new CatalogoService(db);

        var request = new ImportarCatalogoRequest(mallaId, new List<AsignaturaImportItemRequest>
        {
            MetodologiasAgilesReales(),
            MetodologiasAgilesReales()
        });

        var (ok, _, resultado) = await servicio.ImportarAsync(request);

        ok.Should().BeTrue();
        resultado!.Importadas.Should().Be(1);
        resultado.Rechazadas.Should().Be(1);
        resultado.Errores.Should().Contain(e => e.Contains("UTA-FISEI-SF-MP-UP-04-01"));
        db.Asignaturas.Count().Should().Be(1);
    }

    [Fact]
    public async Task Rechaza_el_codigo_real_ya_importado_en_una_corrida_anterior()
    {
        var (db, mallaId) = await PrepararAsync("ddd");
        var servicio = new CatalogoService(db);

        var primera = new ImportarCatalogoRequest(mallaId, new List<AsignaturaImportItemRequest>
        {
            AdministracionBdReales()
        });
        await servicio.ImportarAsync(primera);

        var segunda = new ImportarCatalogoRequest(mallaId, new List<AsignaturaImportItemRequest>
        {
            AdministracionBdReales()
        });
        var (ok, _, resultado) = await servicio.ImportarAsync(segunda);

        ok.Should().BeTrue();
        resultado!.Rechazadas.Should().Be(1);
        resultado.Errores.Should().Contain(e => e.Contains("duplicado"));
        db.Asignaturas.Count().Should().Be(1);
    }

    [Fact]
    public async Task El_lote_ordenado_por_codigo_del_catalogo_real_importa_sin_rechazos()
    {
        // Los 24 planes reales de Software (SF) del catalogo estructurado, en orden de codigo.
        var (db, mallaId) = await PrepararAsync("ddd");
        var servicio = new CatalogoService(db);

        var lote = new List<AsignaturaImportItemRequest>
        {
            new("UTA-FISEI-SF-MP-UB-01-04", "ALGORITMOS Y LOGICA DE PROGRAMACION", 4, 192, 1, null),
            new("UTA-FISEI-SF-MP-UB-02-01", "FUNDAMENTOS DE INGENIERIA DE SOFTWARE", 3, 144, 2, null),
            new("UTA-FISEI-SF-MP-UB-03-04", "INTRODUCCION A REDES", 3, 144, 3, null),
            new("UTA-FISEI-SF-MP-UB-03-05", "MODELAMIENTO Y DISEñO DE SOFTWARE", 3, 144, 3, null),
            new("UTA-FISEI-SF-MP-UP-04-01", "METODOLOGIAS AGILES", 4, 192, 4, null),
            new("UTA-FISEI-SF-MP-UP-04-02", "REDES", 3, 144, 4, null),
            new("UTA-FISEI-SF-MP-UP-04-03", "BASE DE DATOS", 3, 144, 4, null),
            new("UTA-FISEI-SF-MP-UP-04-04", "COMPUTACION VISUAL", 3, 144, 4, null),
            new("UTA-FISEI-SF-MP-UP-04-05", "MANEJO Y CONFIGURACION DEL SOFTWARE", 2, 96, 4, null),
            new("UTA-FISEI-SF-MP-UP-05-01", "INVESTIGACIÓN OPERATIVA", 3, 144, 5, null),
            new("UTA-FISEI-SF-MP-UP-05-03", "PATRONES DE SOFTWARE", 3, 144, 5, null),
            new("UTA-FISEI-SF-MP-UP-05-04", "APLICACIONES ORIENTADAS A SERVICIOS", 3, 144, 5, null),
            new("UTA-FISEI-SF-MP-UP-05-05", "INTERACCION HUMANO / COMPUTADOR", 3, 144, 5, null),
            new("UTA-FISEI-SF-MP-UP-06-02", "APLICACIONES WEB Y MOVILES", 4, 192, 6, null),
            new("UTA-FISEI-SF-MP-UP-06-03", "APLICACIONES DISTRIBUIDAS", 3, 144, 6, null),
            new("UTA-FISEI-SF-MP-UP-06-04", "GESTION DE PRUEBAS E IMPLANTACIÓN DE SOFTWARE", 3, 144, 6, null),
            new("UTA-FISEI-SF-MP-UP-07-02", "DESARROLLO ASISTIDO POR SOFTWARE", 2, 96, 7, null),
            new("UTA-FISEI-SF-MP-UP-07-03", "AUDITORIA DE SISTEMAS DE INFORMACION", 3, 144, 7, null),
            new("UTA-FISEI-SF-MP-UP-07-04", "GESTION DE PROYECTOS DE SOFTWARE", 3, 144, 7, null),
            new("UTA-FISEI-SF-MP-UP-07-05", "GESTION DE CALIDAD DEL SOFTWARE", 3, 144, 7, null),
            new("UTA-FISEI-SF-MP-UP-07-06", "INTELIGENCIA ARTIFICIAL", 2, 96, 7, null),
            new("UTA-FISEI-SF-MP-UP-08-02", "EMPRENDIMIENTO Y LEGISLACION LABORAL", 2, 96, 8, null),
            new("UTA-FISEI-SF-MP-UP-08-03", "SEGURIDAD EN EL DESARROLLO DEL SOFTWARE", 3, 144, 8, null),
            new("UTA-FISEI-SF-MP-UP-08-04", "INGENIERIA ECONOMICA PARA SOFTWARE", 2, 96, 8, null)
        };

        var request = new ImportarCatalogoRequest(mallaId, lote);
        var (ok, _, resultado) = await servicio.ImportarAsync(request);

        ok.Should().BeTrue();
        resultado!.Total.Should().Be(24);
        resultado.Importadas.Should().Be(24);
        resultado.Rechazadas.Should().Be(0);
        // Sin unidades temáticas el servicio marca cada plan como pendiente (no inventamos unidades).
        resultado.Errores.Should().HaveCount(24);
        resultado.Errores.Should().OnlyContain(e => e.Contains("no tiene unidades temáticas"));
        (await db.Asignaturas.CountAsync(a => a.PlanCompleto)).Should().Be(0);
    }
}
