namespace SelfMX.Api.Data;

public enum DatabaseProvider
{
    Postgres,
    SqlServer
}

public static class DatabaseProviderResolver
{
    /// <summary>
    /// Resolves the database provider. An explicit <c>Database:Provider</c> setting wins.
    /// Otherwise the connection string decides: <c>Host=</c> means PostgreSQL, while
    /// <c>Server=</c> or <c>Data Source=</c> means SQL Server (all pre-Postgres installs
    /// use one of those). Anything else defaults to PostgreSQL.
    /// </summary>
    public static DatabaseProvider Resolve(string? configuredProvider, string? connectionString)
    {
        if (!string.IsNullOrWhiteSpace(configuredProvider))
        {
            return configuredProvider.Trim().ToLowerInvariant() switch
            {
                "postgres" or "postgresql" or "npgsql" => DatabaseProvider.Postgres,
                "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
                _ => throw new InvalidOperationException(
                    $"Unknown Database:Provider '{configuredProvider}'. Use 'Postgres' or 'SqlServer'.")
            };
        }

        var keys = (connectionString ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2)[0].Trim().ToLowerInvariant())
            .ToHashSet();

        if (keys.Contains("host"))
            return DatabaseProvider.Postgres;
        if (keys.Contains("server") || keys.Contains("data source"))
            return DatabaseProvider.SqlServer;

        return DatabaseProvider.Postgres;
    }
}
