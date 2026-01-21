using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Selfmx.Api.Data;
using Selfmx.Api.Entities;
using Selfmx.Api.Jobs;
using Selfmx.Api.Services;
using Selfmx.Api.Settings;
using Xunit;

namespace Selfmx.Api.Tests;

public class VerifyDomainsJobTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DomainService _domainService;
    private readonly ISesService _mockSesService;
    private readonly IDnsVerificationService _mockDnsService;
    private readonly ILogger<VerifyDomainsJob> _mockLogger;
    private readonly VerifyDomainsJob _job;

    public VerifyDomainsJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        var appSettings = Options.Create(new AppSettings
        {
            ApiKeyHash = "test",
            VerificationTimeout = TimeSpan.FromHours(72)
        });

        _domainService = new DomainService(_db, appSettings);
        _mockSesService = Substitute.For<ISesService>();
        _mockDnsService = Substitute.For<IDnsVerificationService>();
        _mockLogger = Substitute.For<ILogger<VerifyDomainsJob>>();

        _job = new VerifyDomainsJob(
            _domainService,
            _mockSesService,
            _mockDnsService,
            _mockLogger);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesOnlyVerifyingDomains()
    {
        // Arrange
        var pendingDomain = await _domainService.CreateAsync("pending.com");

        var verifyingDomain = await _domainService.CreateAsync("verifying.com");
        verifyingDomain.Status = DomainStatus.Verifying;
        verifyingDomain.VerificationStartedAt = DateTime.UtcNow;
        await _domainService.UpdateAsync(verifyingDomain);

        _mockSesService.CheckDkimVerificationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _job.ExecuteAsync();

        // Assert - only verifying domain should be checked
        await _mockSesService.Received(1).CheckDkimVerificationAsync("verifying.com", Arg.Any<CancellationToken>());
        await _mockSesService.DidNotReceive().CheckDkimVerificationAsync("pending.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MarksDomainAsVerifiedWhenSesConfirms()
    {
        // Arrange
        var domain = await _domainService.CreateAsync("example.com");
        domain.Status = DomainStatus.Verifying;
        domain.VerificationStartedAt = DateTime.UtcNow;
        await _domainService.UpdateAsync(domain);

        _mockSesService.CheckDkimVerificationAsync("example.com", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _job.ExecuteAsync();

        // Assert
        var updatedDomain = await _domainService.GetByIdAsync(domain.Id);
        updatedDomain!.Status.Should().Be(DomainStatus.Verified);
        updatedDomain.VerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_MarksDomainAsFailedWhenTimedOut()
    {
        // Arrange
        var domain = await _domainService.CreateAsync("example.com");
        domain.Status = DomainStatus.Verifying;
        domain.VerificationStartedAt = DateTime.UtcNow.AddHours(-73); // Timed out
        await _domainService.UpdateAsync(domain);

        // Act
        await _job.ExecuteAsync();

        // Assert
        var updatedDomain = await _domainService.GetByIdAsync(domain.Id);
        updatedDomain!.Status.Should().Be(DomainStatus.Failed);
        updatedDomain.FailureReason.Should().Contain("timed out");

        // SES should not be checked for timed out domains
        await _mockSesService.DidNotReceive().CheckDkimVerificationAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ChecksDnsRecordsWhenSesNotVerified()
    {
        // Arrange
        var domain = await _domainService.CreateAsync("example.com");
        domain.Status = DomainStatus.Verifying;
        domain.VerificationStartedAt = DateTime.UtcNow;
        domain.DnsRecordsJson = new[]
        {
            new DnsRecordInfo("CNAME", "token._domainkey.example.com", "token.dkim.amazonses.com")
        }.SerializeDnsRecords();
        await _domainService.UpdateAsync(domain);

        _mockSesService.CheckDkimVerificationAsync("example.com", Arg.Any<CancellationToken>())
            .Returns(false);

        _mockDnsService.VerifyAllDkimRecordsAsync(
            Arg.Any<DnsRecordInfo[]>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _job.ExecuteAsync();

        // Assert
        await _mockDnsService.Received(1).VerifyAllDkimRecordsAsync(
            Arg.Any<DnsRecordInfo[]>(), Arg.Any<CancellationToken>());

        // Domain should still be verifying (waiting for SES)
        var updatedDomain = await _domainService.GetByIdAsync(domain.Id);
        updatedDomain!.Status.Should().Be(DomainStatus.Verifying);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesMultipleDomains()
    {
        // Arrange
        var domain1 = await _domainService.CreateAsync("domain1.com");
        domain1.Status = DomainStatus.Verifying;
        domain1.VerificationStartedAt = DateTime.UtcNow;
        await _domainService.UpdateAsync(domain1);

        var domain2 = await _domainService.CreateAsync("domain2.com");
        domain2.Status = DomainStatus.Verifying;
        domain2.VerificationStartedAt = DateTime.UtcNow;
        await _domainService.UpdateAsync(domain2);

        _mockSesService.CheckDkimVerificationAsync("domain1.com", Arg.Any<CancellationToken>())
            .Returns(true);
        _mockSesService.CheckDkimVerificationAsync("domain2.com", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _job.ExecuteAsync();

        // Assert
        var updatedDomain1 = await _domainService.GetByIdAsync(domain1.Id);
        updatedDomain1!.Status.Should().Be(DomainStatus.Verified);

        var updatedDomain2 = await _domainService.GetByIdAsync(domain2.Id);
        updatedDomain2!.Status.Should().Be(DomainStatus.Verifying);
    }
}
