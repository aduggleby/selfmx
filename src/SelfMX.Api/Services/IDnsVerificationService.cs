namespace SelfMX.Api.Services;

/// <summary>
/// Result of verifying a single DNS record.
/// </summary>
public record DnsRecordVerificationResult(
    string RecordName,
    string ExpectedValue,
    string? ActualValue,
    bool IsVerified,
    string DnsServer
);

/// <summary>
/// Result of verifying all DNS records for a domain.
/// </summary>
public record DnsVerificationDetailedResult(
    bool AllVerified,
    DnsRecordVerificationResult[] Records
);

public interface IDnsVerificationService
{
    Task<bool> VerifyCnameRecordAsync(
        string recordName,
        string expectedValue,
        CancellationToken ct = default);

    Task<bool> VerifyTxtRecordAsync(
        string recordName,
        string expectedValue,
        CancellationToken ct = default);

    Task<bool> VerifyAllDkimRecordsAsync(
        DnsRecordInfo[] records,
        CancellationToken ct = default);

    /// <summary>
    /// Verify all DNS records (CNAME for DKIM, TXT for SPF/DMARC) and return detailed results.
    /// </summary>
    Task<DnsVerificationDetailedResult> VerifyAllRecordsDetailedAsync(
        DnsRecordInfo[] records,
        CancellationToken ct = default);
}
