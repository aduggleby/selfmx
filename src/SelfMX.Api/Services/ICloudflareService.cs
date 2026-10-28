namespace SelfMX.Api.Services;

public interface ICloudflareService
{
    /// <summary>Finds the zone ID for a hostname; throws <see cref="CloudflareZoneNotFoundException"/> if none matches.</summary>
    Task<string> ResolveZoneIdAsync(string name, CancellationToken ct = default);

    Task<string> CreateDnsRecordAsync(
        string zoneId,
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

    /// <summary>Deletes only the given records (matched by type, name and content) from the domain's zone.</summary>
    Task DeleteDnsRecordsForDomainAsync(
        string domainName,
        IReadOnlyCollection<DnsRecordInfo> records,
        CancellationToken ct = default);
}
