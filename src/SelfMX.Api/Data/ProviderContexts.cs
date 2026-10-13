using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SelfMX.Api.Data;

// One subclass per provider so each owns its own migration set
// (EF ties migrations to a context type via [DbContext(typeof(...))]).
// The app resolves AppDbContext / AuditDbContext and never sees these types.

public sealed class PostgresAppDbContext(DbContextOptions<PostgresAppDbContext> options) : AppDbContext(options);

public sealed class SqlServerAppDbContext(DbContextOptions<SqlServerAppDbContext> options) : AppDbContext(options);

public sealed class PostgresAuditDbContext(DbContextOptions<PostgresAuditDbContext> options) : AuditDbContext(options);

public sealed class SqlServerAuditDbContext(DbContextOptions<SqlServerAuditDbContext> options) : AuditDbContext(options);

// Design-time factories for `dotnet ef migrations add`; the connection strings are
// placeholders because adding a migration never connects to the database.

public sealed class PostgresAppDbContextFactory : IDesignTimeDbContextFactory<PostgresAppDbContext>
{
    public PostgresAppDbContext CreateDbContext(string[] args) =>
        new(DatabaseRegistration.BuildOptions<PostgresAppDbContext>(DatabaseProvider.Postgres, DesignTime.Postgres, audit: false));
}

public sealed class SqlServerAppDbContextFactory : IDesignTimeDbContextFactory<SqlServerAppDbContext>
{
    public SqlServerAppDbContext CreateDbContext(string[] args) =>
        new(DatabaseRegistration.BuildOptions<SqlServerAppDbContext>(DatabaseProvider.SqlServer, DesignTime.SqlServer, audit: false));
}

public sealed class PostgresAuditDbContextFactory : IDesignTimeDbContextFactory<PostgresAuditDbContext>
{
    public PostgresAuditDbContext CreateDbContext(string[] args) =>
        new(DatabaseRegistration.BuildOptions<PostgresAuditDbContext>(DatabaseProvider.Postgres, DesignTime.Postgres, audit: true));
}

public sealed class SqlServerAuditDbContextFactory : IDesignTimeDbContextFactory<SqlServerAuditDbContext>
{
    public SqlServerAuditDbContext CreateDbContext(string[] args) =>
        new(DatabaseRegistration.BuildOptions<SqlServerAuditDbContext>(DatabaseProvider.SqlServer, DesignTime.SqlServer, audit: true));
}

internal static class DesignTime
{
    public const string Postgres = "Host=localhost;Database=selfmx_design";
    public const string SqlServer = "Server=localhost;Database=SelfMX_Design;TrustServerCertificate=True";
}
