using DevNotes.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNotes.Web.Data;

public class DevNotesDbContext : DbContext
{
    public DevNotesDbContext(DbContextOptions<DevNotesDbContext> options)
        : base(options)
    {
    }

    public DbSet<Nota> Notas => Set<Nota>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Tecnologia> Tecnologias => Set<Tecnologia>();
    public DbSet<NotaTecnologia> NotasTecnologias => Set<NotaTecnologia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>(entidade =>
        {
            entidade.Property(c => c.Nome).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<Tecnologia>(entidade =>
        {
            entidade.Property(t => t.Nome).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<Nota>(entidade =>
        {
            entidade.Property(n => n.Titulo).IsRequired().HasMaxLength(200);
            entidade.Property(n => n.Conteudo).IsRequired();
            entidade.Property(n => n.Estado)
                .HasConversion(e => e.ToString(), valor => Enum.Parse<NotaEstado>(valor))
                .HasMaxLength(20);

            entidade.HasIndex(n => n.Titulo);
            entidade.HasIndex(n => n.Estado);

            entidade.HasOne(n => n.Categoria)
                .WithMany(c => c.Notas)
                .HasForeignKey(n => n.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotaTecnologia>(entidade =>
        {
            entidade.HasKey(nt => new { nt.NotaId, nt.TecnologiaId });

            entidade.HasIndex(nt => nt.TecnologiaId);

            entidade.HasOne(nt => nt.Nota)
                .WithMany(n => n.Tecnologias)
                .HasForeignKey(nt => nt.NotaId)
                .OnDelete(DeleteBehavior.Restrict);

            entidade.HasOne(nt => nt.Tecnologia)
                .WithMany(t => t.Notas)
                .HasForeignKey(nt => nt.TecnologiaId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
