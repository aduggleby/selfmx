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

        // Create RevokedApiKeys table if it doesn't exist
        var revokedKeysExists = await TableExistsAsync(db, "RevokedApiKeys");
        if (!revokedKeysExists)
        {
            logger.LogInformation("Creating RevokedApiKeys table...");
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE [RevokedApiKeys] (
                    [Id] nvarchar(36) NOT NULL,
                    [Name] nvarchar(100) NOT NULL,
                    [KeyPrefix] nvarchar(11) NOT NULL,
                    [IsAdmin] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [RevokedAt] datetime2 NOT NULL,
                    [ArchivedAt] datetime2 NOT NULL,
                    [LastUsedIp] nvarchar(45) NULL,
                    [LastUsedAt] datetime2 NULL,
                    [AllowedDomainIds] nvarchar(1000) NULL,
                    CONSTRAINT [PK_RevokedApiKeys] PRIMARY KEY ([Id])
                )");
            logger.LogInformation("RevokedApiKeys table created");

            // Create indexes
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX [IX_RevokedApiKeys_RevokedAt] ON [RevokedApiKeys] ([RevokedAt])");
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX [IX_RevokedApiKeys_KeyPrefix] ON [RevokedApiKeys] ([KeyPrefix])");
            logger.LogInformation("RevokedApiKeys indexes created");
        }

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
                    [ResourceId] nvarchar(100) NULL,
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

        // Widen ResourceId column if needed (SES message IDs are ~61 chars, was 36)
        await AlterColumnIfSmallerAsync(db, "AuditLogs", "ResourceId", "nvarchar(100) NULL", 100, logger);

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

    private static async Task AlterColumnIfSmallerAsync(
        DbContext db,
        string tableName,
        string columnName,
        string newColumnDefinition,
        int requiredSize,
        ILogger logger)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            // Get current column size
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS
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
            if (result == null || result == DBNull.Value)
            {
                logger.LogDebug("Column {Table}.{Column} not found or has no max length", tableName, columnName);
                return;
            }

            var currentSize = Convert.ToInt32(result);
            if (currentSize >= requiredSize)
            {
                logger.LogDebug("Column {Table}.{Column} is already {Size} chars (>= {Required})",
                    tableName, columnName, currentSize, requiredSize);
                return;
            }

            logger.LogInformation("Widening column {Table}.{Column} from {OldSize} to {NewSize}...",
                tableName, columnName, currentSize, requiredSize);
        }
        finally
        {
            await connection.CloseAsync();
        }

        // Alter the column
        var sql = $"ALTER TABLE [{tableName}] ALTER COLUMN [{columnName}] {newColumnDefinition}";
        await db.Database.ExecuteSqlRawAsync(sql);

        logger.LogInformation("Column {Table}.{Column} widened successfully", tableName, columnName);
    }
}
