using DnsClient;
using DnsClient.Protocol;

namespace SelfMX.Api.Services;

public class DnsVerificationService : IDnsVerificationService
{
    private readonly ILookupClient _dnsClient;
    private readonly ILookupClient _cloudflareDnsClient;
    private readonly ILookupClient _googleDnsClient;
    private readonly ILogger<DnsVerificationService> _logger;

    public DnsVerificationService(ILogger<DnsVerificationService> logger)
    {
        _logger = logger;

        // Primary DNS resolver (system default) - disable caching to always get fresh records
        _dnsClient = new LookupClient(
            new LookupClientOptions()
            {
                UseCache = false,
                Timeout = TimeSpan.FromSeconds(10)
            });

        // Cloudflare DNS (1.1.1.1) - typically has faster cache updates
        _cloudflareDnsClient = new LookupClient(
            new LookupClientOptions(new[] { new System.Net.IPAddress(new byte[] { 1, 1, 1, 1 }) })
            {
                UseCache = false,
                Timeout = TimeSpan.FromSeconds(10)
            });

        // Google DNS (8.8.8.8) - reliable fallback
        _googleDnsClient = new LookupClient(
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

            // Fallback to Cloudflare DNS (typically has faster cache updates)
            result = await QueryCnameAsync(_cloudflareDnsClient, recordName, ct);
            if (result != null && MatchesCname(result, expectedValue))
            {
                _logger.LogDebug("CNAME record verified via Cloudflare DNS");
                return true;
            }

            // Fallback to Google DNS
            result = await QueryCnameAsync(_googleDnsClient, recordName, ct);
            if (result != null && MatchesCname(result, expectedValue))
            {
                _logger.LogDebug("CNAME record verified via Google DNS");
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

    public async Task<bool> VerifyTxtRecordAsync(
        string recordName,
        string expectedValue,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Verifying TXT record: {Name} -> {Expected}", recordName, expectedValue);

        try
        {
            // Try primary DNS first
            var result = await QueryTxtAsync(_dnsClient, recordName, ct);
            if (result != null && MatchesTxt(result, expectedValue))
            {
                _logger.LogDebug("TXT record verified via primary DNS");
                return true;
            }

            // Fallback to Cloudflare DNS
            result = await QueryTxtAsync(_cloudflareDnsClient, recordName, ct);
            if (result != null && MatchesTxt(result, expectedValue))
            {
                _logger.LogDebug("TXT record verified via Cloudflare DNS");
                return true;
            }

            // Fallback to Google DNS
            result = await QueryTxtAsync(_googleDnsClient, recordName, ct);
            if (result != null && MatchesTxt(result, expectedValue))
            {
                _logger.LogDebug("TXT record verified via Google DNS");
                return true;
            }

            _logger.LogDebug("TXT record not verified: {Name}", recordName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DNS TXT query failed for {Name}", recordName);
            return false;
        }
    }

    private async Task<IDnsQueryResponse?> QueryTxtAsync(
        ILookupClient client,
        string name,
        CancellationToken ct)
    {
        try
        {
            return await client.QueryAsync(name, QueryType.TXT, cancellationToken: ct);
        }
        catch (DnsResponseException)
        {
            return null;
        }
    }

    private bool MatchesTxt(IDnsQueryResponse response, string expectedValue)
    {
        var txtRecords = response.Answers.TxtRecords();
        foreach (var record in txtRecords)
        {
            // TXT records can have multiple strings that need to be concatenated
            var fullValue = string.Join("", record.Text);
            if (fullValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase))
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

    public async Task<DnsVerificationDetailedResult> VerifyAllRecordsDetailedAsync(
        DnsRecordInfo[] records,
        CancellationToken ct = default)
    {
        var results = new List<DnsRecordVerificationResult>();

        foreach (var record in records)
        {
            (bool verified, string? actualValue, string dnsServer) result;

            if (record.Type == "CNAME")
            {
                result = await VerifyCnameRecordDetailedAsync(record.Name, record.Value, ct);
            }
            else if (record.Type == "TXT")
            {
                result = await VerifyTxtRecordDetailedAsync(record.Name, record.Value, ct);
            }
            else
            {
                // Skip unsupported record types
                continue;
            }

            results.Add(new DnsRecordVerificationResult(
                RecordName: record.Name,
                ExpectedValue: record.Value,
                ActualValue: result.actualValue,
                IsVerified: result.verified,
                DnsServer: result.dnsServer
            ));
        }

        return new DnsVerificationDetailedResult(
            AllVerified: results.All(r => r.IsVerified),
            Records: results.ToArray()
        );
    }

    private async Task<(bool Verified, string? ActualValue, string DnsServer)> VerifyCnameRecordDetailedAsync(
        string recordName,
        string expectedValue,
        CancellationToken ct = default)
    {
        try
        {
            // Try primary DNS first
            var result = await QueryCnameAsync(_dnsClient, recordName, ct);
            if (result != null)
            {
                var actualValue = GetCnameValue(result);
                if (MatchesCname(result, expectedValue))
                {
                    return (true, actualValue, "System DNS");
                }
                // Record found but doesn't match
                return (false, actualValue, "System DNS");
            }

            // Fallback to Cloudflare DNS (typically has faster cache updates)
            result = await QueryCnameAsync(_cloudflareDnsClient, recordName, ct);
            if (result != null)
            {
                var actualValue = GetCnameValue(result);
                if (MatchesCname(result, expectedValue))
                {
                    return (true, actualValue, "Cloudflare DNS (1.1.1.1)");
                }
                // Record found but doesn't match
                return (false, actualValue, "Cloudflare DNS (1.1.1.1)");
            }

            // Fallback to Google DNS
            result = await QueryCnameAsync(_googleDnsClient, recordName, ct);
            if (result != null)
            {
                var actualValue = GetCnameValue(result);
                if (MatchesCname(result, expectedValue))
                {
                    return (true, actualValue, "Google DNS (8.8.8.8)");
                }
                // Record found but doesn't match
                return (false, actualValue, "Google DNS (8.8.8.8)");
            }

            return (false, null, "Not found in any DNS");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DNS query failed for {Name}", recordName);
            return (false, $"Error: {ex.Message}", "Query failed");
        }
    }

    private string? GetCnameValue(IDnsQueryResponse response)
    {
        var cnameRecords = response.Answers.CnameRecords();
        var first = cnameRecords.FirstOrDefault();
        return first?.CanonicalName.Value.TrimEnd('.');
    }

    private async Task<(bool Verified, string? ActualValue, string DnsServer)> VerifyTxtRecordDetailedAsync(
        string recordName,
        string expectedValue,
        CancellationToken ct = default)
    {
        try
        {
            // Try primary DNS first
            var result = await QueryTxtAsync(_dnsClient, recordName, ct);
            if (result != null)
            {
                var actualValue = GetTxtValue(result);
                if (MatchesTxt(result, expectedValue))
                {
                    return (true, actualValue, "System DNS");
                }
                if (actualValue != null)
                {
                    return (false, actualValue, "System DNS");
                }
            }

            // Fallback to Cloudflare DNS
            result = await QueryTxtAsync(_cloudflareDnsClient, recordName, ct);
            if (result != null)
            {
                var actualValue = GetTxtValue(result);
                if (MatchesTxt(result, expectedValue))
                {
                    return (true, actualValue, "Cloudflare DNS (1.1.1.1)");
                }
                if (actualValue != null)
                {
                    return (false, actualValue, "Cloudflare DNS (1.1.1.1)");
                }
            }

            // Fallback to Google DNS
            result = await QueryTxtAsync(_googleDnsClient, recordName, ct);
            if (result != null)
            {
                var actualValue = GetTxtValue(result);
                if (MatchesTxt(result, expectedValue))
                {
                    return (true, actualValue, "Google DNS (8.8.8.8)");
                }
                if (actualValue != null)
                {
                    return (false, actualValue, "Google DNS (8.8.8.8)");
                }
            }

            return (false, null, "Not found in any DNS");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DNS TXT query failed for {Name}", recordName);
            return (false, $"Error: {ex.Message}", "Query failed");
        }
    }

    private string? GetTxtValue(IDnsQueryResponse response)
    {
        var txtRecords = response.Answers.TxtRecords();
        var first = txtRecords.FirstOrDefault();
        if (first == null) return null;
        // TXT records can have multiple strings that need to be concatenated
        return string.Join("", first.Text);
    }
}
