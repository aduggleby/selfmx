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

        var response = await _http.PostAsJsonAsync(
            $"zones/{_settings.ZoneId}/dns_records",
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

    public async Task DeleteDnsRecordAsync(string recordId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting DNS record: {RecordId}", recordId);

        var response = await _http.DeleteAsync(
            $"zones/{_settings.ZoneId}/dns_records/{recordId}",
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
        CancellationToken ct = default)
    {
        var query = $"zones/{_settings.ZoneId}/dns_records?per_page=100";
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
        var records = await ListDnsRecordsAsync(ct: ct);
        var domainRecords = records.Where(r =>
            r.Name.EndsWith($"._domainkey.{domainName}") ||
            r.Name == domainName ||
            r.Name.EndsWith($".{domainName}"));

        foreach (var record in domainRecords)
        {
            await DeleteDnsRecordAsync(record.Id, ct);
        }
    }
}

public class CloudflareException : Exception
{
    public CloudflareException(string message) : base(message) { }
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
