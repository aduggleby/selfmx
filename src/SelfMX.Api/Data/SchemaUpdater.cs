using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace SelfMX.Api.Data;

/// <summary>
/// Handles schema updates for SQL Server databases.
/// EnsureCreatedAsync only creates tables if they don't exist, but doesn't add new columns.
/// This class detects and applies schema changes for SQL Server.
/// </summary>
public static class SchemaUpdater
{
    /// <summary>
    /// Apply any pending schema updates to the AppDbContext database.
    /// Only runs for SQL Server (SQLite uses EnsureCreated which recreates on schema change).
    /// </summary>
    public static async Task UpdateAppSchemaAsync(AppDbContext db, ILogger logger)
    {
        if (!db.Database.IsSqlServer())
            return;

        logger.LogInformation("Checking for schema updates on SQL Server...");

        // Check and add missing columns to Domains table
        await AddColumnIfNotExistsAsync(db, "Domains", "LastCheckedAt", "datetime2 NULL", logger);

        logger.LogInformation("Schema update check complete");
    }

    /// <summary>
    /// Apply any pending schema updates to the AuditDbContext database.
    /// </summary>
    public static async Task UpdateAuditSchemaAsync(AuditDbContext db, ILogger logger)
    {
        if (!db.Database.IsSqlServer())
            return;

        logger.LogInformation("Checking for audit schema updates on SQL Server...");

        // Check if AuditLogs table exists
        var tableExists = await TableExistsAsync(db, "AuditLogs");
        if (!tableExists)
        {
            logger.LogInformation("Creating AuditLogs table...");
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE [AuditLogs] (
                    [Id] nvarchar(36) NOT NULL,
                    [Timestamp] datetime2 NOT NULL,
                    [Action] nvarchar(50) NOT NULL,
                    [ActorType] nvarchar(20) NOT NULL,
                    [ActorId] nvarchar(50) NULL,
                    [ResourceType] nvarchar(50) NOT NULL,
                    [ResourceId] nvarchar(36) NULL,
                    [StatusCode] int NOT NULL,
                    [ErrorMessage] nvarchar(500) NULL,
                    [Details] nvarchar(4000) NULL,
                    [IpAddress] nvarchar(45) NULL,
                    [UserAgent] nvarchar(500) NULL,
                    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
                )");
            logger.LogInformation("AuditLogs table created");

            // Create indexes
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp] DESC)");
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action])");
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX [IX_AuditLogs_ActorType_ActorId] ON [AuditLogs] ([ActorType], [ActorId])");
            logger.LogInformation("AuditLogs indexes created");
        }

        logger.LogInformation("Audit schema update check complete");
    }

    private static async Task<bool> TableExistsAsync(DbContext db, string tableName)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME = @tableName";
            var param = command.CreateParameter();
            param.ParameterName = "@tableName";
            param.Value = tableName;
            command.Parameters.Add(param);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<bool> ColumnExistsAsync(DbContext db, string tableName, string columnName)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @tableName AND COLUMN_NAME = @columnName";

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@tableName";
            tableParam.Value = tableName;
            command.Parameters.Add(tableParam);

            var columnParam = command.CreateParameter();
            columnParam.ParameterName = "@columnName";
            columnParam.Value = columnName;
            command.Parameters.Add(columnParam);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task AddColumnIfNotExistsAsync(
        DbContext db,
        string tableName,
        string columnName,
        string columnDefinition,
        ILogger logger)
    {
        if (await ColumnExistsAsync(db, tableName, columnName))
        {
            logger.LogDebug("Column {Table}.{Column} already exists", tableName, columnName);
            return;
        }

        logger.LogInformation("Adding column {Table}.{Column}...", tableName, columnName);

        var sql = $"ALTER TABLE [{tableName}] ADD [{columnName}] {columnDefinition}";
        await db.Database.ExecuteSqlRawAsync(sql);

        logger.LogInformation("Column {Table}.{Column} added successfully", tableName, columnName);
    }
}
