using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Selfmx.Api.Data;
using Selfmx.Api.Entities;
using Selfmx.Api.Services;
using Selfmx.Api.Settings;
using Xunit;

namespace Selfmx.Api.Tests;

public class DomainServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DomainService _service;

    public DomainServiceTests()
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

        _service = new DomainService(_db, appSettings);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task CreateAsync_CreatesNewDomain()
    {
        var domain = await _service.CreateAsync("example.com");

        domain.Should().NotBeNull();
        domain.Name.Should().Be("example.com");
        domain.Status.Should().Be(DomainStatus.Pending);
        domain.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAsync_NormalizesToLowercase()
    {
        var domain = await _service.CreateAsync("EXAMPLE.COM");

        domain.Name.Should().Be("example.com");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsExistingDomain()
    {
        var created = await _service.CreateAsync("example.com");

        var retrieved = await _service.GetByIdAsync(created.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForNonExistent()
    {
        var result = await _service.GetByIdAsync("non-existent-id");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsExistingDomain()
    {
        await _service.CreateAsync("example.com");

        var result = await _service.GetByNameAsync("example.com");

        result.Should().NotBeNull();
        result!.Name.Should().Be("example.com");
    }

    [Fact]
    public async Task ListAsync_ReturnsPaginatedResults()
    {
        for (int i = 0; i < 5; i++)
        {
            await _service.CreateAsync($"domain{i}.com");
        }

        var (items, total) = await _service.ListAsync(1, 2);

        items.Should().HaveCount(2);
        total.Should().Be(5);
    }

    [Fact]
    public async Task DeleteAsync_RemovesDomain()
    {
        var domain = await _service.CreateAsync("example.com");

        await _service.DeleteAsync(domain);

        var result = await _service.GetByIdAsync(domain.Id);
        result.Should().BeNull();
    }

    [Fact]
    public void IsTimedOut_ReturnsTrueWhenVerificationExceedsTimeout()
    {
        var domain = new Domain
        {
            Id = Guid.NewGuid().ToString(),
            Name = "example.com",
            Status = DomainStatus.Verifying,
            VerificationStartedAt = DateTime.UtcNow.AddHours(-73)
        };

        var result = _service.IsTimedOut(domain);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsTimedOut_ReturnsFalseWhenWithinTimeout()
    {
        var domain = new Domain
        {
            Id = Guid.NewGuid().ToString(),
            Name = "example.com",
            Status = DomainStatus.Verifying,
            VerificationStartedAt = DateTime.UtcNow.AddHours(-1)
        };

        var result = _service.IsTimedOut(domain);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsTimedOut_ReturnsFalseForPendingDomains()
    {
        var domain = new Domain
        {
            Id = Guid.NewGuid().ToString(),
            Name = "example.com",
            Status = DomainStatus.Pending,
            VerificationStartedAt = DateTime.UtcNow.AddHours(-100)
        };

        var result = _service.IsTimedOut(domain);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetDomainsNeedingVerificationAsync_ReturnsOnlyVerifyingDomains()
    {
        await _service.CreateAsync("pending.com");

        var verifying = await _service.CreateAsync("verifying.com");
        verifying.Status = DomainStatus.Verifying;
        await _service.UpdateAsync(verifying);

        var result = await _service.GetDomainsNeedingVerificationAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("verifying.com");
    }
}
