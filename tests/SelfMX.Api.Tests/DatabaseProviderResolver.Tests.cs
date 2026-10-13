using FluentAssertions;
using SelfMX.Api.Data;
using Xunit;

namespace SelfMX.Api.Tests;

public class DatabaseProviderResolverTests
{
    [Theory]
    [InlineData("Postgres", DatabaseProvider.Postgres)]
    [InlineData("postgresql", DatabaseProvider.Postgres)]
    [InlineData("SqlServer", DatabaseProvider.SqlServer)]
    [InlineData("mssql", DatabaseProvider.SqlServer)]
    public void ExplicitProvider_WinsOverConnectionString(string configured, DatabaseProvider expected)
    {
        DatabaseProviderResolver.Resolve(configured, "Server=localhost;Database=x")
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=selfmx;Username=selfmx", DatabaseProvider.Postgres)]
    [InlineData("Server=localhost,17402;Database=SelfMX;User Id=sa;Password=x;TrustServerCertificate=True", DatabaseProvider.SqlServer)]
    [InlineData("server = sql01; database=SelfMX", DatabaseProvider.SqlServer)]
    [InlineData("Data Source=sql01;Initial Catalog=SelfMX", DatabaseProvider.SqlServer)]
    [InlineData("", DatabaseProvider.Postgres)]
    [InlineData(null, DatabaseProvider.Postgres)]
    public void UnsetProvider_IsInferredFromConnectionString(string? connectionString, DatabaseProvider expected)
    {
        DatabaseProviderResolver.Resolve(null, connectionString).Should().Be(expected);
        DatabaseProviderResolver.Resolve("", connectionString).Should().Be(expected);
    }

    [Fact]
    public void PostgresConnectionString_DisablesGssProbeUnlessConfigured()
    {
        DatabaseProviderResolver.NormalizeConnectionString(DatabaseProvider.Postgres, "Host=db;Database=selfmx")
            .Should().Contain("GSS Encryption Mode=Disable");
        DatabaseProviderResolver.NormalizeConnectionString(DatabaseProvider.Postgres, "Host=db;Gss Encryption Mode=Require")
            .Should().Contain("GSS Encryption Mode=Require");
        DatabaseProviderResolver.NormalizeConnectionString(DatabaseProvider.SqlServer, "Server=db")
            .Should().Be("Server=db");
    }

    [Fact]
    public void UnknownProvider_Throws()
    {
        var act = () => DatabaseProviderResolver.Resolve("oracle", "Host=x");

        act.Should().Throw<InvalidOperationException>().WithMessage("*oracle*");
    }
}
