using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;

namespace SelfMX.Api.Services;

/// <summary>
/// Migrates data from SQLite databases to SQL Server.
/// Supports migration of Domains, ApiKeys, ApiKeyDomains, and AuditLogs.
/// </summary>
public class DataMigrationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataMigrationService> _logger;

    private const int AuditLogBatchSize = 10000;
    private const string MigrationStateFile = ".migration-state";

    public DataMigrationService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DataMigrationService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets the path to the migration state file.
    /// </summary>
    private string GetMigrationStatePath()
    {
        var dataPath = _configuration["SELFMX_DATA_PATH"] ?? ".";
        return Path.Combine(dataPath, MigrationStateFile);
    }

    /// <summary>
    /// Checks if migration is needed (SQLite files exist and not yet migrated).
    /// </summary>
    public async Task<MigrationStatus> CheckMigrationStatusAsync()
    {
        var statePath = GetMigrationStatePath();

        // Check if migration was already completed
        if (File.Exists(statePath))
        {
            var state = await File.ReadAllTextAsync(statePath);
            if (state.StartsWith("COMPLETE"))
            {
                return new MigrationStatus { State = MigrationState.Complete };
            }
            if (state.StartsWith("IN_PROGRESS"))
            {
                return new MigrationStatus { State = MigrationState.InProgress };
            }
            if (state.StartsWith("FAILED"))
            {
                return new MigrationStatus
                {
                    State = MigrationState.Failed,
                    ErrorMessage = state.Replace("FAILED:", "").Trim()
                };
            }
        }

        // Check for SQLite database files
        var defaultConn = _configuration.GetConnectionString("DefaultConnection") ?? "";
        var sqlitePath = ExtractSqlitePath(defaultConn);

        if (!string.IsNullOrEmpty(sqlitePath) && File.Exists(sqlitePath))
        {
            return new MigrationStatus
            {
                State = MigrationState.Pending,
                SourceDatabasePath = sqlitePath
            };
        }

        return new MigrationStatus { State = MigrationState.NotNeeded };
    }

    /// <summary>
    /// Migrates data from SQLite to SQL Server.
    /// </summary>
    public async Task<MigrationResult> MigrateAsync(string sqliteMainDbPath, CancellationToken ct = default)
    {
        var result = new MigrationResult();
        var statePath = GetMigrationStatePath();

        _logger.LogInformation("Starting data migration from SQLite to SQL Server");
        _logger.LogInformation("Source: {SqlitePath}", sqliteMainDbPath);

        try
        {
            // Step 1: Create backups
            await BackupSqliteDatabases(sqliteMainDbPath);

            // Step 2: Mark migration as in progress
            await File.WriteAllTextAsync(statePath, "IN_PROGRESS", ct);

            using var scope = _serviceProvider.CreateScope();
            var targetAppDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var targetAuditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

            // Step 3: Ensure target schema exists
            await targetAppDb.Database.EnsureCreatedAsync(ct);
            await targetAuditDb.Database.EnsureCreatedAsync(ct);

            // Step 4: Migrate main database tables
            await using var mainConn = new SqliteConnection($"Data Source={sqliteMainDbPath}");
            await mainConn.OpenAsync(ct);

            result.DomainsCount = await MigrateDomainsAsync(mainConn, targetAppDb, ct);
            result.ApiKeysCount = await MigrateApiKeysAsync(mainConn, targetAppDb, ct);
            result.ApiKeyDomainsCount = await MigrateApiKeyDomainsAsync(mainConn, targetAppDb, ct);

            // Step 5: Migrate audit database
            var auditDbPath = sqliteMainDbPath.Replace("selfmx.db", "audit.db");
            if (File.Exists(auditDbPath))
            {
                await using var auditConn = new SqliteConnection($"Data Source={auditDbPath}");
                await auditConn.OpenAsync(ct);
                result.AuditLogsCount = await MigrateAuditLogsAsync(auditConn, targetAuditDb, ct);
            }

            // Step 6: Verify migration
            var verified = await VerifyMigrationAsync(targetAppDb, targetAuditDb, result, ct);
            if (!verified)
            {
                throw new InvalidOperationException("Migration verification failed - row counts do not match");
            }

            // Step 7: Mark migration complete
            await File.WriteAllTextAsync(statePath,
                $"COMPLETE:{DateTime.UtcNow:O}\n" +
                $"Domains:{result.DomainsCount}\n" +
                $"ApiKeys:{result.ApiKeysCount}\n" +
                $"ApiKeyDomains:{result.ApiKeyDomainsCount}\n" +
                $"AuditLogs:{result.AuditLogsCount}", ct);

            result.Success = true;
            _logger.LogInformation("Migration completed successfully. " +
                "Domains: {Domains}, ApiKeys: {ApiKeys}, ApiKeyDomains: {ApiKeyDomains}, AuditLogs: {AuditLogs}",
                result.DomainsCount, result.ApiKeysCount, result.ApiKeyDomainsCount, result.AuditLogsCount);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            await File.WriteAllTextAsync(statePath, $"FAILED:{ex.Message}", ct);
            _logger.LogError(ex, "Migration failed");
        }

        return result;
    }

    private async Task BackupSqliteDatabases(string mainDbPath)
    {
        var backups = new[]
        {
            mainDbPath,
            mainDbPath.Replace("selfmx.db", "audit.db"),
            mainDbPath.Replace("selfmx.db", "selfmx-hangfire.db")
        };

        foreach (var dbPath in backups.Where(File.Exists))
        {
            var backupPath = $"{dbPath}.migrated.bak";
            File.Copy(dbPath, backupPath, overwrite: true);
            _logger.LogInformation("Created backup: {BackupPath}", backupPath);
        }
    }

    private async Task<int> MigrateDomainsAsync(SqliteConnection source, AppDbContext target, CancellationToken ct)
    {
        _logger.LogInformation("Migrating Domains...");

        var domains = new List<Domain>();
        await using var cmd = source.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Status, CreatedAt, VerificationStartedAt, VerifiedAt, FailureReason, SesIdentityArn, DnsRecordsJson FROM Domains";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            domains.Add(new Domain
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Status = Enum.Parse<DomainStatus>(reader.GetString(2)),
                CreatedAt = DateTime.Parse(reader.GetString(3)),
                VerificationStartedAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
                VerifiedAt = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                FailureReason = reader.IsDBNull(6) ? null : reader.GetString(6),
                SesIdentityArn = reader.IsDBNull(7) ? null : reader.GetString(7),
                DnsRecordsJson = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        if (domains.Count > 0)
        {
            target.Domains.AddRange(domains);
            await target.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Migrated {Count} domains", domains.Count);
        return domains.Count;
    }

    private async Task<int> MigrateApiKeysAsync(SqliteConnection source, AppDbContext target, CancellationToken ct)
    {
        _logger.LogInformation("Migrating ApiKeys...");

        var apiKeys = new List<ApiKey>();
        await using var cmd = source.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, KeyHash, KeySalt, KeyPrefix, IsAdmin, CreatedAt, RevokedAt, LastUsedAt, LastUsedIp FROM ApiKeys";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            apiKeys.Add(new ApiKey
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                KeyHash = reader.GetString(2),
                KeySalt = reader.GetString(3),
                KeyPrefix = reader.GetString(4),
                IsAdmin = reader.GetInt64(5) != 0,
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                RevokedAt = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                LastUsedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                LastUsedIp = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }

        if (apiKeys.Count > 0)
        {
            target.ApiKeys.AddRange(apiKeys);
            await target.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Migrated {Count} API keys", apiKeys.Count);
        return apiKeys.Count;
    }

    private async Task<int> MigrateApiKeyDomainsAsync(SqliteConnection source, AppDbContext target, CancellationToken ct)
    {
        _logger.LogInformation("Migrating ApiKeyDomains...");

        var apiKeyDomains = new List<ApiKeyDomain>();
        await using var cmd = source.CreateCommand();
        cmd.CommandText = "SELECT ApiKeyId, DomainId FROM ApiKeyDomains";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            apiKeyDomains.Add(new ApiKeyDomain
            {
                ApiKeyId = reader.GetString(0),
                DomainId = reader.GetString(1)
            });
        }

        if (apiKeyDomains.Count > 0)
        {
            target.ApiKeyDomains.AddRange(apiKeyDomains);
            await target.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Migrated {Count} API key-domain associations", apiKeyDomains.Count);
        return apiKeyDomains.Count;
    }

    private async Task<int> MigrateAuditLogsAsync(SqliteConnection source, AuditDbContext target, CancellationToken ct)
    {
        _logger.LogInformation("Migrating AuditLogs (batched)...");

        var totalMigrated = 0;
        var offset = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var batch = new List<AuditLog>();
            await using var cmd = source.CreateCommand();
            cmd.CommandText = $"SELECT Id, Timestamp, Action, ActorType, ActorId, ResourceType, ResourceId, IpAddress, UserAgent, StatusCode, ErrorMessage, Details FROM AuditLogs LIMIT {AuditLogBatchSize} OFFSET {offset}";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                batch.Add(new AuditLog
                {
                    Id = reader.GetString(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    Action = reader.GetString(2),
                    ActorType = reader.GetString(3),
                    ActorId = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ResourceType = reader.GetString(5),
                    ResourceId = reader.IsDBNull(6) ? null : reader.GetString(6),
                    IpAddress = reader.IsDBNull(7) ? null : reader.GetString(7),
                    UserAgent = reader.IsDBNull(8) ? null : reader.GetString(8),
                    StatusCode = reader.GetInt32(9),
                    ErrorMessage = reader.IsDBNull(10) ? null : reader.GetString(10),
                    Details = reader.IsDBNull(11) ? null : reader.GetString(11)
                });
            }

            if (batch.Count == 0)
                break;

            target.AuditLogs.AddRange(batch);
            await target.SaveChangesAsync(ct);

            totalMigrated += batch.Count;
            offset += AuditLogBatchSize;

            _logger.LogInformation("Migrated {Total} audit logs...", totalMigrated);
        }

        _logger.LogInformation("Completed migrating {Total} audit logs", totalMigrated);
        return totalMigrated;
    }

    private async Task<bool> VerifyMigrationAsync(AppDbContext appDb, AuditDbContext auditDb, MigrationResult result, CancellationToken ct)
    {
        var domainsCount = await appDb.Domains.CountAsync(ct);
        var apiKeysCount = await appDb.ApiKeys.CountAsync(ct);
        var apiKeyDomainsCount = await appDb.ApiKeyDomains.CountAsync(ct);
        var auditLogsCount = await auditDb.AuditLogs.CountAsync(ct);

        var verified = domainsCount == result.DomainsCount
            && apiKeysCount == result.ApiKeysCount
            && apiKeyDomainsCount == result.ApiKeyDomainsCount
            && auditLogsCount == result.AuditLogsCount;

        _logger.LogInformation("Verification: Domains {ExpectedD}/{ActualD}, ApiKeys {ExpectedA}/{ActualA}, " +
            "ApiKeyDomains {ExpectedAD}/{ActualAD}, AuditLogs {ExpectedAL}/{ActualAL}",
            result.DomainsCount, domainsCount,
            result.ApiKeysCount, apiKeysCount,
            result.ApiKeyDomainsCount, apiKeyDomainsCount,
            result.AuditLogsCount, auditLogsCount);

        return verified;
    }

    private static string? ExtractSqlitePath(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return null;

        // Handle "Data Source=/path/to/file.db" format
        if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString["Data Source=".Length..].Trim().TrimEnd(';');
        }

        return null;
    }
}

public enum MigrationState
{
    NotNeeded,
    Pending,
    InProgress,
    Complete,
    Failed
}

public class MigrationStatus
{
    public MigrationState State { get; set; }
    public string? SourceDatabasePath { get; set; }
    public string? ErrorMessage { get; set; }
}

public class MigrationResult
{
    public bool Success { get; set; }
    public int DomainsCount { get; set; }
    public int ApiKeysCount { get; set; }
    public int ApiKeyDomainsCount { get; set; }
    public int AuditLogsCount { get; set; }
    public string? ErrorMessage { get; set; }
}
