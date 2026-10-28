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
            var zone = await FindZoneAsync(candidate, name, ct);
            if (zone is not null)
                return zone.Id;
        }

        throw new CloudflareZoneNotFoundException(name);
    }

    private async Task<ZoneResult?> FindZoneAsync(string candidate, string requestedName, CancellationToken ct)
    {
        var zones = await SendAsync<List<ZoneResult>>(
            () => _http.GetAsync($"zones?name={Uri.EscapeDataString(candidate)}&status=active&per_page=1", ct),
            $"Failed to look up Cloudflare zone for {requestedName}", ct);

        return zones?.FirstOrDefault(z =>
            string.Equals(z.Name, candidate, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(z.Id));
    }

    /// <summary>
    /// Sends a request, parses the Cloudflare envelope and returns its result. Non-success responses,
    /// transport errors, timeouts and non-JSON bodies are all raised as <see cref="CloudflareException"/>.
    /// </summary>
    private async Task<T?> SendAsync<T>(Func<Task<HttpResponseMessage>> send, string failureMessage, CancellationToken ct)
    {
        string detail;
        try
        {
            using var response = await send();
            CloudflareResponse<T>? envelope = null;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<CloudflareResponse<T>>(JsonOptions, ct);
            }
            catch (JsonException) when (!response.IsSuccessStatusCode)
            {
                // Non-JSON error body: fall through to the status code message.
            }

            if (response.IsSuccessStatusCode && envelope?.Success == true)
                return envelope.Result;

            detail = envelope?.Errors is { Count: > 0 } errors
                ? string.Join(", ", errors.Select(e => e.Message))
                : $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException or NotSupportedException ||
            (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            detail = ex is TaskCanceledException ? "request timed out" : ex.Message;
        }

        _logger.LogError("{Failure}: {Detail}", failureMessage, detail);
        throw new CloudflareException($"{failureMessage}: {detail}");
    }

    public async Task<string> CreateDnsRecordAsync(
        string zoneId,
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

        var result = await SendAsync<DnsRecordResult>(
            () => _http.PostAsJsonAsync($"zones/{zoneId}/dns_records", request, JsonOptions, ct),
            "Failed to create DNS record", ct);

        _logger.LogInformation("DNS record created with ID: {RecordId}", result?.Id);
        return result?.Id ?? throw new CloudflareException("Failed to create DNS record: response had no record");
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

        return await SendAsync<List<DnsRecordResult>>(
            () => _http.GetAsync(query, ct), "Failed to list DNS records", ct) ?? [];
    }

    public async Task DeleteDnsRecordsForDomainAsync(
        string domainName,
        IReadOnlyCollection<DnsRecordInfo> records,
        CancellationToken ct = default)
    {
        if (records.Count == 0) return;

        _logger.LogInformation("Deleting SelfMX-created DNS records for domain: {Domain}", domainName);

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

        // Only delete records matching what SelfMX created (type, name, content); never anything else under the domain.
        foreach (var expected in records)
        {
            var existing = await ListDnsRecordsAsync(zoneId, expected.Name, expected.Type, ct);
            foreach (var record in existing.Where(r =>
                         string.Equals(r.Name, expected.Name, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(r.Type, expected.Type, StringComparison.OrdinalIgnoreCase) &&
                         NormalizeContent(r.Content) == NormalizeContent(expected.Value)))
            {
                await DeleteDnsRecordAsync(zoneId, record.Id, ct);
            }
        }
    }

    private static string NormalizeContent(string content) =>
        content.Trim().Trim('"').TrimEnd('.').ToLowerInvariant();
}

public class CloudflareException : Exception
{
    public CloudflareException(string message) : base(message) { }
}

public class CloudflareZoneNotFoundException : CloudflareException
{
    public CloudflareZoneNotFoundException(string name)
        : base($"No Cloudflare zone found for {name} in this account. " +
               "Add the zone to the Cloudflare account of the configured API token, ensure the token has Zone:Read, or create the DNS records manually.") { }
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
