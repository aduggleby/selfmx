using System.Net.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Resend;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;
using SelfMX.Api.Services;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Xunit;

namespace SelfMX.Api.Tests;

public class ResendCompatibilityTests : IClassFixture<SelfMxTestFactory>
{
    private readonly SelfMxTestFactory _factory;

    public ResendCompatibilityTests(SelfMxTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EmailSend_AndRetrieve_AreResendCompatible()
    {
        var (client, domainName) = await CreateResendClientAsync();

        var message = new EmailMessage
        {
            From = $"Ando CI <sender@{domainName}>",
            Subject = "Test subject",
            HtmlBody = "<p>Hello</p>"
        };
        message.To.Add("recipient@example.com");

        var sendResponse = await client.EmailSendAsync(message);
        sendResponse.Success.Should().BeTrue();
        var emailId = sendResponse.Content;
        emailId.Should().NotBe(Guid.Empty);

        var retrieveResponse = await client.EmailRetrieveAsync(emailId);
        retrieveResponse.Success.Should().BeTrue();

        var receipt = retrieveResponse.Content;
        receipt.Id.Should().Be(emailId);
        receipt.From.Email.Should().Be($"sender@{domainName}");
        receipt.From.DisplayName.Should().Be("Ando CI");
        receipt.Subject.Should().Be("Test subject");
        receipt.HtmlBody.Should().Be("<p>Hello</p>");
        receipt.TextBody.Should().BeNull();
    }

    [Fact]
    public async Task EmailList_ReturnsResendPaginatedResult()
    {
        var (client, domainName) = await CreateResendClientAsync();

        var message = new EmailMessage
        {
            From = $"Sender <sender@{domainName}>",
            Subject = "List test",
            TextBody = "hello"
        };
        message.To.Add("recipient@example.com");

        var sendResponse = await client.EmailSendAsync(message);
        sendResponse.Success.Should().BeTrue();
        var emailId = sendResponse.Content;

        var listResponse = await client.EmailListAsync();
        listResponse.Success.Should().BeTrue();

        listResponse.Content.HasMore.Should().BeFalse();
        listResponse.Content.Data.Should().ContainSingle(r => r.Id == emailId);
    }

    [Fact]
    public async Task EmailBatch_Strict_IsResendCompatible()
    {
        var (client, domainName) = await CreateResendClientAsync();

        var msg1 = new EmailMessage
        {
            From = $"Sender <sender@{domainName}>",
            Subject = "Batch 1",
            TextBody = "Hello 1"
        };
        msg1.To.Add("recipient@example.com");

        var msg2 = new EmailMessage
        {
            From = $"Sender <sender@{domainName}>",
            Subject = "Batch 2",
            HtmlBody = "<p>Hello 2</p>"
        };
        msg2.To.Add("recipient2@example.com");

        var batchResponse = await client.EmailBatchAsync(new[] { msg1, msg2 });
        batchResponse.Success.Should().BeTrue();
        batchResponse.Content.Should().HaveCount(2);
        batchResponse.Content.Should().OnlyContain(id => id != Guid.Empty);
    }

    [Fact]
    public async Task EmailBatch_Permissive_ReturnsErrors()
    {
        var (client, domainName) = await CreateResendClientAsync();

        var valid = new EmailMessage
        {
            From = $"Sender <sender@{domainName}>",
            Subject = "Valid",
            TextBody = "ok"
        };
        valid.To.Add("recipient@example.com");

        var invalid = new EmailMessage
        {
            From = $"Sender <sender@{domainName}>",
            Subject = "",
            TextBody = "missing subject"
        };
        invalid.To.Add("recipient@example.com");

        var response = await client.EmailBatchAsync(new[] { valid, invalid }, EmailBatchValidationMode.Permissive);
        response.Success.Should().BeTrue();

        response.Content.Data.Should().HaveCount(1);
        response.Content.Errors.Should().NotBeNull();
        response.Content.Errors!.Should().ContainSingle(e => e.Index == 1);
    }

    private async Task<(IResend Client, string DomainName)> CreateResendClientAsync()
    {
        var domainName = $"test-{Guid.NewGuid():N}.example.com";

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var domain = new SelfMX.Api.Entities.Domain
        {
            Id = Guid.NewGuid().ToString(),
            Name = domainName,
            Status = DomainStatus.Verified
        };
        db.Domains.Add(domain);
        await db.SaveChangesAsync();

        var apiKeyService = scope.ServiceProvider.GetRequiredService<ApiKeyService>();
        var created = await apiKeyService.CreateAsync("test", new[] { domain.Id }, isAdmin: false);
        var apiKey = created.PlainTextKey;

        var httpClient = _factory.CreateClient();
        var options = new ResendClientOptions
        {
            ApiToken = apiKey,
            ApiUrl = httpClient.BaseAddress!.ToString()
        };
        var resend = ResendClient.Create(options, httpClient);

        return (resend, domainName);
    }
}

public class SelfMxTestFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString = "Data Source=SelfMxTestDb;Mode=Memory;Cache=Shared";
    private SqliteConnection? _keepAlive;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Test:SqliteConnectionString"] = TestConnectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            _keepAlive = new SqliteConnection(TestConnectionString);
            _keepAlive.Open();

            var auditHostedServices = services
                .Where(d =>
                    d.ServiceType == typeof(IHostedService) &&
                    (d.ImplementationType == typeof(AuditService) ||
                     d.ImplementationFactory?.Method.ReturnType == typeof(AuditService)))
                .ToList();

            foreach (var descriptor in auditHostedServices)
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<ISesService>();
            services.AddSingleton<ISesService, FakeSesService>();
            services.RemoveAll<IBackgroundJobClient>();
            services.AddSingleton<IBackgroundJobClient, NoopBackgroundJobClient>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keepAlive?.Dispose();
        }

        base.Dispose(disposing);
    }
}

public class FakeSesService : ISesService
{
    private int _counter;

    public Task<(string IdentityArn, DnsRecordInfo[] DnsRecords)> CreateDomainIdentityAsync(string domainName, CancellationToken ct = default)
        => Task.FromResult(("", Array.Empty<DnsRecordInfo>()));

    public Task<bool> CheckDkimVerificationAsync(string domainName, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<DkimVerificationResult> GetDkimVerificationDetailsAsync(string domainName, CancellationToken ct = default)
        => Task.FromResult(new DkimVerificationResult(true, "SUCCESS", "TEST", "2048", null, null));

    public Task DeleteDomainIdentityAsync(string domainName, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<string> SendEmailAsync(
        string from,
        string[] to,
        string subject,
        string? html,
        string? text,
        string[]? cc = null,
        string[]? bcc = null,
        string[]? replyTo = null,
        CancellationToken ct = default)
    {
        var id = $"test-message-{Interlocked.Increment(ref _counter)}";
        return Task.FromResult(id);
    }
}

public class NoopBackgroundJobClient : IBackgroundJobClient
{
    public string? Create(Job job, IState state)
    {
        return Guid.NewGuid().ToString("N");
    }

    public bool ChangeState(string jobId, IState state, string? expectedState)
    {
        return true;
    }
}
