using System.IO.Compression;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SelfMX.Api.Contracts;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;
using SelfMX.Api.Jobs;

namespace SelfMX.Api.Services;

public class DataImportException(string message, bool conflict = false) : Exception(message)
{
    public bool Conflict { get; } = conflict;
}

/// <summary>
/// Exports and imports an instance's configuration (domains, API keys incl. hashes,
/// revoked keys) and audit trail. Sent emails and Hangfire state are not included.
/// </summary>
public class DataTransferService(
    AppDbContext db,
    AuditDbContext auditDb,
    IBackgroundJobClient backgroundJobs,
    ILogger<DataTransferService> logger)
{
    private const int BatchSize = 1000;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public async Task<DataExport> ExportAsync(CancellationToken ct = default)
    {
        var domains = await db.Domains.AsNoTracking().OrderBy(d => d.Id).ToListAsync(ct);
        var apiKeys = await db.ApiKeys.AsNoTracking().OrderBy(k => k.Id).ToListAsync(ct);
        var apiKeyDomains = await db.ApiKeyDomains.AsNoTracking().OrderBy(x => x.ApiKeyId).ThenBy(x => x.DomainId).ToListAsync(ct);
        var revoked = await db.RevokedApiKeys.AsNoTracking().OrderBy(k => k.Id).ToListAsync(ct);
        var auditLogs = await auditDb.AuditLogs.AsNoTracking().OrderBy(l => l.Timestamp).ToListAsync(ct);

        return new DataExport(
            DataExport.CurrentFormatVersion,
            SourceVersion(),
            db.Database.ProviderName ?? "unknown",
            DateTime.UtcNow,
            domains.Select(d => new DomainExport(
                d.Id, d.Name, d.Status, d.CreatedAt, d.VerificationStartedAt, d.VerifiedAt,
                d.FailureReason, d.SesIdentityArn, d.DnsRecordsJson, d.LastCheckedAt)).ToArray(),
            apiKeys.Select(k => new ApiKeyExport(
                k.Id, k.Name, k.KeyHash, k.KeySalt, k.KeyPrefix, k.IsAdmin, k.CreatedAt, k.RevokedAt,
                k.LastUsedAt, k.LastUsedIp)).ToArray(),
            apiKeyDomains.Select(x => new ApiKeyDomainExport(x.ApiKeyId, x.DomainId)).ToArray(),
            revoked.Select(k => new RevokedApiKeyExport(
                k.Id, k.Name, k.KeyPrefix, k.IsAdmin, k.CreatedAt, k.RevokedAt, k.ArchivedAt,
                k.LastUsedIp, k.LastUsedAt, k.AllowedDomainIds)).ToArray(),
            auditLogs.Select(l => new AuditLogExport(
                l.Id, l.Timestamp, l.Action, l.ActorType, l.ActorId, l.ResourceType, l.ResourceId,
                l.IpAddress, l.UserAgent, l.StatusCode, l.ErrorMessage, l.Details)).ToArray());
    }

    public async Task WriteExportAsync(Stream output, CancellationToken ct = default)
    {
        var export = await ExportAsync(ct);
        await using var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);
        await JsonSerializer.SerializeAsync(gzip, export, JsonOptions, ct);
    }

    /// <summary>Reads an export file, gzip-compressed or plain JSON.</summary>
    public static async Task<DataExport> ReadExportAsync(Stream input, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var isGzip = buffer.Length >= 2 && buffer.GetBuffer()[0] == 0x1f && buffer.GetBuffer()[1] == 0x8b;
        Stream json = isGzip ? new GZipStream(buffer, CompressionMode.Decompress) : buffer;

        try
        {
            return await JsonSerializer.DeserializeAsync<DataExport>(json, JsonOptions, ct)
                ?? throw new DataImportException("The import file is empty.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw new DataImportException($"The import file is not a valid SelfMX export: {ex.Message}");
        }
    }

    /// <summary>
    /// Imports into an instance without domains or API keys, in one transaction across both
    /// contexts. Audit logs already present (e.g. the admin login on the new instance) are kept.
    /// </summary>
    public async Task<ImportResult> ImportAsync(DataExport data, CancellationToken ct = default)
    {
        if (data.FormatVersion != DataExport.CurrentFormatVersion)
            throw new DataImportException(
                $"Unsupported export format version {data.FormatVersion}; this SelfMX supports version {DataExport.CurrentFormatVersion}.");

        if (await db.Domains.AnyAsync(ct) || await db.ApiKeys.AnyAsync(ct) ||
            await db.ApiKeyDomains.AnyAsync(ct) || await db.RevokedApiKeys.AnyAsync(ct))
        {
            throw new DataImportException(
                "Import requires an instance without domains or API keys. Sign in with the admin password (not an API key) on a fresh installation.",
                conflict: true);
        }

        var domains = data.Domains ?? [];
        var apiKeys = data.ApiKeys ?? [];
        var apiKeyDomains = data.ApiKeyDomains ?? [];
        var revoked = data.RevokedApiKeys ?? [];
        var auditLogs = data.AuditLogs ?? [];

        Require(domains.All(d => d is { Id: not null, Name: not null }), "domain ids or names");
        Require(apiKeys.All(k => k is { Id: not null, Name: not null, KeyHash: not null, KeySalt: not null, KeyPrefix: not null }),
            "API key ids, names, hashes, salts or prefixes");
        Require(apiKeyDomains.All(x => x is { ApiKeyId: not null, DomainId: not null }), "API key domain links");
        Require(revoked.All(k => k is { Id: not null, Name: not null, KeyPrefix: not null }), "revoked API key fields");
        Require(auditLogs.All(l => l is { Id: not null, Action: not null, ActorType: not null, ResourceType: not null }),
            "audit log fields");

        var auditImported = 0;
        var auditSkipped = 0;

        // One transaction for both contexts (same database): the audit context joins the app
        // context's connection, so a rejected import leaves nothing behind.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            auditDb.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            auditDb.Database.SetDbConnection(db.Database.GetDbConnection());
            await auditDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);

            db.Domains.AddRange(domains.Select(d => new Domain
            {
                Id = d.Id, Name = d.Name.ToLowerInvariant(), Status = d.Status, CreatedAt = d.CreatedAt,
                VerificationStartedAt = d.VerificationStartedAt, VerifiedAt = d.VerifiedAt,
                FailureReason = d.FailureReason, SesIdentityArn = d.SesIdentityArn,
                DnsRecordsJson = d.DnsRecordsJson, LastCheckedAt = d.LastCheckedAt
            }));
            db.ApiKeys.AddRange(apiKeys.Select(k => new ApiKey
            {
                Id = k.Id, Name = k.Name, KeyHash = k.KeyHash, KeySalt = k.KeySalt, KeyPrefix = k.KeyPrefix,
                IsAdmin = k.IsAdmin, CreatedAt = k.CreatedAt, RevokedAt = k.RevokedAt,
                LastUsedAt = k.LastUsedAt, LastUsedIp = k.LastUsedIp
            }));
            await SaveAsync(db, ct);

            db.ApiKeyDomains.AddRange(apiKeyDomains.Select(x => new ApiKeyDomain { ApiKeyId = x.ApiKeyId, DomainId = x.DomainId }));
            db.RevokedApiKeys.AddRange(revoked.Select(k => new RevokedApiKey
            {
                Id = k.Id, Name = k.Name, KeyPrefix = k.KeyPrefix, IsAdmin = k.IsAdmin, CreatedAt = k.CreatedAt,
                RevokedAt = k.RevokedAt, ArchivedAt = k.ArchivedAt, LastUsedIp = k.LastUsedIp,
                LastUsedAt = k.LastUsedAt, AllowedDomainIds = k.AllowedDomainIds
            }));
            await SaveAsync(db, ct);

            (auditImported, auditSkipped) = await ImportAuditLogsAsync(auditLogs, ct);

            await transaction.CommitAsync(ct);
        });
        auditDb.ChangeTracker.Clear();
        db.ChangeTracker.Clear();

        // Domains whose SES setup never finished on the source get their setup job here
        var pending = domains.Where(d => d.Status == DomainStatus.Pending).Select(d => d.Id).ToArray();
        foreach (var id in pending)
            backgroundJobs.Enqueue<SetupDomainJob>(job => job.ExecuteAsync(id, null));

        logger.LogInformation(
            "Imported {Domains} domains, {ApiKeys} API keys, {Revoked} revoked keys, {Audit} audit logs ({Skipped} skipped) from SelfMX {Version}",
            domains.Length, apiKeys.Length, revoked.Length, auditImported, auditSkipped, data.SourceVersion);

        return new ImportResult(domains.Length, apiKeys.Length, apiKeyDomains.Length, revoked.Length,
            auditImported, auditSkipped, pending.Length);
    }

    // Merges audit logs: ids already present (e.g. the admin login on the new instance) or
    // repeated within the file are skipped.
    private async Task<(int Imported, int Skipped)> ImportAuditLogsAsync(AuditLogExport[] logs, CancellationToken ct)
    {
        var existing = (await auditDb.AuditLogs.Select(l => l.Id).ToListAsync(ct)).ToHashSet();
        var toImport = logs.Where(l => existing.Add(l.Id)).ToArray();

        foreach (var batch in toImport.Chunk(BatchSize))
        {
            auditDb.AuditLogs.AddRange(batch.Select(l => new AuditLog
            {
                Id = l.Id, Timestamp = l.Timestamp, Action = l.Action, ActorType = l.ActorType,
                ActorId = l.ActorId, ResourceType = l.ResourceType, ResourceId = l.ResourceId,
                IpAddress = l.IpAddress, UserAgent = l.UserAgent, StatusCode = l.StatusCode,
                ErrorMessage = l.ErrorMessage, Details = l.Details
            }));
            await SaveAsync(auditDb, ct);
            auditDb.ChangeTracker.Clear();
        }

        return (toImport.Length, logs.Length - toImport.Length);
    }

    private static void Require(bool valid, string what)
    {
        if (!valid)
            throw new DataImportException($"The import file is missing required {what}.");
    }

    private static async Task SaveAsync(DbContext context, CancellationToken ct)
    {
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw new DataImportException(
                $"The import file contains invalid or inconsistent data: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private static string SourceVersion()
    {
        var version = typeof(DataTransferService).Assembly.GetName().Version;
        return version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
