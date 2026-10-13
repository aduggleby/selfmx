using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SelfMX.Api.Contracts;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;
using SelfMX.Api.Services;
using Xunit;

namespace SelfMX.Api.Tests;

/// <summary>Test host whose admin password is "test-admin", so imports can sign in without an API key.</summary>
public class AdminLoginTestFactory : SelfMxTestFactory
{
    public const string AdminPassword = "test-admin";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:AdminPasswordHash"] = "$6$selfmxtest$MQf56q7VpjjXMp.8J/BOpvFA16FvUXbCSbup.wx9WyHTThnU4a0.pG6jlED.eIrxAG7xrQVuzXO3OTdk5XFIq."
        }));
    }
}

public class DataTransferTests : IAsyncLifetime
{
    private readonly AdminLoginTestFactory _source = new();
    private readonly AdminLoginTestFactory _target = new();

    public async Task InitializeAsync()
    {
        await CreateSchemaAsync(_source);
        await CreateSchemaAsync(_target);
    }

    public async Task DisposeAsync()
    {
        await _source.DisposeAsync();
        await _target.DisposeAsync();
    }

    [Fact]
    public async Task ExportThenImport_MovesConfigurationAndKeepsApiKeysWorking()
    {
        var seeded = await SeedSourceAsync();
        var export = await ExportAsync(_source, seeded.AdminKey);

        var target = await AdminSessionAsync(_target);
        var response = await target.PostAsync("/admin/import", Gzip(export));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ImportResult>();
        result.Should().BeEquivalentTo(new ImportResult(
            Domains: 2, ApiKeys: 2, ApiKeyDomains: 1, RevokedApiKeys: 1, AuditLogs: 2, AuditLogsSkipped: 0,
            PendingDomainsRequeued: 1));

        // The sender's existing key authenticates against the new instance with the same scope
        var sender = _target.CreateClient();
        sender.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seeded.SenderKey);
        var me = await sender.GetFromJsonAsync<JsonElement>("/tokens/me");
        me.GetProperty("allowedDomainIds").EnumerateArray().Select(e => e.GetString())
            .Should().Equal(seeded.VerifiedDomainId);

        using var scope = _target.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domain = await db.Domains.SingleAsync(d => d.Id == seeded.VerifiedDomainId);
        domain.Status.Should().Be(DomainStatus.Verified);
        domain.DnsRecordsJson.Should().Be("[{\"type\":\"CNAME\"}]");
        domain.VerifiedAt.Should().Be(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        (await db.RevokedApiKeys.SingleAsync()).AllowedDomainIds.Should().Be(seeded.VerifiedDomainId);
        var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        (await auditDb.AuditLogs.Select(l => l.Id).ToListAsync()).Should().BeEquivalentTo(["audit-1", "audit-2"]);
    }

    [Fact]
    public async Task Import_IntoInstanceWithConfiguration_IsRejectedWithConflict()
    {
        var seeded = await SeedSourceAsync();
        var export = await ExportAsync(_source, seeded.AdminKey);
        var target = await AdminSessionAsync(_target);
        (await target.PostAsync("/admin/import", Gzip(export))).StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await target.PostAsync("/admin/import", Gzip(export));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString()
            .Should().Contain("without domains or API keys");
    }

