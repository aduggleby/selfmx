namespace Selfmx.Api.Services;

public interface ISesService
{
    Task<(string IdentityArn, DnsRecordInfo[] DnsRecords)> CreateDomainIdentityAsync(
        string domainName, CancellationToken ct = default);

    Task<bool> CheckDkimVerificationAsync(string domainName, CancellationToken ct = default);

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
