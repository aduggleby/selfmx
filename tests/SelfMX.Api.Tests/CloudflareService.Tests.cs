using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SelfMX.Api.Services;
using SelfMX.Api.Settings;
using Xunit;

namespace SelfMX.Api.Tests;

public class CloudflareServiceTests
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly CloudflareService _service;
    private readonly CloudflareSettings _settings;

    public CloudflareServiceTests()
    {
        _settings = new CloudflareSettings
        {
            ApiToken = "test-api-token",
            ZoneId = "test-zone-id"
        };

        _mockHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(_mockHandler);
        var mockLogger = Substitute.For<ILogger<CloudflareService>>();

        _service = new CloudflareService(
            httpClient,
            Options.Create(_settings),
            mockLogger);
    }

    [Fact]
    public async Task CreateDnsRecordAsync_CreatesRecordAndReturnsId()
    {
        // Arrange
        var recordId = "record-123";
        _mockHandler.SetResponse(new
        {
            success = true,
            result = new { id = recordId, type = "CNAME", name = "test", content = "value" }
        });

        // Act
        var result = await _service.CreateDnsRecordAsync("CNAME", "test.example.com", "target.example.com");

        // Assert
        result.Should().Be(recordId);
        _mockHandler.LastRequestUri.Should().Contain($"zones/{_settings.ZoneId}/dns_records");
    }

    [Fact]
    public async Task CreateDnsRecordAsync_ThrowsOnFailure()
    {
        // Arrange
        _mockHandler.SetResponse(new
        {
            success = false,
            errors = new[] { new { code = 1001, message = "Invalid zone" } }
        }, HttpStatusCode.BadRequest);

        // Act & Assert
        var act = async () => await _service.CreateDnsRecordAsync("CNAME", "test", "value");
        await act.Should().ThrowAsync<CloudflareException>()
            .WithMessage("*Invalid zone*");
    }

    [Fact]
    public async Task DeleteDnsRecordAsync_DeletesRecord()
    {
        // Arrange
        var recordId = "record-123";
        _mockHandler.SetResponse(new { success = true });

        // Act
        await _service.DeleteDnsRecordAsync(recordId);

        // Assert
        _mockHandler.LastRequestUri.Should().Contain($"dns_records/{recordId}");
        _mockHandler.LastRequestMethod.Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public async Task ListDnsRecordsAsync_ReturnsRecords()
    {
        // Arrange
        _mockHandler.SetResponse(new
        {
            success = true,
            result = new[]
            {
                new { id = "1", type = "CNAME", name = "test1", content = "value1", priority = 0, proxied = false },
                new { id = "2", type = "CNAME", name = "test2", content = "value2", priority = 0, proxied = false }
            }
        });

        // Act
        var result = await _service.ListDnsRecordsAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("1");
        result[1].Name.Should().Be("test2");
    }

    [Fact]
    public async Task ListDnsRecordsAsync_FiltersbyNameAndType()
    {
        // Arrange
        _mockHandler.SetResponse(new { success = true, result = Array.Empty<object>() });

        // Act
        await _service.ListDnsRecordsAsync(name: "test.example.com", type: "CNAME");

        // Assert
        _mockHandler.LastRequestUri.Should().Contain("name=test.example.com");
        _mockHandler.LastRequestUri.Should().Contain("type=CNAME");
    }

    [Fact]
    public async Task DeleteDnsRecordsForDomainAsync_DeletesMatchingRecords()
    {
        // Arrange
        var domainName = "example.com";

        // First call: list records
        _mockHandler.SetResponse(new
        {
            success = true,
            result = new[]
            {
                new { id = "1", type = "CNAME", name = "token1._domainkey.example.com", content = "v", priority = 0, proxied = false },
                new { id = "2", type = "CNAME", name = "token2._domainkey.example.com", content = "v", priority = 0, proxied = false },
                new { id = "3", type = "A", name = "other.com", content = "v", priority = 0, proxied = false }
            }
        });

        // Act
        await _service.DeleteDnsRecordsForDomainAsync(domainName);

        // Assert
        _mockHandler.RequestCount.Should().BeGreaterOrEqualTo(1); // At least list was called
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(object Response, HttpStatusCode StatusCode)> _responses = new();
    public string? LastRequestUri { get; private set; }
    public HttpMethod? LastRequestMethod { get; private set; }
    public int RequestCount { get; private set; }

    public void SetResponse(object response, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responses.Clear();
        _responses.Enqueue((response, statusCode));
    }

    public void SetResponseSequence(object[] responses)
    {
        _responses.Clear();
        foreach (var response in responses)
        {
            _responses.Enqueue((response, HttpStatusCode.OK));
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri?.ToString();
        LastRequestMethod = request.Method;
        RequestCount++;

        var (response, statusCode) = _responses.Count > 0
            ? _responses.Dequeue()
            : (new { success = true }, HttpStatusCode.OK);

        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(response),
                System.Text.Encoding.UTF8,
                "application/json")
        });
    }
}
