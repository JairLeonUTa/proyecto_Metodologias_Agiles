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
                "Server=localhost,1433;Database=SihaDbA;User Id=sa;Password=SihaAdmin2026!;TrustServerCertificate=True;Encrypt=False")
            .Options;

        return new SihaDbContextA(options);
    }
}
