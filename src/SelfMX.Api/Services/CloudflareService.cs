using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SelfMX.Api.Settings;

namespace SelfMX.Api.Services;

public class CloudflareService : ICloudflareService
{
    private readonly HttpClient _http;
    private readonly CloudflareSettings _settings;
    private readonly ILogger<CloudflareService> _logger;

    private readonly Dictionary<string, string> _zoneCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CloudflareService(
        HttpClient httpClient,
        IOptions<CloudflareSettings> settings,
        ILogger<CloudflareService> logger)
    {
        _http = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _http.BaseAddress = new Uri("https://api.cloudflare.com/client/v4/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiToken);
    }

    /// <summary>
    /// Resolves the Cloudflare zone ID for a hostname. An explicitly configured ZoneId wins;
    /// otherwise walks up the labels (a.b.example.com, b.example.com, example.com) and returns
    /// the longest name that matches a zone in the account.
    /// </summary>
    public async Task<string> ResolveZoneIdAsync(string name, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_settings.ZoneId))
            return _settings.ZoneId;

        var labels = name.Trim().TrimEnd('.').ToLowerInvariant().Split('.', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i <= labels.Length - 2; i++)
        {
            var candidate = string.Join('.', labels[i..]);
            if (_zoneCache.TryGetValue(candidate, out var cached))
                return cached;

            var response = await _http.GetAsync(
                $"zones?name={Uri.EscapeDataString(candidate)}&per_page=1", ct);
            var result = await response.Content.ReadFromJsonAsync<CloudflareResponse<List<ZoneResult>>>(JsonOptions, ct);

            if (!response.IsSuccessStatusCode || result?.Success != true)
            {
                var errors = result?.Errors?.Select(e => e.Message) ?? ["Unknown error"];
                var errorMessage = string.Join(", ", errors);
                _logger.LogError("Failed to look up Cloudflare zone {Zone}: {Errors}", candidate, errorMessage);
                throw new CloudflareException($"Failed to look up Cloudflare zone for {name}: {errorMessage}");
            }

            var zone = result.Result?.FirstOrDefault(z =>
                string.Equals(z.Name, candidate, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(z.Id));
            if (zone is not null)
            {
                _zoneCache[candidate] = zone.Id;
                return zone.Id;
            }
        }

        throw new CloudflareZoneNotFoundException(name);
    }

    public async Task<string> CreateDnsRecordAsync(
        string type,
        string name,
        string content,
        int priority = 0,
        bool proxied = false,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Creating DNS record: {Type} {Name} -> {Content}",
            type, name, content);

        var request = new
        {
            type,
            name,
            content,
            priority = type == "MX" ? priority : (int?)null,
            proxied,
            ttl = 1 // Auto TTL
        };

        var zoneId = await ResolveZoneIdAsync(name, ct);

        var response = await _http.PostAsJsonAsync(
            $"zones/{zoneId}/dns_records",
            request,
            JsonOptions,
            ct);

        var result = await response.Content.ReadFromJsonAsync<CloudflareResponse<DnsRecordResult>>(JsonOptions, ct);

        if (!response.IsSuccessStatusCode || result?.Success != true)
        {
            var errors = result?.Errors?.Select(e => e.Message) ?? ["Unknown error"];
            var errorMessage = string.Join(", ", errors);
            _logger.LogError("Failed to create DNS record: {Errors}", errorMessage);
            throw new CloudflareException($"Failed to create DNS record: {errorMessage}");
        }

        _logger.LogInformation("DNS record created with ID: {RecordId}", result.Result?.Id);
        return result.Result!.Id;
    }

    private string RequireConfiguredZoneId() =>
        !string.IsNullOrWhiteSpace(_settings.ZoneId)
            ? _settings.ZoneId
            : throw new CloudflareException("Cloudflare:ZoneId is not configured; cannot target a zone without a record name.");

    public Task DeleteDnsRecordAsync(string recordId, CancellationToken ct = default) =>
        DeleteDnsRecordAsync(RequireConfiguredZoneId(), recordId, ct);

    private async Task DeleteDnsRecordAsync(string zoneId, string recordId, CancellationToken ct)
    {
        _logger.LogInformation("Deleting DNS record: {RecordId}", recordId);

        var response = await _http.DeleteAsync(
            $"zones/{zoneId}/dns_records/{recordId}",
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Failed to delete DNS record {RecordId}: {StatusCode} {Content}",
                recordId, response.StatusCode, content);
        }
    }

    public async Task<List<DnsRecordResult>> ListDnsRecordsAsync(
        string? name = null,
        string? type = null,
        CancellationToken ct = default) =>
        await ListDnsRecordsAsync(
            string.IsNullOrEmpty(name) ? RequireConfiguredZoneId() : await ResolveZoneIdAsync(name, ct),
            name, type, ct);

    private async Task<List<DnsRecordResult>> ListDnsRecordsAsync(
        string zoneId,
        string? name,
        string? type,
        CancellationToken ct)
    {
        var query = $"zones/{zoneId}/dns_records?per_page=100";
        if (!string.IsNullOrEmpty(name)) query += $"&name={Uri.EscapeDataString(name)}";
        if (!string.IsNullOrEmpty(type)) query += $"&type={type}";

        var response = await _http.GetAsync(query, ct);
        var result = await response.Content.ReadFromJsonAsync<CloudflareResponse<List<DnsRecordResult>>>(JsonOptions, ct);

        return result?.Result ?? [];
    }

    public async Task DeleteDnsRecordsForDomainAsync(string domainName, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting all DNS records for domain: {Domain}", domainName);

        // Find DKIM records for this domain
        string zoneId;
        try
        {
            zoneId = await ResolveZoneIdAsync(domainName, ct);
        }
        catch (CloudflareZoneNotFoundException ex)
        {
            _logger.LogWarning("{Message} Nothing to delete in Cloudflare.", ex.Message);
            return;
        }

        var records = await ListDnsRecordsAsync(zoneId, null, null, ct);
        var domainRecords = records.Where(r =>
            r.Name.EndsWith($"._domainkey.{domainName}") ||
            r.Name == domainName ||
            r.Name.EndsWith($".{domainName}"));

        foreach (var record in domainRecords)
        {
            await DeleteDnsRecordAsync(zoneId, record.Id, ct);
        }
    }
}

public class CloudflareException : Exception
{
    public CloudflareException(string message) : base(message) { }
}

public class CloudflareZoneNotFoundException : CloudflareException
{
    public CloudflareZoneNotFoundException(string name)
        : base($"No Cloudflare zone found for {name} in this account. " +
               "Add the zone to the Cloudflare account of the configured API token, or create the DNS records manually.") { }
}

public class ZoneResult
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class CloudflareResponse<T>
{
    public bool Success { get; set; }
    public T? Result { get; set; }
    public List<CloudflareError>? Errors { get; set; }
}

public class CloudflareError
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
}

public class DnsRecordResult
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
    public int Priority { get; set; }
    public bool Proxied { get; set; }
}
