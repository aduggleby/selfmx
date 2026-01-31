using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;
using SelfMX.Api.Jobs;
using SelfMX.Api.Services;
using SelfMX.Api.Settings;
using Xunit;

namespace SelfMX.Api.Tests;

public class SetupDomainJobTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DomainService _domainService;
    private readonly ISesService _mockSesService;
    private readonly ICloudflareService _mockCloudflareService;
    private readonly ILogger<SetupDomainJob> _mockLogger;
    private readonly SetupDomainJob _job;

    public SetupDomainJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        var appSettings = Options.Create(new AppSettings
        {
            ApiKeyHash = "test",
            AdminPasswordHash = "$6$testsalt$K3lHqxM.xK.B.D3ZwQ9RGvJyN8O.mS.pT.uV.wX.yZ.0A.1B.2C.3D.4E.5F.6G.7H.8I.9J", // SHA-512 crypt hash placeholder for tests
            VerificationTimeout = TimeSpan.FromHours(72)
        });

        _domainService = new DomainService(_db, appSettings);
        _mockSesService = Substitute.For<ISesService>();
        _mockCloudflareService = Substitute.For<ICloudflareService>();
        _mockLogger = Substitute.For<ILogger<SetupDomainJob>>();

        _job = new SetupDomainJob(
            _domainService,
            _mockSesService,
            _mockCloudflareService,
            _mockLogger);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_SkipsWhenDomainNotFound()
    {
        // Act
        await _job.ExecuteAsync("missing-id");

        // Assert
        await _mockSesService.DidNotReceive().CreateDomainIdentityAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SkipsWhenDomainNotPending()
    {
        // Arrange
        var domain = await _domainService.CreateAsync("example.com");
        domain.Status = DomainStatus.Verifying;
        await _domainService.UpdateAsync(domain);

        // Act
        await _job.ExecuteAsync(domain.Id);

        // Assert
        await _mockSesService.DidNotReceive().CreateDomainIdentityAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_CreatesSesIdentityAndDnsRecords()
    {
        // Arrange
        var domain = await _domainService.CreateAsync("example.com");

        var dnsRecords = new[]
        {
            new DnsRecordInfo("CNAME", "token._domainkey.example.com", "token.dkim.amazonses.com")
        };
        _mockSesService.CreateDomainIdentityAsync("example.com", Arg.Any<CancellationToken>())
            .Returns(("arn:aws:ses:domain", dnsRecords));

        _mockCloudflareService.CreateDnsRecordAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("cf-record-id");

        // Act
        await _job.ExecuteAsync(domain.Id);

        // Assert
        await _mockSesService.Received(1).CreateDomainIdentityAsync("example.com", Arg.Any<CancellationToken>());
        await _mockCloudflareService.Received(1).CreateDnsRecordAsync(
            "CNAME", "token._domainkey.example.com", "token.dkim.amazonses.com",
            0, false, Arg.Any<CancellationToken>());

        var updatedDomain = await _domainService.GetByIdAsync(domain.Id);
        updatedDomain!.Status.Should().Be(DomainStatus.Verifying);
        updatedDomain.SesIdentityArn.Should().Be("arn:aws:ses:domain");
    }

    [Fact]
    public async Task ExecuteAsync_SetsDomainToFailedOnSesError()
    {
        // Arrange
        var domain = await _domainService.CreateAsync("example.com");

        _mockSesService.CreateDomainIdentityAsync("example.com", Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("SES error"));

        // Act
        await _job.ExecuteAsync(domain.Id);

        // Assert
        var updatedDomain = await _domainService.GetByIdAsync(domain.Id);
        updatedDomain!.Status.Should().Be(DomainStatus.Failed);
        updatedDomain.FailureReason.Should().Contain("SES error");
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesOnCloudflareError()
    {
        // Arrange
        var domain = await _domainService.CreateAsync("example.com");

        var dnsRecords = new[]
        {
            new DnsRecordInfo("CNAME", "token1._domainkey.example.com", "token1.dkim.amazonses.com"),
            new DnsRecordInfo("CNAME", "token2._domainkey.example.com", "token2.dkim.amazonses.com")
        };
        _mockSesService.CreateDomainIdentityAsync("example.com", Arg.Any<CancellationToken>())
            .Returns(("arn", dnsRecords));

        // First call fails
        _mockCloudflareService.CreateDnsRecordAsync(
            "CNAME", "token1._domainkey.example.com", Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new CloudflareException("DNS error"));

        // Second call succeeds
        _mockCloudflareService.CreateDnsRecordAsync(
            "CNAME", "token2._domainkey.example.com", Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("cf-id");

        // Act
        await _job.ExecuteAsync(domain.Id);

        // Assert - should still set to Verifying despite one DNS error
        var updatedDomain = await _domainService.GetByIdAsync(domain.Id);
        updatedDomain!.Status.Should().Be(DomainStatus.Verifying);
    }
}
