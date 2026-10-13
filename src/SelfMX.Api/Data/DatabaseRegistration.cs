using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace SelfMX.Api.Data;

public static class DatabaseRegistration
{
    /// <summary>
    /// Registers AppDbContext and AuditDbContext for the given provider with a shared
    /// retry and timeout policy.
    /// </summary>
    public static IServiceCollection AddSelfMxDatabase(
        this IServiceCollection services, DatabaseProvider provider, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => Configure(options, provider, connectionString));
        services.AddDbContext<AuditDbContext>(options => Configure(options, provider, connectionString));
        return services;
    }

    private static void Configure(DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString)
    {
        switch (provider)
        {
            case DatabaseProvider.Postgres:
                options.UseNpgsql(connectionString, pg =>
                {
                    pg.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null);
                    pg.CommandTimeout(30);
                });
                break;
            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
                    sql.CommandTimeout(30);
                });
                break;
        }
    }

    /// <summary>
    /// Creates the database if needed and the context's tables if its first table is missing.
    /// Both contexts share one database, so EnsureCreated alone would skip the second context.
    /// </summary>
    public static async Task EnsureTablesAsync(DbContext db, CancellationToken ct = default)
    {
        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync(ct))
            await creator.CreateAsync(ct);

        var table = db.Model.GetEntityTypes().Select(e => e.GetTableName()).First(t => t is not null)!;
        var exists = await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM information_schema.tables WHERE table_name = {table}")
            .SingleAsync(ct);

        if (exists == 0)
            await creator.CreateTablesAsync(ct);
    }
}
