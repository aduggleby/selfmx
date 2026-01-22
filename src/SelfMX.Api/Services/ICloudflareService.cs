namespace SelfMX.Api.Services;

public interface ICloudflareService
{
    Task<string> CreateDnsRecordAsync(
        string type,
        string name,
        string content,
        int priority = 0,
        bool proxied = false,
        CancellationToken ct = default);

    Task DeleteDnsRecordAsync(string recordId, CancellationToken ct = default);

    Task<List<DnsRecordResult>> ListDnsRecordsAsync(
        string? name = null,
        string? type = null,
        CancellationToken ct = default);

    Task DeleteDnsRecordsForDomainAsync(string domainName, CancellationToken ct = default);
}
