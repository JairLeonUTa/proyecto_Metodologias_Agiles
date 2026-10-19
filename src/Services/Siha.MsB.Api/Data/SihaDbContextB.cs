using Microsoft.EntityFrameworkCore;
using Siha.MsB.Api.Domain.Entities;

namespace Siha.MsB.Api.Data;

/// <summary>Contexto de persistencia del Microservicio B (SQL Server DB B) para expedientes.</summary>
public class SihaDbContextB : DbContext
{
    public SihaDbContextB(DbContextOptions<SihaDbContextB> options) : base(options)
    {
    }

    public DbSet<Expediente> Expedientes => Set<Expediente>();
    public DbSet<ExpedienteDocumento> ExpedienteDocumentos => Set<ExpedienteDocumento>();
    public DbSet<ExpedienteAsignatura> ExpedienteAsignaturas => Set<ExpedienteAsignatura>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Expediente>(e =>
        {
            e.ToTable("EXPEDIENTE");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasIndex(x => x.CorrelationId);
            e.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            e.Property(x => x.TipoTramite).HasMaxLength(20).IsRequired();
            e.Property(x => x.RutaNormativa).HasMaxLength(20).IsRequired();
            e.Property(x => x.CarreraOrigenCodigo).HasMaxLength(20);
            e.Property(x => x.CarreraDestinoCodigo).HasMaxLength(20);
            e.Property(x => x.InstitucionOrigen).HasMaxLength(150);
            e.Property(x => x.Estado).HasMaxLength(40).IsRequired();
            e.Property(x => x.UltimoErrorPublicacion).HasMaxLength(1000);
            e.Property(x => x.Observaciones).HasMaxLength(1000);
        });

        modelBuilder.Entity<ExpedienteDocumento>(e =>
        {
            e.ToTable("EXPEDIENTE_DOC");
            e.HasKey(x => x.Id);
            e.Property(x => x.NombreArchivo).HasMaxLength(255).IsRequired();
            e.Property(x => x.NombreAlmacenado).HasMaxLength(80);
            e.Property(x => x.RutaAlmacenamiento).HasMaxLength(500).IsRequired();
            e.Property(x => x.HashSha256).HasMaxLength(64);
            e.Property(x => x.MimeType).HasMaxLength(100);
            e.Property(x => x.EstadoRecepcion).HasMaxLength(30).IsRequired();
            e.Property(x => x.ObservacionValidacion).HasMaxLength(500);
            e.HasIndex(x => x.HashSha256);
            e.HasOne(x => x.Expediente)
                .WithMany(x => x.Documentos)
                .HasForeignKey(x => x.ExpedienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExpedienteAsignatura>(e =>
        {
            e.ToTable("EXPEDIENTE_ASIGNATURA");
            e.HasKey(x => x.Id);
            e.Property(x => x.AsignaturaOrigenCodigo).HasMaxLength(30).IsRequired();
            e.Property(x => x.AsignaturaOrigenNombre).HasMaxLength(200).IsRequired();
            e.Property(x => x.AsignaturaDestinoCodigo).HasMaxLength(30);
            e.HasOne(x => x.Expediente)
                .WithMany(x => x.Asignaturas)
                .HasForeignKey(x => x.ExpedienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
