using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Siha.MsB.Api.Data;

/// <summary>Fábrica de contexto para tiempo de diseño (migraciones EF Core de DB B).</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SihaDbContextB>
{
    public SihaDbContextB CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SihaDbContextB>()
            .UseSqlServer(
                Environment.GetEnvironmentVariable("ConnectionStrings__SihaDbB")
                ?? throw new InvalidOperationException("Configura ConnectionStrings__SihaDbB para ejecutar las migraciones."))
            .Options;

        return new SihaDbContextB(options);
    }
}
