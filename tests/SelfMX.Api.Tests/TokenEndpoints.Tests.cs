using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;
using SelfMX.Api.Services;
using Xunit;

namespace SelfMX.Api.Tests;

public class TokenEndpointsTests : IClassFixture<SelfMxTestFactory>
{
    private readonly SelfMxTestFactory _factory;

    public TokenEndpointsTests(SelfMxTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TokensMe_WithNonAdminApiKey_ReturnsAllowedDomains()
    {
        var (apiKey, domainId) = await CreateNonAdminKeyAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await client.GetAsync("/tokens/me");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TokenInfoResponseDto>();
        body.Should().NotBeNull();
        body!.Authenticated.Should().BeTrue();
        body.ActorType.Should().Be("api_key");
        body.IsAdmin.Should().BeFalse();
        body.Name.Should().Be("test");
        body.KeyPrefix.Should().NotBeNull();
        body.AllowedDomainIds.Should().Contain(domainId);
    }

    [Fact]
    public async Task TokensMe_WithAdminApiKey_ReturnsAdmin()
    {
        var apiKey = await CreateAdminKeyAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await client.GetAsync("/tokens/me");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TokenInfoResponseDto>();
        body.Should().NotBeNull();
        body!.ActorType.Should().Be("admin");
        body.IsAdmin.Should().BeTrue();
    }

    private async Task<(string ApiKey, string DomainId)> CreateNonAdminKeyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var domain = new Domain
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"test-{Guid.NewGuid():N}.example.com",
            Status = DomainStatus.Verified
        };
        db.Domains.Add(domain);
        await db.SaveChangesAsync();

        var apiKeyService = scope.ServiceProvider.GetRequiredService<ApiKeyService>();
        var created = await apiKeyService.CreateAsync("test", new[] { domain.Id }, isAdmin: false);
        return (created.PlainTextKey, domain.Id);
    }

    private async Task<string> CreateAdminKeyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var apiKeyService = scope.ServiceProvider.GetRequiredService<ApiKeyService>();
        var created = await apiKeyService.CreateAsync("admin-test", Array.Empty<string>(), isAdmin: true);
        return created.PlainTextKey;
    }

    private sealed record TokenInfoResponseDto(
        bool Authenticated,
        string ActorType,
        bool IsAdmin,
        string? Name,
        string? KeyId,
        string? KeyPrefix,
        string[] AllowedDomainIds
    );
}