    [Fact]
    public async Task Import_PlainJsonFile_IsAccepted()
    {
        var seeded = await SeedSourceAsync();
        var export = await ExportAsync(_source, seeded.AdminKey);
        var target = await AdminSessionAsync(_target);

        var response = await target.PostAsync("/admin/import",
            new ByteArrayContent(export) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("""{"formatVersion":99}""", "Unsupported export format version 99")]
    [InlineData("""{"formatVersion":1,"domains":[{"id":"d1"}]}""", "missing required domain")]
    [InlineData("not json at all", "not a valid SelfMX export")]
    public async Task Import_InvalidFile_IsRejectedWithBadRequest(string body, string expectedMessage)
    {
        var target = await AdminSessionAsync(_target);

        var response = await target.PostAsync("/admin/import", Gzip(Encoding.UTF8.GetBytes(body)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString()
            .Should().Contain(expectedMessage);
    }

    [Fact]
    public async Task Import_WithDanglingDomainLink_IsRejectedAndNothingIsImported()
    {
        var target = await AdminSessionAsync(_target);
        var body = """
            {"formatVersion":1,"sourceVersion":"x","sourceDatabase":"x","exportedAt":"2026-01-01T00:00:00Z",
             "domains":[{"id":"d1","name":"a.example","status":"Verified","createdAt":"2026-01-01T00:00:00Z"}],
             "apiKeys":[],"apiKeyDomains":[{"apiKeyId":"missing","domainId":"d1"}],"revokedApiKeys":[],"auditLogs":[]}
            """;

        var response = await target.PostAsync("/admin/import", Gzip(Encoding.UTF8.GetBytes(body)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = _target.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Domains.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Export_RequiresAdmin()
    {
        var seeded = await SeedSourceAsync();

        var anonymous = await _source.CreateClient().GetAsync("/admin/export");
        var sender = _source.CreateClient();
        sender.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", seeded.SenderKey);
        var nonAdmin = await sender.GetAsync("/admin/export");

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        nonAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private record Seeded(string AdminKey, string SenderKey, string VerifiedDomainId);

    private async Task<Seeded> SeedSourceAsync()
    {
        using var scope = _source.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verified = new Domain
        {
            Id = "domain-verified", Name = "mail.example.com", Status = DomainStatus.Verified,
            VerifiedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), DnsRecordsJson = "[{\"type\":\"CNAME\"}]"
        };
        db.Domains.AddRange(verified, new Domain { Id = "domain-pending", Name = "pending.example.com" });
        db.RevokedApiKeys.Add(new RevokedApiKey
        {
            Id = "revoked-1", Name = "old", KeyPrefix = "re_oldoldol", CreatedAt = DateTime.UtcNow.AddDays(-200),
            RevokedAt = DateTime.UtcNow.AddDays(-100), AllowedDomainIds = verified.Id
        });
        await db.SaveChangesAsync();

        var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        auditDb.AuditLogs.AddRange(
            new AuditLog { Id = "audit-1", Action = "domain.create", ActorType = "admin", ResourceType = "domain" },
            new AuditLog { Id = "audit-2", Action = "email.send", ActorType = "api_key", ResourceType = "email" });
        await auditDb.SaveChangesAsync();

        var keys = scope.ServiceProvider.GetRequiredService<ApiKeyService>();
        var admin = await keys.CreateAsync("admin", null, isAdmin: true);
        var sender = await keys.CreateAsync("sender", [verified.Id], isAdmin: false);
        return new Seeded(admin.PlainTextKey, sender.PlainTextKey, verified.Id);
    }

    private static async Task<byte[]> ExportAsync(AdminLoginTestFactory factory, string adminKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminKey);
        var response = await client.GetAsync("/admin/export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/gzip");
        response.Content.Headers.ContentDisposition!.FileName.Should().StartWith("selfmx-export-");

        await using var gzip = new GZipStream(await response.Content.ReadAsStreamAsync(), CompressionMode.Decompress);
        using var json = new MemoryStream();
        await gzip.CopyToAsync(json);
        return json.ToArray();
    }

    private static async Task<HttpClient> AdminSessionAsync(AdminLoginTestFactory factory)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/admin/login", new { password = AdminLoginTestFactory.AdminPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    private static ByteArrayContent Gzip(byte[] content)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(content);
        return new ByteArrayContent(buffer.ToArray()) { Headers = { ContentType = new MediaTypeHeaderValue("application/gzip") } };
    }

    private static async Task CreateSchemaAsync(SelfMxTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        // Shared SQLite database: EnsureCreated skips the second context, so create its tables directly
        await scope.ServiceProvider.GetRequiredService<AuditDbContext>()
            .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }
}
