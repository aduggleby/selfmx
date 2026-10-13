using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace SelfMX.Api.Data;

public static class DatabaseRegistration
{
    // Both contexts share one database, so each needs its own history table
    public const string AppHistoryTable = "__EFMigrationsHistory";
    public const string AuditHistoryTable = "__EFMigrationsHistory_Audit";

    /// <summary>
    /// Registers AppDbContext and AuditDbContext backed by the provider-specific subclasses
    /// that own the migrations, with a shared retry and timeout policy.
    /// </summary>
    public static IServiceCollection AddSelfMxDatabase(
        this IServiceCollection services, DatabaseProvider provider, string connectionString)
    {
        if (provider == DatabaseProvider.Postgres)
        {
            services.AddDbContext<AppDbContext, PostgresAppDbContext>(o => Configure(o, provider, connectionString, audit: false));
            services.AddDbContext<AuditDbContext, PostgresAuditDbContext>(o => Configure(o, provider, connectionString, audit: true));
        }
        else
        {
            services.AddDbContext<AppDbContext, SqlServerAppDbContext>(o => Configure(o, provider, connectionString, audit: false));
            services.AddDbContext<AuditDbContext, SqlServerAuditDbContext>(o => Configure(o, provider, connectionString, audit: true));
        }
        return services;
    }

    public static DbContextOptions<TContext> BuildOptions<TContext>(
        DatabaseProvider provider, string connectionString, bool audit) where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        Configure(builder, provider, connectionString, audit);
        return builder.Options;
    }

    private static void Configure(DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString, bool audit)
    {
        var historyTable = audit ? AuditHistoryTable : AppHistoryTable;
        switch (provider)
        {
            case DatabaseProvider.Postgres:
                options.UseNpgsql(connectionString, pg =>
                {
                    pg.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null);
                    pg.CommandTimeout(30);
                    pg.MigrationsHistoryTable(historyTable);
                });
                break;
            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
                    sql.CommandTimeout(30);
                    sql.MigrationsHistoryTable(historyTable);
                });
                break;
        }
    }

    /// <summary>
    /// Applies migrations for both contexts. Databases created before migrations existed
    /// (EnsureCreated + SchemaUpdater) are first brought up to the legacy schema and then
    /// marked as being at the initial migration, so no data is touched.
    /// </summary>
    public static async Task MigrateAsync(AppDbContext appDb, AuditDbContext auditDb, ILogger logger, CancellationToken ct = default)
    {
        await AdoptLegacySchemaAsync(appDb, "Domains", () => SchemaUpdater.UpdateAppSchemaAsync(appDb, logger), logger, ct);
        await appDb.Database.MigrateAsync(ct);

        await AdoptLegacySchemaAsync(auditDb, "AuditLogs", () => SchemaUpdater.UpdateAuditSchemaAsync(auditDb, logger), logger, ct);
        await auditDb.Database.MigrateAsync(ct);
    }

    private static async Task AdoptLegacySchemaAsync(
        DbContext db, string sentinelTable, Func<Task> upgradeLegacySchema, ILogger logger, CancellationToken ct)
    {
        var history = db.GetService<IHistoryRepository>();
        if (!await db.GetService<IRelationalDatabaseCreator>().ExistsAsync(ct) ||
            await history.ExistsAsync(ct) ||
            !await TableExistsAsync(db, sentinelTable, ct))
        {
            return;
        }

        var initialMigration = db.Database.GetMigrations().First();
        logger.LogWarning(
            "Existing {Context} schema without migration history found; adopting it as {Migration}",
            db.GetType().Name, initialMigration);

        await upgradeLegacySchema();
        await EnsureModelColumnsExistAsync(db, ct);
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), ct);
        await db.Database.ExecuteSqlRawAsync(
            history.GetInsertScript(new HistoryRow(initialMigration, ProductInfo.GetVersion())), ct);
    }

    /// <summary>
    /// Refuses to adopt a schema that lacks tables or columns of the initial migration
    /// (e.g. a database from a release whose changes SchemaUpdater never covered).
    /// Nothing has been recorded at this point, so the previous release still works.
    /// </summary>
    private static async Task EnsureModelColumnsExistAsync(DbContext db, CancellationToken ct)
    {
        var expected = db.Model.GetEntityTypes()
            .Select(e => (Table: e.GetTableName(), Entity: e))
            .Where(x => x.Table is not null)
            .SelectMany(x => x.Entity.GetProperties().Select(p =>
                $"{x.Table}.{p.GetColumnName(StoreObjectIdentifier.Table(x.Table!, x.Entity.GetSchema()))}"))
            .ToHashSet();

        var actual = (await db.Database
                .SqlQuery<string>($"SELECT CONCAT(table_name, '.', column_name) AS \"Value\" FROM information_schema.columns")
                .ToListAsync(ct))
            .ToHashSet();

        var missing = expected.Except(actual).Order().ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Cannot adopt the existing {db.GetType().Name} schema; missing columns: {string.Join(", ", missing)}. " +
                "Nothing was changed. Fix the schema manually or restore a backup before upgrading.");
        }
    }

    private static async Task<bool> TableExistsAsync(DbContext db, string table, CancellationToken ct)
    {
        var count = await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM information_schema.tables WHERE table_name = {table}")
            .SingleAsync(ct);
        return count > 0;
    }
}
