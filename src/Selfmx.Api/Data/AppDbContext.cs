using Microsoft.EntityFrameworkCore;
using Selfmx.Api.Entities;

namespace Selfmx.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Domain> Domains => Set<Domain>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}
