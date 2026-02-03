using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;

namespace SelfMX.Api.Jobs;

/// <summary>
/// Archives revoked API keys after 90 days, then deletes the original.
/// Runs daily.
/// </summary>
public class CleanupRevokedApiKeysJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<CleanupRevokedApiKeysJob> _logger;
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(90);

    public CleanupRevokedApiKeysJob(AppDbContext db, ILogger<CleanupRevokedApiKeysJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync(PerformContext? context)
    {
        var cutoffDate = DateTime.UtcNow - RetentionPeriod;

        context?.WriteLine($"Looking for API keys revoked before {cutoffDate:u}...");

        // Find keys revoked more than 90 days ago
        var keysToArchive = await _db.ApiKeys
            .Include(k => k.AllowedDomains)
            .Where(k => k.RevokedAt != null && k.RevokedAt < cutoffDate)
            .ToListAsync();

        if (keysToArchive.Count == 0)
        {
            context?.WriteLine("No revoked keys to archive.");
            _logger.LogInformation("CleanupRevokedApiKeys: No keys to archive");
            return;
        }

        context?.WriteLine($"Found {keysToArchive.Count} revoked key(s) to archive.");
        _logger.LogInformation("CleanupRevokedApiKeys: Archiving {Count} keys", keysToArchive.Count);

        foreach (var key in keysToArchive)
        {
            // Create archived record
            var archived = new RevokedApiKey
            {
                Id = key.Id,
                Name = key.Name,
                KeyPrefix = key.KeyPrefix,
                IsAdmin = key.IsAdmin,
                CreatedAt = key.CreatedAt,
                RevokedAt = key.RevokedAt!.Value,
                ArchivedAt = DateTime.UtcNow,
                LastUsedIp = key.LastUsedIp,
                LastUsedAt = key.LastUsedAt,
                AllowedDomainIds = key.AllowedDomains.Any()
                    ? string.Join(",", key.AllowedDomains.Select(d => d.DomainId))
                    : null
            };

            _db.RevokedApiKeys.Add(archived);

            context?.WriteLine($"  Archiving: {key.KeyPrefix} ({key.Name})");
            _logger.LogInformation("CleanupRevokedApiKeys: Archiving key {Prefix} ({Name})",
                key.KeyPrefix, key.Name);
        }

        // Delete the original keys (cascades to ApiKeyDomains)
        _db.ApiKeys.RemoveRange(keysToArchive);

        await _db.SaveChangesAsync();

        context?.WriteLine($"Archived and deleted {keysToArchive.Count} revoked key(s).");
        _logger.LogInformation("CleanupRevokedApiKeys: Completed, archived {Count} keys", keysToArchive.Count);
    }
}
