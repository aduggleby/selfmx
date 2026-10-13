using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SelfMX.Api.Contracts;
using SelfMX.Api.Data;
using SelfMX.Api.Services;
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
            // Adopted at the initial migration, then every later migration applied normally
            (await app.Database.GetAppliedMigrationsAsync()).Should().Equal(app.Database.GetMigrations());
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
    public async Task SqlServer_LegacyDatabaseWithoutSentEmails_GetsTheTableCreated()
    {
        Skip.If(string.IsNullOrEmpty(SqlServerServer), "SELFMX_TEST_SQLSERVER not set");
        await using var legacy = await TestDatabase.CreateSqlServerAsync(SqlServerServer!);
        await using var fresh = await TestDatabase.CreateSqlServerAsync(SqlServerServer!);
        await CreateLegacySqlServerSchemaAsync(legacy.ConnectionString);
        await using (var app = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(legacy.ConnectionString).Options))
        {
            // Databases created between SQL Server support and sent-email storage have no SentEmails table
            await app.Database.ExecuteSqlRawAsync("DROP TABLE [SentEmails]");
        }

        await MigrateAsync(DatabaseProvider.SqlServer, legacy.ConnectionString);
        await MigrateAsync(DatabaseProvider.SqlServer, fresh.ConnectionString);

        (await SqlServerSchemaAsync(legacy.ConnectionString))
            .Should().Contain(row => row.StartsWith("col SentEmails."))
            .And.Equal(await SqlServerSchemaAsync(fresh.ConnectionString));
    }

    [SkippableFact]
    public async Task SqlServer_LegacyDatabaseMissingColumns_IsRefusedWithoutRecordingHistory()
    {
        Skip.If(string.IsNullOrEmpty(SqlServerServer), "SELFMX_TEST_SQLSERVER not set");
        await using var legacy = await TestDatabase.CreateSqlServerAsync(SqlServerServer!);
        await CreateLegacySqlServerSchemaAsync(legacy.ConnectionString);
        await using (var app = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(legacy.ConnectionString).Options))
        {
            await app.Database.ExecuteSqlRawAsync("ALTER TABLE [Domains] DROP COLUMN [FailureReason]");
        }

        var act = () => MigrateAsync(DatabaseProvider.SqlServer, legacy.ConnectionString);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Domains.FailureReason*");
        (await SqlServerSchemaAsync(legacy.ConnectionString)).Should().NotContain(row => row.Contains("__EFMigrationsHistory"));
    }

    [SkippableTheory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.SqlServer)]
    public async Task Import_OnRealDatabase_IsAtomicAndMergesAuditLogs(DatabaseProvider provider)
    {
        var server = provider == DatabaseProvider.Postgres ? PostgresServer : SqlServerServer;
        Skip.If(string.IsNullOrEmpty(server), $"Connection string for {provider} not set");
        await using var db = provider == DatabaseProvider.Postgres
            ? await TestDatabase.CreatePostgresAsync(server!)
            : await TestDatabase.CreateSqlServerAsync(server!);
        await MigrateAsync(provider, db.ConnectionString);

        // Dates without a zone (as from an old export) must still import into timestamptz
        var unspecified = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);
        var audit = new AuditLogExport("a1", unspecified, "domain.create", "admin", null, "domain", "d1", null, null, 201, null, null);
        DataExport Export(params ApiKeyDomainExport[] links) => new(
            DataExport.CurrentFormatVersion, "test", "test", DateTime.UtcNow,
            [new DomainExport("d1", "a.example", DomainStatus.Verified, unspecified, null, unspecified, null, null, null, null)],
            [new ApiKeyExport("k1", "key", "hash", "salt", "re_abcdefgh", false, unspecified, null, null, null)],
            links, [], [audit, audit]);

        var rejected = () => ImportAsync(provider, db.ConnectionString, Export(new ApiKeyDomainExport("k1", "missing")));
        await rejected.Should().ThrowAsync<DataImportException>();
        await AssertCountsAsync(provider, db.ConnectionString, domains: 0, auditLogs: 0);

        var result = await ImportAsync(provider, db.ConnectionString, Export(new ApiKeyDomainExport("k1", "d1")));

        result.Should().BeEquivalentTo(new ImportResult(1, 1, 1, 0, AuditLogs: 1, AuditLogsSkipped: 1, PendingDomainsRequeued: 0));
        await AssertCountsAsync(provider, db.ConnectionString, domains: 1, auditLogs: 1);
    }

    [SkippableTheory]
    [InlineData(DatabaseProvider.Postgres)]
    [InlineData(DatabaseProvider.SqlServer)]
    public async Task SendStats_OnRealDatabase_CountEachWindowAndRespectRetention(DatabaseProvider provider)
    {
        var server = provider == DatabaseProvider.Postgres ? PostgresServer : SqlServerServer;
        Skip.If(string.IsNullOrEmpty(server), $"Connection string for {provider} not set");
        await using var db = provider == DatabaseProvider.Postgres
            ? await TestDatabase.CreatePostgresAsync(server!)
            : await TestDatabase.CreateSqlServerAsync(server!);
        await MigrateAsync(provider, db.ConnectionString);

        var now = DateTime.UtcNow;
        await using (var app = CreateAppDb(provider, db.ConnectionString))
        {
            app.Domains.Add(new Domain { Id = "d1", Name = "a.example", Status = DomainStatus.Verified });
            app.Domains.Add(new Domain { Id = "d2", Name = "b.example", Status = DomainStatus.Verified });
            app.ApiKeys.AddRange(
                new ApiKey { Id = "k1", Name = "active", KeyHash = "h", KeySalt = "s", KeyPrefix = "re_aaaaaaaa" },
                new ApiKey { Id = "k2", Name = "revoked", KeyHash = "h", KeySalt = "s", KeyPrefix = "re_bbbbbbbb", RevokedAt = now });
            app.ApiKeyDomains.AddRange(
                new ApiKeyDomain { ApiKeyId = "k1", DomainId = "d1" }, new ApiKeyDomain { ApiKeyId = "k2", DomainId = "d1" },
                new ApiKeyDomain { ApiKeyId = "k1", DomainId = "d2" });
            SentEmail Sent(DateTime at, string? key) => new()
            {
                Id = Guid.NewGuid().ToString(), MessageId = Guid.NewGuid().ToString(), SentAt = at, FromAddress = "a@a.example",
                ToAddresses = "[]", Subject = "s", DomainId = "d1", ApiKeyId = key
            };
            app.SentEmails.AddRange(
                Sent(now.AddHours(-1), "k1"), Sent(now.AddHours(-30), "k1"), Sent(now.AddDays(-10), "k1"),
                Sent(now.AddDays(-60), "k1"), Sent(now.AddDays(-100), "k1"), Sent(now.AddHours(-1), null));
            await app.SaveChangesAsync();
        }

        await using (var app = CreateAppDb(provider, db.ConnectionString))
        {
            (await ApiKeySendStats.GetAsync(app, retentionDays: null, now))
                .Should().Equal(new ApiKeyStatsResponse("k1", 1, 2, 3, 4));
            (await ApiKeySendStats.GetAsync(app, retentionDays: 30, now))
                .Should().Equal(new ApiKeyStatsResponse("k1", 1, 2, 3, null));

            // Domain d1: the five keyed sends plus one admin-UI send; one active key (k2 is revoked).
            // Domain d2: no sends, still listed for its active key.
            (await ApiKeySendStats.GetForDomainsAsync(app, retentionDays: null, now))
                .Should().BeEquivalentTo(new[]
                {
                    new DomainStatsResponse("d1", ActiveApiKeys: 1, Last24h: 2, Last7d: 3, Last30d: 4, Last90d: 5),
                    new DomainStatsResponse("d2", ActiveApiKeys: 1, Last24h: 0, Last7d: 0, Last30d: 0, Last90d: 0)
                });
        }
    }

    private static async Task<ImportResult> ImportAsync(DatabaseProvider provider, string connectionString, DataExport data)
    {
        await using var app = CreateAppDb(provider, connectionString);
        await using var audit = CreateAuditDb(provider, connectionString);
        var service = new DataTransferService(app, audit, new NoopBackgroundJobClient(), NullLogger<DataTransferService>.Instance);
        return await service.ImportAsync(data);
    }

    private static async Task AssertCountsAsync(DatabaseProvider provider, string connectionString, int domains, int auditLogs)
    {
        await using var app = CreateAppDb(provider, connectionString);
        await using var audit = CreateAuditDb(provider, connectionString);
        (await app.Domains.CountAsync()).Should().Be(domains);
        (await audit.AuditLogs.CountAsync()).Should().Be(auditLogs);
    }

    private static AppDbContext CreateAppDb(DatabaseProvider provider, string connectionString) =>
        provider == DatabaseProvider.Postgres
            ? new PostgresAppDbContext(Options<PostgresAppDbContext>(provider, connectionString, audit: false))
            : new SqlServerAppDbContext(Options<SqlServerAppDbContext>(provider, connectionString, audit: false));

    private static AuditDbContext CreateAuditDb(DatabaseProvider provider, string connectionString) =>
        provider == DatabaseProvider.Postgres
            ? new PostgresAuditDbContext(Options<PostgresAuditDbContext>(provider, connectionString, audit: true))
            : new SqlServerAuditDbContext(Options<SqlServerAuditDbContext>(provider, connectionString, audit: true));

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
            // EnsureCreated uses the current model; remove what later migrations add, as legacy databases never had it
            await app.Database.ExecuteSqlRawAsync(
                "DROP INDEX [IX_SentEmails_SentAt_ApiKeyId_DomainId] ON [SentEmails]; CREATE INDEX [IX_SentEmails_SentAt] ON [SentEmails] ([SentAt]);");
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

        // Query-string dates (e.g. ?from=2026-10-13) bind with Kind=Unspecified; PostgreSQL rejects those
        // unless they are marked as UTC, which the model-wide converter does for parameters too
        var from = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Unspecified);
        var to = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Unspecified);
        (await audit.AuditLogs.CountAsync(l => l.Timestamp >= from && l.Timestamp <= to)).Should().Be(1);
        (await audit.AuditLogs.SingleAsync()).Timestamp.Kind.Should().Be(DateTimeKind.Utc);
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
