using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;
using Xunit;

namespace SelfMX.Api.Tests;

public class DatabaseMigrationModelTests
{
    public static TheoryData<string> Contexts => new()
    {
        nameof(PostgresAppDbContext), nameof(PostgresAuditDbContext),
        nameof(SqlServerAppDbContext), nameof(SqlServerAuditDbContext)
    };

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Migrations_CoverTheCurrentModel(string context)
    {
        using DbContext db = context switch
        {
            nameof(PostgresAppDbContext) => new PostgresAppDbContextFactory().CreateDbContext([]),
            nameof(PostgresAuditDbContext) => new PostgresAuditDbContextFactory().CreateDbContext([]),
            nameof(SqlServerAppDbContext) => new SqlServerAppDbContextFactory().CreateDbContext([]),
            _ => new SqlServerAuditDbContextFactory().CreateDbContext([])
        };

        db.Database.GetMigrations().Should().NotBeEmpty();
        db.Database.HasPendingModelChanges().Should().BeFalse(
            "run `dotnet ef migrations add` for {0} after changing the model", context);
    }
}

/// <summary>
/// Runs against real servers. Set SELFMX_TEST_POSTGRES and/or SELFMX_TEST_SQLSERVER to a
/// connection string with rights to create databases (the dev compose containers work);
/// otherwise these tests are skipped.
/// </summary>
public class DatabaseMigrationIntegrationTests
{
    private static readonly string? PostgresServer = Environment.GetEnvironmentVariable("SELFMX_TEST_POSTGRES");
    private static readonly string? SqlServerServer = Environment.GetEnvironmentVariable("SELFMX_TEST_SQLSERVER");

    [SkippableFact]
    public async Task Postgres_FreshDatabase_MigratesAndStoresData()
    {
        Skip.If(string.IsNullOrEmpty(PostgresServer), "SELFMX_TEST_POSTGRES not set");
        await using var db = await TestDatabase.CreatePostgresAsync(PostgresServer!);

        await MigrateAsync(DatabaseProvider.Postgres, db.ConnectionString);
        await MigrateAsync(DatabaseProvider.Postgres, db.ConnectionString); // idempotent

        await AssertRoundTripAsync(DatabaseProvider.Postgres, db.ConnectionString);
    }

    [SkippableFact]
    public async Task SqlServer_FreshDatabase_MigratesAndStoresData()
    {
        Skip.If(string.IsNullOrEmpty(SqlServerServer), "SELFMX_TEST_SQLSERVER not set");
        await using var db = await TestDatabase.CreateSqlServerAsync(SqlServerServer!);

        await MigrateAsync(DatabaseProvider.SqlServer, db.ConnectionString);
        await MigrateAsync(DatabaseProvider.SqlServer, db.ConnectionString); // idempotent

        await AssertRoundTripAsync(DatabaseProvider.SqlServer, db.ConnectionString);
    }

    [SkippableFact]
    public async Task SqlServer_LegacyDatabase_IsAdoptedWithoutDataLoss_AndMatchesFreshSchema()
    {
        Skip.If(string.IsNullOrEmpty(SqlServerServer), "SELFMX_TEST_SQLSERVER not set");
        await using var legacy = await TestDatabase.CreateSqlServerAsync(SqlServerServer!);
        await using var fresh = await TestDatabase.CreateSqlServerAsync(SqlServerServer!);

        await CreateLegacySqlServerSchemaAsync(legacy.ConnectionString);

        await MigrateAsync(DatabaseProvider.SqlServer, legacy.ConnectionString);
        await MigrateAsync(DatabaseProvider.SqlServer, fresh.ConnectionString);

        await using (var app = new SqlServerAppDbContext(Options<SqlServerAppDbContext>(DatabaseProvider.SqlServer, legacy.ConnectionString, audit: false)))
        {
            var domain = await app.Domains.SingleAsync();
            domain.Name.Should().Be("legacy.example");
            domain.Status.Should().Be(DomainStatus.Verified);
            (await app.Database.GetAppliedMigrationsAsync()).Should().ContainSingle();
        }
        await using (var audit = new SqlServerAuditDbContext(Options<SqlServerAuditDbContext>(DatabaseProvider.SqlServer, legacy.ConnectionString, audit: true)))
        {
            (await audit.AuditLogs.SingleAsync()).Action.Should().Be("domain.create");
            (await audit.Database.GetAppliedMigrationsAsync()).Should().ContainSingle();
        }

        var legacySchema = await SqlServerSchemaAsync(legacy.ConnectionString);
        legacySchema.Should().Contain([
            "col AuditLogs.ResourceId nvarchar(100) YES",
            "col Domains.LastCheckedAt datetime2() YES",
            "idx AuditLogs.IX_AuditLogs_Timestamp Timestamp DESC"
        ]).And.Contain(row => row.StartsWith("col RevokedApiKeys."));
        legacySchema.Should().Equal(await SqlServerSchemaAsync(fresh.ConnectionString));
    }

