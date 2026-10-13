using SelfMX.Api.Entities;

namespace SelfMX.Api.Contracts;

// Versioned file format for moving an instance's configuration and audit trail
// between installations. Kept separate from the entities so the format stays stable.

public record DataExport(
    int FormatVersion,
    string SourceVersion,
    string SourceDatabase,
    DateTime ExportedAt,
    DomainExport[] Domains,
    ApiKeyExport[] ApiKeys,
    ApiKeyDomainExport[] ApiKeyDomains,
    RevokedApiKeyExport[] RevokedApiKeys,
    AuditLogExport[] AuditLogs)
{
    public const int CurrentFormatVersion = 1;
}

public record DomainExport(
    string Id, string Name, DomainStatus Status, DateTime CreatedAt, DateTime? VerificationStartedAt,
    DateTime? VerifiedAt, string? FailureReason, string? SesIdentityArn, string? DnsRecordsJson,
    DateTime? LastCheckedAt);

public record ApiKeyExport(
    string Id, string Name, string KeyHash, string KeySalt, string KeyPrefix, bool IsAdmin,
    DateTime CreatedAt, DateTime? RevokedAt, DateTime? LastUsedAt, string? LastUsedIp);

public record ApiKeyDomainExport(string ApiKeyId, string DomainId);

public record RevokedApiKeyExport(
    string Id, string Name, string KeyPrefix, bool IsAdmin, DateTime CreatedAt, DateTime RevokedAt,
    DateTime ArchivedAt, string? LastUsedIp, DateTime? LastUsedAt, string? AllowedDomainIds);

public record AuditLogExport(
    string Id, DateTime Timestamp, string Action, string ActorType, string? ActorId, string ResourceType,
    string? ResourceId, string? IpAddress, string? UserAgent, int StatusCode, string? ErrorMessage,
    string? Details);

public record ImportResult(
    int Domains, int ApiKeys, int ApiKeyDomains, int RevokedApiKeys, int AuditLogs,
    int AuditLogsSkipped, int PendingDomainsRequeued);
