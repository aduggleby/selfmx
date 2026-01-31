using DnsClient;
using DnsClient.Protocol;

namespace SelfMX.Api.Services;

public class DnsVerificationService : IDnsVerificationService
{
    private readonly ILookupClient _dnsClient;
    private readonly ILookupClient _fallbackDnsClient;
    private readonly ILogger<DnsVerificationService> _logger;

    public DnsVerificationService(ILogger<DnsVerificationService> logger)
    {
        _logger = logger;

        // Primary DNS resolver (system default)
        _dnsClient = new LookupClient();

        // Fallback to Google DNS
        _fallbackDnsClient = new LookupClient(
            new LookupClientOptions(new[] { new System.Net.IPAddress(new byte[] { 8, 8, 8, 8 }) })
            {
                UseCache = false,
                Timeout = TimeSpan.FromSeconds(10)
            });
    }

    public async Task<bool> VerifyCnameRecordAsync(
        string recordName,
        string expectedValue,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Verifying CNAME record: {Name} -> {Expected}", recordName, expectedValue);

        try
        {
            // Try primary DNS first
            var result = await QueryCnameAsync(_dnsClient, recordName, ct);
            if (result != null && MatchesCname(result, expectedValue))
            {
                _logger.LogDebug("CNAME record verified via primary DNS");
                return true;
            }

            // Fallback to Google DNS
            result = await QueryCnameAsync(_fallbackDnsClient, recordName, ct);
            if (result != null && MatchesCname(result, expectedValue))
            {
                _logger.LogDebug("CNAME record verified via fallback DNS");
                return true;
            }

            _logger.LogDebug("CNAME record not verified: {Name}", recordName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DNS query failed for {Name}", recordName);
            return false;
        }
    }

    private async Task<IDnsQueryResponse?> QueryCnameAsync(
        ILookupClient client,
        string name,
        CancellationToken ct)
    {
        try
        {
            return await client.QueryAsync(name, QueryType.CNAME, cancellationToken: ct);
        }
        catch (DnsResponseException)
        {
            return null;
        }
    }

    private bool MatchesCname(IDnsQueryResponse response, string expectedValue)
    {
        var cnameRecords = response.Answers.CnameRecords();
        foreach (var record in cnameRecords)
        {
            var canonical = record.CanonicalName.Value.TrimEnd('.');
            if (canonical.Equals(expectedValue.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public async Task<bool> VerifyAllDkimRecordsAsync(
        DnsRecordInfo[] records,
        CancellationToken ct = default)
    {
        foreach (var record in records)
        {
            if (record.Type != "CNAME") continue;

            var verified = await VerifyCnameRecordAsync(record.Name, record.Value, ct);
            if (!verified)
            {
                _logger.LogDebug("DKIM record not verified: {Name}", record.Name);
                return false;
            }
        }

        return true;
    }
}
