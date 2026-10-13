using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SelfMX.Api.Contracts.Responses;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;
using SelfMX.Api.Services;
using Xunit;

namespace SelfMX.Api.Tests;

public class SentEmailFiltersTests : IClassFixture<SelfMxTestFactory>
{
    private readonly SelfMxTestFactory _factory;

    public SentEmailFiltersTests(SelfMxTestFactory factory) => _factory = factory;

    [Fact]
    public async Task TestEmail_IsStoredAsSentEmailAndAttributedToTheSendingKey()
    {
        var (admin, domainId, domainName) = await SetupAsync();
        string adminKeyId;
        using (var keyScope = _factory.Services.CreateScope())
            adminKeyId = (await keyScope.ServiceProvider.GetRequiredService<ApiKeyService>().ValidateAsync(admin, null))!.Id;

        var response = await Client(admin).PostAsJsonAsync($"/domains/{domainId}/test-email",
            new { senderPrefix = "check", to = "owner@example.org", subject = "Test", text = "Hello" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SentEmails
            .SingleAsync(e => e.DomainId == domainId);
        stored.FromAddress.Should().Be($"check@{domainName}");
        JsonSerializer.Deserialize<string[]>(stored.ToAddresses).Should().Equal("owner@example.org");
        stored.TextBody.Should().Be("Hello");
        stored.ApiKeyId.Should().Be(adminKeyId, "a send triggered with an API key is attributed to that key");
    }

    [Theory]
    [InlineData("fromAddress=ALERTS@", new[] { "alert" })]
    [InlineData("toAddress=bob@example", new[] { "alert", "news" })]
    [InlineData("fromAddress=news&toAddress=bob", new[] { "news" })]
    [InlineData("toAddress=nobody", new string[0])]
    public async Task AddressFilters_MatchCaseInsensitiveSubstrings(string query, string[] expectedSubjects)
    {
        var (admin, domainId, domainName) = await SetupAsync();
        await SeedAsync(domainId,
            ($"Alerts <alerts@{domainName}>", new[] { "Bob@Example.com" }, "alert"),
            ($"news@{domainName}", new[] { "carol@example.com", "bob@example.com" }, "news"),
            ($"other@{domainName}", new[] { "dave@example.com" }, "other"));

        var page = await Client(admin).GetFromJsonAsync<CursorPagedResponse<SentEmailListItem>>(
            $"/sent-emails?domainId={domainId}&{query}");

        page!.Data.Select(e => e.Subject).Should().BeEquivalentTo(expectedSubjects);
    }

    private HttpClient Client(string key)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    private async Task<(string AdminKey, string DomainId, string DomainName)> SetupAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var domain = new Domain { Id = Guid.NewGuid().ToString(), Name = $"d-{Guid.NewGuid():N}.example.com", Status = DomainStatus.Verified };
        db.Domains.Add(domain);
        await db.SaveChangesAsync();
        var admin = await scope.ServiceProvider.GetRequiredService<ApiKeyService>().CreateAsync("admin", [], isAdmin: true);
        return (admin.PlainTextKey, domain.Id, domain.Name);
    }

    private async Task SeedAsync(string domainId, params (string From, string[] To, string Subject)[] emails)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SentEmails.AddRange(emails.Select(e => new SentEmail
        {
            Id = Guid.NewGuid().ToString(),
            MessageId = Guid.NewGuid().ToString(),
            FromAddress = e.From,
            ToAddresses = JsonSerializer.Serialize(e.To),
            Subject = e.Subject,
            DomainId = domainId
        }));
        await db.SaveChangesAsync();
    }
}
