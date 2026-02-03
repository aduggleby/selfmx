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
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();
    public DbSet<RevokedApiKey> RevokedApiKeys => Set<RevokedApiKey>();

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

        modelBuilder.Entity<SentEmail>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.MessageId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.FromAddress).HasMaxLength(320).IsRequired(); // RFC 5321 max
            entity.Property(e => e.ToAddresses).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(998).IsRequired(); // RFC 5322 limit
            entity.Property(e => e.DomainId).HasMaxLength(36).IsRequired();
            entity.Property(e => e.ApiKeyId).HasMaxLength(36);

            // Large text - explicit nvarchar(max)
            entity.Property(e => e.HtmlBody).HasColumnType("nvarchar(max)");
            entity.Property(e => e.TextBody).HasColumnType("nvarchar(max)");

            // Indexes for cleanup job and queries
            entity.HasIndex(e => e.SentAt);
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => new { e.DomainId, e.SentAt });

            // Foreign key to Domain (no cascade - keep history)
            entity.HasOne(e => e.Domain)
                .WithMany()
                .HasForeignKey(e => e.DomainId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RevokedApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.KeyPrefix).HasMaxLength(11).IsRequired();
            entity.Property(e => e.LastUsedIp).HasMaxLength(45);
            entity.Property(e => e.AllowedDomainIds).HasMaxLength(1000);
            entity.HasIndex(e => e.RevokedAt);
            entity.HasIndex(e => e.KeyPrefix);
        });
    }
}
