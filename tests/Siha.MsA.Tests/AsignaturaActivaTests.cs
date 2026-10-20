using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Siha.MsA.Api.Contracts;
using Siha.MsA.Api.Data;
using Siha.MsA.Api.Domain.Entities;

namespace Siha.MsA.Tests;

public class AsignaturaActivaTests
{
    [Fact]
    public void La_migracion_solo_agrega_activa_con_default_true_y_es_reversible()
    {
        var migracion = new Siha.MsA.Api.Migrations.AsignaturaActiva();
        var alta = Assert.IsType<AddColumnOperation>(Assert.Single(migracion.UpOperations));
        Assert.Equal("ASIGNATURA", alta.Table);
        Assert.Equal("Activa", alta.Name);
        Assert.False(alta.IsNullable);
        Assert.Equal(true, alta.DefaultValue);
        var baja = Assert.IsType<DropColumnOperation>(Assert.Single(migracion.DownOperations));
        Assert.Equal(alta.Table, baja.Table);
        Assert.Equal(alta.Name, baja.Name);
    }

    [Fact]
    public void La_baja_conserva_el_codigo_reservado_y_se_expone_en_el_dto()
    {
        using var db = new SihaDbContextA(new DbContextOptionsBuilder<SihaDbContextA>()
            .UseSqlServer("Server=localhost;Database=NoSeConecta;Integrated Security=true")
            .Options);
        var entidad = db.Model.FindEntityType(typeof(Asignatura))!;
        var propiedad = entidad.FindProperty("Activa");

        Assert.NotNull(propiedad);
        Assert.False(propiedad.IsNullable);
        Assert.Equal(true, propiedad.GetDefaultValue());
        Assert.Contains(entidad.GetIndexes(), i => i.IsUnique
            && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "MallaId", "Codigo" })
            && i.GetFilter() is null);

        var asignatura = new Asignatura();
        Assert.True(asignatura.Activa);
        asignatura.Activa = false;
        Assert.False(Mapeos.AAsignaturaDto(asignatura).Activa);
    }
}
