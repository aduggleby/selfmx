using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SelfMX.Api.Data;
using Xunit;

namespace SelfMX.Api.Tests;

public class HealthProbesTests : IClassFixture<SelfMxTestFactory>
{
    private readonly SelfMxTestFactory _factory;

    public HealthProbesTests(SelfMxTestFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/live")]
    [InlineData("/ready")]
    [InlineData("/health")]
    public async Task Probe_IsAnonymousAndHealthy(string path)
    {
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var response = await _factory.CreateClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}
