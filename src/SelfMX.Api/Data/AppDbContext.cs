using Microsoft.EntityFrameworkCore;
using SelfMX.Api.Entities;

namespace SelfMX.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Domain> Domains => Set<Domain>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ApiKeyDomain> ApiKeyDomains => Set<ApiKeyDomain>();

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

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.KeyHash).HasMaxLength(64).IsRequired(); // SHA256 base64
            entity.Property(e => e.KeySalt).HasMaxLength(24).IsRequired(); // 16 bytes base64
            entity.Property(e => e.KeyPrefix).HasMaxLength(11).IsRequired();
            entity.Property(e => e.LastUsedIp).HasMaxLength(45);
            entity.HasIndex(e => e.KeyPrefix);
        });

        modelBuilder.Entity<ApiKeyDomain>(entity =>
        {
            entity.HasKey(e => new { e.ApiKeyId, e.DomainId });
            entity.HasOne(e => e.ApiKey)
                .WithMany(k => k.AllowedDomains)
                .HasForeignKey(e => e.ApiKeyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Domain)
                .WithMany()
                .HasForeignKey(e => e.DomainId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent orphaned keys
        });
    }
}
