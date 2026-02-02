namespace SelfMX.Api.Services;

/// <summary>
/// Detailed DKIM verification status from AWS SES.
/// </summary>
public record DkimVerificationResult(
    bool IsVerified,
    string Status,
    string SigningAttributesOrigin,
    string CurrentSigningKeyLength,
    DateTime? LastKeyGenerationTimestamp,
    string[]? Tokens
);

public interface ISesService
{
    Task<(string IdentityArn, DnsRecordInfo[] DnsRecords)> CreateDomainIdentityAsync(
        string domainName, CancellationToken ct = default);

    Task<bool> CheckDkimVerificationAsync(string domainName, CancellationToken ct = default);

    /// <summary>
    /// Get detailed DKIM verification status from AWS SES.
    /// </summary>
    Task<DkimVerificationResult> GetDkimVerificationDetailsAsync(string domainName, CancellationToken ct = default);

    Task DeleteDomainIdentityAsync(string domainName, CancellationToken ct = default);

    Task<string> SendEmailAsync(
        string from,
        string[] to,
        string subject,
        string? html,
        string? text,
        string[]? cc = null,
        string[]? bcc = null,
        string[]? replyTo = null,
        CancellationToken ct = default);
}
