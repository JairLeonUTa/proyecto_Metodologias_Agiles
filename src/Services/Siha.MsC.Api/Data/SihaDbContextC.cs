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
    public DbSet<Precedente> Precedentes => Set<Precedente>();
    public DbSet<EmbeddingUnidad> EmbeddingsUnidad => Set<EmbeddingUnidad>();
    public DbSet<AnalisisExpediente> AnalisisExpedientes => Set<AnalisisExpediente>();
    public DbSet<DictamenDocente> DictamenesDocentes => Set<DictamenDocente>();

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

        modelBuilder.Entity<AnalisisExpediente>(e =>
        {
            e.ToTable("ANALISIS_EXPEDIENTE");
            e.HasKey(x => x.ExpedienteId);
            e.Property(x => x.CodigoExpediente).HasMaxLength(30);
            e.Property(x => x.NumeroCertificado).HasMaxLength(40);
            e.Property(x => x.EstudianteNombre).HasMaxLength(200);
            e.Property(x => x.TipoTramite).HasMaxLength(20);
            e.Property(x => x.RutaNormativa).HasMaxLength(20);
            e.Property(x => x.InstitucionOrigen).HasMaxLength(150);
            e.Property(x => x.CarreraOrigenCodigo).HasMaxLength(20);
            e.Property(x => x.CarreraDestinoCodigo).HasMaxLength(20);
            e.Property(x => x.Revisor1Nombre).HasMaxLength(200);
            e.Property(x => x.Revisor1Correo).HasMaxLength(150);
            e.Property(x => x.Revisor2Nombre).HasMaxLength(200);
            e.Property(x => x.Revisor2Correo).HasMaxLength(150);
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            e.Property(x => x.ResultadoJson).HasColumnType("NVARCHAR(MAX)");
            e.Property(x => x.CertificadoJson).HasColumnType("NVARCHAR(MAX)").IsRequired();
            e.Property(x => x.CarreraOrigenNombre).HasMaxLength(150);
            e.Property(x => x.CarreraDestinoNombre).HasMaxLength(150);
            e.Property(x => x.Mensaje).HasMaxLength(1000);
        });

        modelBuilder.Entity<DictamenDocente>(e =>
        {
            e.ToTable("DICTAMEN_DOCENTE");
            e.HasKey(x => x.Id);
            e.Property(x => x.CorreoDocente).HasMaxLength(150).IsRequired();
            e.Property(x => x.NombreDocente).HasMaxLength(200);
            e.Property(x => x.DecisionesJson).HasColumnType("NVARCHAR(MAX)").IsRequired();
            e.Property(x => x.Comentario).HasMaxLength(2000);
            e.HasIndex(x => new { x.ExpedienteId, x.CorreoDocente }).IsUnique();
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

        modelBuilder.Entity<Precedente>(e =>
        {
            e.ToTable("PRECEDENTE");
            e.HasKey(x => x.Id);
            e.Property(x => x.Resolucion).HasMaxLength(50).IsRequired();
            e.Property(x => x.AsignaturasOrigenJson).HasColumnType("NVARCHAR(MAX)").IsRequired();
            e.Property(x => x.AsignaturaDestino).HasMaxLength(30).IsRequired();
            e.Property(x => x.InstitucionOrigen).HasMaxLength(150).IsRequired();
            e.Property(x => x.Fecha).HasColumnType("DATE").IsRequired();
            e.HasIndex(x => new { x.AsignaturaDestino, x.InstitucionOrigen });
        });

        modelBuilder.Entity<EmbeddingUnidad>(e =>
        {
            e.ToTable("EMBEDDING_UNIDAD");
            e.HasKey(x => x.Id);
            e.Property(x => x.CodigoAsignatura).HasMaxLength(30).IsRequired();
            e.Property(x => x.Modelo).HasMaxLength(100).IsRequired();
            e.Property(x => x.HashContenido).HasMaxLength(64).IsFixedLength().IsRequired();
            e.Property(x => x.Vector).IsRequired();
            e.HasIndex(x => new { x.CodigoAsignatura, x.NumeroUnidad, x.Modelo }).IsUnique();
        });
    }
}
