using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
        await CompleteLegacySchemaAsync(db, initialMigration, logger, ct);
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), ct);
        await db.Database.ExecuteSqlRawAsync(
            history.GetInsertScript(new HistoryRow(initialMigration, ProductInfo.GetVersion())), ct);
    }

    /// <summary>
    /// Brings a legacy schema up to the initial migration before it is recorded as applied:
    /// tables that are missing entirely are created from the model (SchemaUpdater never created
    /// e.g. SentEmails for databases from before sent-email storage). A table that exists but
    /// lacks columns is refused, because recording the baseline would hide the gap.
    /// </summary>
    private static async Task CompleteLegacySchemaAsync(DbContext db, string initialMigrationId, ILogger logger, CancellationToken ct)
    {
        // Compare against the initial migration's model, not the current one: only the initial
        // migration is recorded here, and later migrations still run afterwards.
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var initialMigration = migrationsAssembly.CreateMigration(
            migrationsAssembly.Migrations[initialMigrationId], db.Database.ProviderName!);
        var targetModel = initialMigration.TargetModel is IMutableModel mutable
            ? mutable.FinalizeModel()
            : initialMigration.TargetModel!;
        targetModel = db.GetService<IModelRuntimeInitializer>().Initialize(targetModel, designTime: true, validationLogger: null);

        var expected = targetModel.GetEntityTypes()
            .Where(e => e.GetTableName() is not null)
            .GroupBy(e => e.GetTableName()!)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(e => e.GetProperties().Select(p =>
                    p.GetColumnName(StoreObjectIdentifier.Table(g.Key, e.GetSchema()))!)).ToHashSet());

        var actual = (await db.Database
                .SqlQuery<string>($"SELECT CONCAT(table_name, '.', column_name) AS \"Value\" FROM information_schema.columns")
                .ToListAsync(ct))
            .ToHashSet();
        var actualTables = actual.Select(c => c[..c.IndexOf('.')]).ToHashSet();

        var missingTables = expected.Keys.Where(t => !actualTables.Contains(t)).ToHashSet();
        var missingColumns = expected
            .Where(t => !missingTables.Contains(t.Key))
            .SelectMany(t => t.Value.Where(c => !actual.Contains($"{t.Key}.{c}")).Select(c => $"{t.Key}.{c}"))
            .Order()
            .ToArray();

        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"Cannot adopt the existing {db.GetType().Name} schema; missing columns: {string.Join(", ", missingColumns)}. " +
                "No migration history was recorded. Fix the schema manually or restore a backup before upgrading.");
        }

        if (missingTables.Count == 0)
            return;

        logger.LogWarning("Creating tables missing from the legacy schema: {Tables}", string.Join(", ", missingTables.Order()));
        var operations = initialMigration.UpOperations
            .Where(op => op is CreateTableOperation create && missingTables.Contains(create.Name)
                      || op is CreateIndexOperation index && missingTables.Contains(index.Table))
            .ToList();

        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(operations, targetModel))
            await db.Database.ExecuteSqlRawAsync(command.CommandText, ct);
    }

    private static async Task<bool> TableExistsAsync(DbContext db, string table, CancellationToken ct)
    {
        var count = await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM information_schema.tables WHERE table_name = {table}")
            .SingleAsync(ct);
        return count > 0;
    }
}
