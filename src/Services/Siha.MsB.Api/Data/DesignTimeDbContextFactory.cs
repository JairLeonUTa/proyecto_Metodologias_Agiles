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
                "Server=localhost,14330;Database=SihaDbB;User Id=sa;Password=SihaAdmin2026!;TrustServerCertificate=True;Encrypt=False")
            .Options;

        return new SihaDbContextB(options);
    }
}
