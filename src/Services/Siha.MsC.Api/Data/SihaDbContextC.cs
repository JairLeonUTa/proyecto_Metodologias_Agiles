using Microsoft.EntityFrameworkCore;
using Siha.MsC.Api.Domain.Entities;

namespace Siha.MsC.Api.Data;

/// <summary>Contexto de persistencia del Microservicio C (SQL Server DB C).</summary>
public class SihaDbContextC : DbContext
{
    public SihaDbContextC(DbContextOptions<SihaDbContextC> options) : base(options)
    {
    }

    public DbSet<EventoProcesado> EventosProcesados => Set<EventoProcesado>();
    public DbSet<Extraccion> Extracciones => Set<Extraccion>();
    public DbSet<ExtraccionUnidad> ExtraccionUnidades => Set<ExtraccionUnidad>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EventoProcesado>(e =>
        {
            e.ToTable("PROCESSED_EVENTS");
            e.HasKey(x => x.ExpedienteId);
            e.Property(x => x.TipoEvento).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.MessageId);
        });

        modelBuilder.Entity<Extraccion>(e =>
        {
            e.ToTable("EXTRACTION");
            e.HasKey(x => x.Id);
            e.Property(x => x.CodigoAsignaturaOrigen).HasMaxLength(30).IsRequired();
            e.Property(x => x.NombreAsignaturaOrigen).HasMaxLength(200).IsRequired();
            e.Property(x => x.Confianza).HasPrecision(5, 4);
            e.Property(x => x.Estado).HasMaxLength(40).IsRequired();
            e.Property(x => x.ConfirmadoPor).HasMaxLength(150);
            e.Property(x => x.Observacion).HasMaxLength(1000);
            e.HasIndex(x => x.ExpedienteId);
            e.HasIndex(x => x.DocumentoId);
        });

        modelBuilder.Entity<ExtraccionUnidad>(e =>
        {
            e.ToTable("EXTRACTION_UNIT");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            e.Property(x => x.Contenidos).HasMaxLength(4000);
            e.HasIndex(x => new { x.ExtraccionId, x.Numero }).IsUnique();
            e.HasOne(x => x.Extraccion)
                .WithMany(x => x.Unidades)
                .HasForeignKey(x => x.ExtraccionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
