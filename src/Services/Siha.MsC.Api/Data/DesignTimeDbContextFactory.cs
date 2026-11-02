using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Siha.MsC.Api.Data;

/// <summary>Fábrica de contexto para tiempo de diseño (migraciones EF Core de DB C).</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SihaDbContextC>
{
    public SihaDbContextC CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SihaDbContextC>()
            .UseSqlServer(
                Environment.GetEnvironmentVariable("ConnectionStrings__SihaDbC")
                ?? "Server=localhost,14331;Database=SihaDbC;User Id=sa;Password=DisenoLocal1!;TrustServerCertificate=True;Encrypt=False")
            .Options;

        return new SihaDbContextC(options);
    }
}
