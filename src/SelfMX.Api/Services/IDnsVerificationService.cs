namespace SelfMX.Api.Services;

public interface IDnsVerificationService
{
    Task<bool> VerifyCnameRecordAsync(
        string recordName,
        string expectedValue,
        CancellationToken ct = default);

    Task<bool> VerifyAllDkimRecordsAsync(
        DnsRecordInfo[] records,
        CancellationToken ct = default);
}
