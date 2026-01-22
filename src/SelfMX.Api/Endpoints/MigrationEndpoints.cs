using Microsoft.AspNetCore.Http.HttpResults;
using SelfMX.Api.Services;

namespace SelfMX.Api.Endpoints;

public static class MigrationEndpoints
{
    public static RouteGroupBuilder MapMigrationEndpoints(this RouteGroupBuilder group)
    {
        var migration = group.MapGroup("/migration");

        migration.MapGet("/status", GetMigrationStatus);
        migration.MapPost("/start", StartMigration);

        return group;
    }

    private static async Task<Ok<MigrationStatusResponse>> GetMigrationStatus(
        DataMigrationService migrationService)
    {
        var status = await migrationService.CheckMigrationStatusAsync();
        return TypedResults.Ok(new MigrationStatusResponse(
            status.State.ToString().ToLowerInvariant(),
            status.SourceDatabasePath,
            status.ErrorMessage
        ));
    }

    private static async Task<Results<Ok<MigrationResultResponse>, BadRequest<MigrationResultResponse>>> StartMigration(
        StartMigrationRequest request,
        DataMigrationService migrationService,
        AuditService auditService,
        CancellationToken ct)
    {
        // Check current status first
        var status = await migrationService.CheckMigrationStatusAsync();

        if (status.State == MigrationState.Complete)
        {
            return TypedResults.BadRequest(new MigrationResultResponse(
                false, "Migration already completed", 0, 0, 0, 0));
        }

        if (status.State == MigrationState.InProgress)
        {
            return TypedResults.BadRequest(new MigrationResultResponse(
                false, "Migration already in progress", 0, 0, 0, 0));
        }

        if (status.State == MigrationState.NotNeeded)
        {
            return TypedResults.BadRequest(new MigrationResultResponse(
                false, "No SQLite database found to migrate", 0, 0, 0, 0));
        }

        var sourcePath = request.SourcePath ?? status.SourceDatabasePath;
        if (string.IsNullOrEmpty(sourcePath))
        {
            return TypedResults.BadRequest(new MigrationResultResponse(
                false, "Source database path not provided", 0, 0, 0, 0));
        }

        auditService.Log(new AuditEntry(
            Action: "migration.start",
            ActorType: "admin",
            ActorId: null,
            ResourceType: "database",
            ResourceId: sourcePath,
            StatusCode: 200
        ));

        var result = await migrationService.MigrateAsync(sourcePath, ct);

        auditService.Log(new AuditEntry(
            Action: result.Success ? "migration.complete" : "migration.failed",
            ActorType: "admin",
            ActorId: null,
            ResourceType: "database",
            ResourceId: sourcePath,
            StatusCode: result.Success ? 200 : 500,
            ErrorMessage: result.ErrorMessage
        ));

        var response = new MigrationResultResponse(
            result.Success,
            result.ErrorMessage,
            result.DomainsCount,
            result.ApiKeysCount,
            result.ApiKeyDomainsCount,
            result.AuditLogsCount
        );

        return result.Success
            ? TypedResults.Ok(response)
            : TypedResults.BadRequest(response);
    }
}

public record StartMigrationRequest(string? SourcePath = null);

public record MigrationStatusResponse(
    string State,
    string? SourceDatabasePath,
    string? ErrorMessage
);

public record MigrationResultResponse(
    bool Success,
    string? ErrorMessage,
    int DomainsCount,
    int ApiKeysCount,
    int ApiKeyDomainsCount,
    int AuditLogsCount
);
