using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Siha.MsA.Api.Data;

/// <summary>
/// Fábrica de contexto para tiempo de diseño (migraciones EF Core, tarea S1-J01).
/// Permite generar migraciones sin ejecutar el host de la aplicación.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SihaDbContextA>
{
    public SihaDbContextA CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SihaDbContextA>()
            .UseSqlServer(
                Environment.GetEnvironmentVariable("ConnectionStrings__SihaDbA")
                ?? throw new InvalidOperationException("Configura ConnectionStrings__SihaDbA para ejecutar las migraciones."))
            .Options;

        return new SihaDbContextA(options);
    }
}