    [SkippableFact]
    public async Task SqlServer_LegacyDatabaseMissingTables_IsRefusedAndLeftUntouched()
    {
        Skip.If(string.IsNullOrEmpty(SqlServerServer), "SELFMX_TEST_SQLSERVER not set");
        await using var legacy = await TestDatabase.CreateSqlServerAsync(SqlServerServer!);
        await CreateLegacySqlServerSchemaAsync(legacy.ConnectionString);
        await using (var app = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(legacy.ConnectionString).Options))
        {
            // Databases created between SQL Server support and sent-email storage have no SentEmails table
            await app.Database.ExecuteSqlRawAsync("DROP TABLE [SentEmails]");
        }

        var act = () => MigrateAsync(DatabaseProvider.SqlServer, legacy.ConnectionString);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*SentEmails.*");
        var schema = await SqlServerSchemaAsync(legacy.ConnectionString);
        schema.Should().NotContain(row => row.Contains("__EFMigrationsHistory"));
    }

    /// <summary>
    /// Recreates a database as an older release left it: tables from EnsureCreated, AuditLogs
    /// from SchemaUpdater's hand-written DDL, and changes that SchemaUpdater added later
    /// (Domains.LastCheckedAt, RevokedApiKeys, the wider AuditLogs.ResourceId) still missing.
    /// </summary>
    private static async Task CreateLegacySqlServerSchemaAsync(string connectionString)
    {
        await using (var app = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options))
        {
            await app.Database.EnsureCreatedAsync();
            await app.Database.ExecuteSqlRawAsync("ALTER TABLE [Domains] DROP COLUMN [LastCheckedAt]; DROP TABLE [RevokedApiKeys];");
            await app.Database.ExecuteSqlRawAsync(
                "INSERT INTO [Domains] ([Id], [Name], [Status], [CreatedAt]) VALUES ('d1', 'legacy.example', 'Verified', SYSUTCDATETIME())");
        }

        await using var audit = new AuditDbContext(new DbContextOptionsBuilder<AuditDbContext>().UseSqlServer(connectionString).Options);
        await SchemaUpdater.UpdateAuditSchemaAsync(audit, NullLogger.Instance);
        await audit.Database.ExecuteSqlRawAsync("ALTER TABLE [AuditLogs] ALTER COLUMN [ResourceId] nvarchar(36) NULL");
        await audit.Database.ExecuteSqlRawAsync(
            "INSERT INTO [AuditLogs] ([Id], [Timestamp], [Action], [ActorType], [ResourceType], [StatusCode]) " +
            "VALUES ('a1', SYSUTCDATETIME(), 'domain.create', 'admin', 'domain', 201)");
    }

    private static async Task MigrateAsync(DatabaseProvider provider, string connectionString)
    {
        await using AppDbContext app = provider == DatabaseProvider.Postgres
            ? new PostgresAppDbContext(Options<PostgresAppDbContext>(provider, connectionString, audit: false))
            : new SqlServerAppDbContext(Options<SqlServerAppDbContext>(provider, connectionString, audit: false));
        await using AuditDbContext audit = provider == DatabaseProvider.Postgres
            ? new PostgresAuditDbContext(Options<PostgresAuditDbContext>(provider, connectionString, audit: true))
            : new SqlServerAuditDbContext(Options<SqlServerAuditDbContext>(provider, connectionString, audit: true));

        await DatabaseRegistration.MigrateAsync(app, audit, NullLogger.Instance);
    }

    private static async Task AssertRoundTripAsync(DatabaseProvider provider, string connectionString)
    {
        var createdAt = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        await using (AppDbContext app = provider == DatabaseProvider.Postgres
            ? new PostgresAppDbContext(Options<PostgresAppDbContext>(provider, connectionString, audit: false))
            : new SqlServerAppDbContext(Options<SqlServerAppDbContext>(provider, connectionString, audit: false)))
        {
            app.Domains.Add(new Domain { Id = "d1", Name = "example.com", CreatedAt = createdAt });
            app.ApiKeys.Add(new ApiKey { Id = "k1", Name = "key", KeyHash = "hash", KeySalt = "salt", KeyPrefix = "re_abcdefgh", CreatedAt = createdAt });
            app.ApiKeyDomains.Add(new ApiKeyDomain { ApiKeyId = "k1", DomainId = "d1" });
            await app.SaveChangesAsync();
        }

        await using (AppDbContext app = provider == DatabaseProvider.Postgres
            ? new PostgresAppDbContext(Options<PostgresAppDbContext>(provider, connectionString, audit: false))
            : new SqlServerAppDbContext(Options<SqlServerAppDbContext>(provider, connectionString, audit: false)))
        {
            var key = await app.ApiKeys.Include(k => k.AllowedDomains).SingleAsync();
            key.AllowedDomains.Should().ContainSingle(d => d.DomainId == "d1");
            (await app.Domains.SingleAsync()).CreatedAt.Should().Be(createdAt);
        }

        await using AuditDbContext audit = provider == DatabaseProvider.Postgres
            ? new PostgresAuditDbContext(Options<PostgresAuditDbContext>(provider, connectionString, audit: true))
            : new SqlServerAuditDbContext(Options<SqlServerAuditDbContext>(provider, connectionString, audit: true));
        audit.AuditLogs.Add(new AuditLog { Id = "a1", Action = "domain.create", ActorType = "admin", ResourceType = "domain", Timestamp = createdAt });
        await audit.SaveChangesAsync();
        (await audit.AuditLogs.CountAsync()).Should().Be(1);
    }

    private static DbContextOptions<T> Options<T>(DatabaseProvider provider, string connectionString, bool audit) where T : DbContext =>
        DatabaseRegistration.BuildOptions<T>(provider, connectionString, audit);

    private static async Task<List<string>> SqlServerSchemaAsync(string connectionString)
    {
        const string sql = """
            SELECT 'col ' + TABLE_NAME + '.' + COLUMN_NAME + ' ' + DATA_TYPE + '(' + ISNULL(CAST(CHARACTER_MAXIMUM_LENGTH AS varchar(10)), '') + ') ' + IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS
            UNION ALL
            SELECT 'idx ' + t.name COLLATE DATABASE_DEFAULT + '.' + i.name COLLATE DATABASE_DEFAULT + ' ' + c.name COLLATE DATABASE_DEFAULT
                + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END
                + CASE WHEN i.is_unique = 1 THEN ' UNIQUE' ELSE '' END
            FROM sys.indexes i
            JOIN sys.tables t ON t.object_id = i.object_id
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            UNION ALL
            SELECT 'fk ' + name COLLATE DATABASE_DEFAULT + ' ' + delete_referential_action_desc COLLATE DATABASE_DEFAULT FROM sys.foreign_keys
            """;
        var rows = new List<string>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(reader.GetString(0));
        rows.Sort(StringComparer.Ordinal);
        return rows;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly Func<Task> _drop;

        private TestDatabase(string connectionString, Func<Task> drop)
        {
            ConnectionString = connectionString;
            _drop = drop;
        }

        public string ConnectionString { get; }

        public static Task<TestDatabase> CreatePostgresAsync(string server)
        {
            var name = $"selfmx_test_{Guid.NewGuid():N}"[..24];
            var cs = new NpgsqlConnectionStringBuilder(server) { Database = name }.ConnectionString;
            var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ConnectionString;
            return Task.FromResult(new TestDatabase(cs, async () =>
            {
                NpgsqlConnection.ClearAllPools();
                await using var connection = new NpgsqlConnection(admin);
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
                await command.ExecuteNonQueryAsync();
            }));
        }

        public static Task<TestDatabase> CreateSqlServerAsync(string server)
        {
            var name = $"SelfMX_Test_{Guid.NewGuid():N}"[..24];
            var cs = new SqlConnectionStringBuilder(server) { InitialCatalog = name }.ConnectionString;
            var admin = new SqlConnectionStringBuilder(server) { InitialCatalog = "master" }.ConnectionString;
            return Task.FromResult(new TestDatabase(cs, async () =>
            {
                SqlConnection.ClearAllPools();
                await using var connection = new SqlConnection(admin);
                await connection.OpenAsync();
                await using var command = new SqlCommand(
                    $"IF DB_ID('{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END",
                    connection);
                await command.ExecuteNonQueryAsync();
            }));
        }

        public ValueTask DisposeAsync() => new(_drop());
    }
}
