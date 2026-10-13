using Microsoft.EntityFrameworkCore;
using SelfMX.Api.Entities;

namespace SelfMX.Api.Data;

public class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
    {
    }

    // Used by the provider-specific subclasses that own the migrations
    protected AuditDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        UtcDateTimeValueConverter.Apply(configurationBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(36);
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ActorType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ActorId).HasMaxLength(50);
            entity.Property(e => e.ResourceType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ResourceId).HasMaxLength(100); // SES message IDs are ~61 chars
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);
            entity.Property(e => e.Details).HasMaxLength(AuditLog.MaxDetailsLength);
            entity.HasIndex(e => e.Timestamp).IsDescending();
            entity.HasIndex(e => new { e.ActorType, e.ActorId });
            entity.HasIndex(e => e.Action);
        });
    }
}
