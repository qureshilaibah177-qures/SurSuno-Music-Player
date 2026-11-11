using Microsoft.EntityFrameworkCore;
using SurSuno.Api.Models;

namespace SurSuno.Api.Data;

public class SurSunoDbContext(DbContextOptions<SurSunoDbContext> options) : DbContext(options)
{
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Song>(entity =>
        {
            entity.HasIndex(s => s.Title);
            entity.HasIndex(s => s.ArtistId);
            entity.HasIndex(s => s.AlbumId);
            entity.HasIndex(s => s.GenreId);
        });

        modelBuilder.Entity<Artist>(entity =>
        {
            entity.HasIndex(a => a.Name).IsUnique();
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasIndex(g => g.Name).IsUnique();
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
        });
    }
}