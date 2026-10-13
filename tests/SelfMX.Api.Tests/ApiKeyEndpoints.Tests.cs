using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;
using SelfMX.Api.Services;
using Xunit;

namespace SelfMX.Api.Tests;

public class ApiKeyEndpointsTests : IClassFixture<SelfMxTestFactory>
{
    private readonly SelfMxTestFactory _factory;

    public ApiKeyEndpointsTests(SelfMxTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Rename_TrimsAndPersistsTheName()
    {
        var (admin, _) = await CreateKeysAsync();
        var target = await CreateKeyAsync("before");

        var response = await AdminClient(admin).PatchAsJsonAsync($"/api-keys/{target}", new { name = "  after  " });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString().Should().Be("after");
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().ApiKeys.SingleAsync(k => k.Id == target))
            .Name.Should().Be("after");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Rename_WithoutName_IsRejected(string? name)
    {
        var (admin, _) = await CreateKeysAsync();
        var target = await CreateKeyAsync("unchanged");

        var response = await AdminClient(admin).PatchAsJsonAsync($"/api-keys/{target}", new { name });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rename_TooLongOrUnknown_IsRejected()
    {
        var (admin, _) = await CreateKeysAsync();
        var target = await CreateKeyAsync("unchanged");
        var client = AdminClient(admin);

        (await client.PatchAsJsonAsync($"/api-keys/{target}", new { name = new string('x', 101) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PatchAsJsonAsync("/api-keys/does-not-exist", new { name = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RenameAndStats_RequireAdmin()
    {
        var (_, sender) = await CreateKeysAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sender);

        (await client.PatchAsJsonAsync("/api-keys/any", new { name = "x" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api-keys/stats")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/domains/stats")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Stats_CountsSendsPerKeyInEachWindow()
    {
        var (admin, _) = await CreateKeysAsync();
        var keyId = await CreateKeyAsync("stats");
        var now = DateTime.UtcNow;
        await SeedSentEmailsAsync(keyId,
            now.AddHours(-1),    // 24h, 7d, 30d, 90d
            now.AddHours(-30),   // 7d, 30d, 90d
            now.AddDays(-10),    // 30d, 90d
            now.AddDays(-60),    // 90d
            now.AddDays(-100));  // outside every window

        var stats = await AdminClient(admin).GetFromJsonAsync<ApiKeyStatsResponse[]>("/api-keys/stats");

        stats.Should().ContainSingle(s => s.ApiKeyId == keyId)
            .Which.Should().Be(new ApiKeyStatsResponse(keyId, Last24h: 1, Last7d: 2, Last30d: 3, Last90d: 4));
    }

    private HttpClient AdminClient(string adminKey)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminKey);
        return client;
    }

    private async Task<(string Admin, string Sender)> CreateKeysAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        var keys = scope.ServiceProvider.GetRequiredService<ApiKeyService>();
        var domainId = await CreateDomainAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        var admin = await keys.CreateAsync("admin", [], isAdmin: true);
        var sender = await keys.CreateAsync("sender", [domainId], isAdmin: false);
        return (admin.PlainTextKey, sender.PlainTextKey);
    }

    private async Task<string> CreateKeyAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var created = await scope.ServiceProvider.GetRequiredService<ApiKeyService>().CreateAsync(name, [], isAdmin: true);
        return created.Key.Id;
    }

    private async Task SeedSentEmailsAsync(string apiKeyId, params DateTime[] sentAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domainId = await CreateDomainAsync(db);
        db.SentEmails.AddRange(sentAt.Select(at => new SentEmail
        {
            Id = Guid.NewGuid().ToString(),
            MessageId = Guid.NewGuid().ToString(),
            SentAt = at,
            FromAddress = "a@example.com",
            ToAddresses = "[\"b@example.com\"]",
            Subject = "s",
            DomainId = domainId,
            ApiKeyId = apiKeyId
        }));
        await db.SaveChangesAsync();
    }

    private static async Task<string> CreateDomainAsync(AppDbContext db)
    {
        var domain = new Domain { Id = Guid.NewGuid().ToString(), Name = $"d-{Guid.NewGuid():N}.example.com", Status = DomainStatus.Verified };
        db.Domains.Add(domain);
        await db.SaveChangesAsync();
        return domain.Id;
    }
}
