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
        var result = await _service.CreateDnsRecordAsync("test-zone-id", "CNAME", "test.example.com", "target.example.com");

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
        var act = async () => await _service.CreateDnsRecordAsync("test-zone-id", "CNAME", "test", "value");
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

    private (CloudflareService Service, MockHttpMessageHandler Handler) CreateLookupService()
    {
        var handler = new MockHttpMessageHandler();
        var service = new CloudflareService(
            new HttpClient(handler),
            Options.Create(new CloudflareSettings { ApiToken = "test-api-token", ZoneId = "" }),
            Substitute.For<ILogger<CloudflareService>>());
        return (service, handler);
    }

    private static object ZoneList(params (string Id, string Name)[] zones) => new
    {
        success = true,
        result = zones.Select(z => new { id = z.Id, name = z.Name }).ToArray()
    };

    [Fact]
    public async Task ResolveZoneIdAsync_UsesConfiguredZoneWithoutLookup()
    {
        var result = await _service.ResolveZoneIdAsync("anything.example.com");

        result.Should().Be("test-zone-id");
        _mockHandler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ResolveZoneIdAsync_FindsExactZone()
    {
        var (service, handler) = CreateLookupService();
        handler.SetResponseSequence([ZoneList(("zone-1", "example.com"))]);

        var result = await service.ResolveZoneIdAsync("example.com");

        result.Should().Be("zone-1");
        handler.LastRequestUri.Should().Contain("zones?name=example.com");
    }

    [Fact]
    public async Task ResolveZoneIdAsync_WalksUpToParentZoneForSubdomain()
    {
        var (service, handler) = CreateLookupService();
        handler.SetResponseSequence([
            ZoneList(),
            ZoneList(("zone-parent", "laurenduggleby.com"))
        ]);

        var result = await service.ResolveZoneIdAsync("family.laurenduggleby.com");

        result.Should().Be("zone-parent");
        handler.RequestCount.Should().Be(2);
        handler.LastRequestUri.Should().Contain("name=laurenduggleby.com");
    }

    [Fact]
    public async Task ResolveZoneIdAsync_ThrowsWhenNoZoneMatches()
    {
        var (service, handler) = CreateLookupService();
        handler.SetResponseSequence([ZoneList(), ZoneList()]);

        var act = async () => await service.ResolveZoneIdAsync("family.laurenduggleby.com");

        await act.Should().ThrowAsync<CloudflareZoneNotFoundException>()
            .WithMessage("*No Cloudflare zone found for family.laurenduggleby.com in this account*");
        handler.RequestCount.Should().Be(2); // never queries the bare TLD
    }

    [Fact]
    public async Task ResolveZoneIdAsync_PrefersLongerZoneWhenParentAlsoExists()
    {
        var (service, handler) = CreateLookupService();
        handler.SetResponseSequence([ZoneList(("zone-sub", "family.example.com"))]);

        var result = await service.ResolveZoneIdAsync("family.example.com");

        result.Should().Be("zone-sub");
        handler.RequestCount.Should().Be(1); // parent never queried
    }

    [Fact]
    public async Task ResolveZoneIdAsync_OnlyRequestsActiveZones()
    {
        var (service, handler) = CreateLookupService();
        handler.SetResponseSequence([ZoneList(("zone-1", "example.com"))]);

        await service.ResolveZoneIdAsync("example.com");

        handler.LastRequestUri.Should().Contain("status=active");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.OK)]
    public async Task ResolveZoneIdAsync_WrapsLookupErrorsAsCloudflareException(HttpStatusCode status)
    {
        var (service, handler) = CreateLookupService();
        handler.SetResponse(new
        {
            success = false,
            errors = new[] { new { code = 9109, message = "Invalid access token" } }
        }, status);

        var act = async () => await service.ResolveZoneIdAsync("example.com");

        var ex = await act.Should().ThrowAsync<CloudflareException>().WithMessage("*Invalid access token*");
        ex.Which.Should().NotBeOfType<CloudflareZoneNotFoundException>();
    }

    [Fact]
    public async Task ResolveZoneIdAsync_WrapsTransportErrorsAsCloudflareException()
    {
        var (service, handler) = CreateLookupService();
        handler.Exception = new HttpRequestException("connection refused");

        var act = async () => await service.ResolveZoneIdAsync("example.com");

        await act.Should().ThrowAsync<CloudflareException>().WithMessage("*connection refused*");
    }

    [Fact]
    public async Task ResolveZoneIdAsync_WrapsMalformedJsonAsCloudflareException()
    {
        var (service, handler) = CreateLookupService();
        handler.SetResponse(new { success = true, result = "not-a-list" });

        var act = async () => await service.ResolveZoneIdAsync("example.com");

        await act.Should().ThrowAsync<CloudflareException>();
    }

    [Fact]
    public async Task DeleteDnsRecordsForDomainAsync_DeletesOnlyRecordsSelfMxCreated()
    {
        var handler = new MockHttpMessageHandler();
        var service = new CloudflareService(
            new HttpClient(handler),
            Options.Create(new CloudflareSettings { ApiToken = "t", ZoneId = "zone-1" }),
            Substitute.For<ILogger<CloudflareService>>());

        var created = new[]
        {
            new DnsRecordInfo("CNAME", "tok1._domainkey.family.example.com", "tok1.dkim.amazonses.com")
        };

        // Listing by name returns SelfMX's record plus a same-name record of another type/content.
        handler.SetResponseSequence([
            new
            {
                success = true,
                result = new[]
                {
                    new { id = "ours", type = "CNAME", name = "tok1._domainkey.family.example.com", content = "tok1.dkim.amazonses.com.", priority = 0, proxied = false },
                    new { id = "other-content", type = "CNAME", name = "tok1._domainkey.family.example.com", content = "elsewhere.example.net", priority = 0, proxied = false },
                    new { id = "website", type = "A", name = "family.example.com", content = "1.2.3.4", priority = 0, proxied = true }
                }
            },
            new { success = true }
        ]);

        await service.DeleteDnsRecordsForDomainAsync("family.example.com", created);

        handler.RequestCount.Should().Be(2); // one list, one delete
        handler.RequestUris.Last().Should().EndWith("zones/zone-1/dns_records/ours");
        handler.RequestMethods.Last().Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public async Task DeleteDnsRecordsForDomainAsync_WithNoStoredRecords_DoesNothing()
    {
        await _service.DeleteDnsRecordsForDomainAsync("family.example.com", []);

        _mockHandler.RequestCount.Should().Be(0);
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(object Response, HttpStatusCode StatusCode)> _responses = new();
    public string? LastRequestUri { get; private set; }
    public HttpMethod? LastRequestMethod { get; private set; }
    public int RequestCount { get; private set; }
    public List<string> RequestUris { get; } = [];
    public List<HttpMethod> RequestMethods { get; } = [];
    public Exception? Exception { get; set; }

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
        RequestUris.Add(LastRequestUri ?? "");
        RequestMethods.Add(request.Method);

        if (Exception is not null) throw Exception;
        if (_responses.Count == 0)
            throw new InvalidOperationException($"Unexpected request (no queued response): {request.Method} {LastRequestUri}");

        var (response, statusCode) = _responses.Dequeue();

        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(response),
                System.Text.Encoding.UTF8,
                "application/json")
        });
    }
}
