using Microsoft.EntityFrameworkCore;
using Siha.MsA.Api.Domain.Entities;

namespace Siha.MsA.Api.Data;

/// <summary>
/// Contexto de persistencia del Microservicio A (SQL Server DB A).
/// Agrupa seguridad (usuarios/roles), configuración de evaluación (reglas/umbrales) y
/// catálogo académico (carreras, mallas, asignaturas y unidades).
/// </summary>
public class SihaDbContextA : DbContext
{
    public SihaDbContextA(DbContextOptions<SihaDbContextA> options) : base(options)
    {
    }

    // Seguridad (US-01)
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    // Configuración de evaluación (US-02)
    public DbSet<Regla> Reglas => Set<Regla>();
    public DbSet<ParametroEvaluacion> ParametrosEvaluacion => Set<ParametroEvaluacion>();

    // Catálogo académico (US-03)
    public DbSet<Carrera> Carreras => Set<Carrera>();
    public DbSet<MallaCurricular> Mallas => Set<MallaCurricular>();
    public DbSet<Asignatura> Asignaturas => Set<Asignatura>();
    public DbSet<UnidadTematica> UnidadesTematicas => Set<UnidadTematica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Rol>(e =>
        {
            e.ToTable("ROL");
            e.HasIndex(x => x.Nombre).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(30).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(200);
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("USUARIO");
            e.HasIndex(x => x.CorreoInstitucional).IsUnique();
            e.Property(x => x.CorreoInstitucional).HasMaxLength(150).IsRequired();
            e.Property(x => x.NombreCompleto).HasMaxLength(150).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(120).IsRequired();
            e.HasOne(x => x.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(x => x.RolId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Carrera>(e =>
        {
            e.ToTable("CARRERA");
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Facultad).HasMaxLength(60);
        });

        modelBuilder.Entity<MallaCurricular>(e =>
        {
            e.ToTable("MALLA");
            e.HasIndex(x => new { x.CarreraId, x.Version }).IsUnique();
            e.Property(x => x.Version).HasMaxLength(20).IsRequired();
            e.Property(x => x.Fuente).HasMaxLength(200);
            e.Property(x => x.RutaNormativa).HasMaxLength(20);
            e.HasOne(x => x.Carrera)
                .WithMany(c => c.Mallas)
                .HasForeignKey(x => x.CarreraId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Asignatura>(e =>
        {
            e.ToTable("ASIGNATURA");
            e.Property(x => x.Activa).HasDefaultValue(true);
            // CA3 de US-03: códigos duplicados dentro de una malla se rechazan.
            e.HasIndex(x => new { x.MallaId, x.Codigo }).IsUnique();
            e.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Malla)
                .WithMany(m => m.Asignaturas)
                .HasForeignKey(x => x.MallaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UnidadTematica>(e =>
        {
            e.ToTable("UNIDAD_TEMATICA");
            e.HasIndex(x => new { x.AsignaturaId, x.Numero }).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            e.Property(x => x.Contenidos).HasMaxLength(4000);
            e.HasOne(x => x.Asignatura)
                .WithMany(a => a.Unidades)
                .HasForeignKey(x => x.AsignaturaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Regla>(e =>
        {
            e.ToTable("REGLA");
            e.HasIndex(x => new { x.Codigo, x.RutaNormativa, x.Version }).IsUnique();
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.Property(x => x.RutaNormativa).HasMaxLength(20).IsRequired();
            e.Property(x => x.TipoTramite).HasMaxLength(20).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(400);
            e.Property(x => x.Expresion).HasMaxLength(400);
            e.Property(x => x.Version).HasMaxLength(20).IsRequired();
            e.Property(x => x.Fuente).HasMaxLength(200);
        });

        modelBuilder.Entity<ParametroEvaluacion>(e =>
        {
            e.ToTable("PARAMETRO_EVALUACION");
            e.HasIndex(x => new { x.RutaNormativa, x.Version }).IsUnique();
            e.Property(x => x.RutaNormativa).HasMaxLength(20).IsRequired();
            e.Property(x => x.ActualizadoPor).HasMaxLength(150);
        });
    }
}
