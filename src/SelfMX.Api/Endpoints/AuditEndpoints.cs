using Microsoft.AspNetCore.Http.HttpResults;
using SelfMX.Api.Contracts.Responses;
using SelfMX.Api.Entities;
using SelfMX.Api.Services;

namespace SelfMX.Api.Endpoints;

public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder group)
    {
        var audit = group.MapGroup("/audit-logs");

        audit.MapGet("/", ListAuditLogs);

        return group;
    }

    private static async Task<Ok<PaginatedResponse<AuditLogResponse>>> ListAuditLogs(
        AuditService auditService,
        int page = 1,
        int limit = 50,
        string? action = null,
        string? actorId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        var (items, total) = await auditService.ListAsync(page, limit, action, actorId, from, to, ct);
        var responses = items.Select(AuditLogResponse.FromEntity).ToArray();
        return TypedResults.Ok(new PaginatedResponse<AuditLogResponse>(responses, page, limit, total));
    }
}

public record AuditLogResponse(
    string Id,
    DateTime Timestamp,
    string Action,
    string ActorType,
    string? ActorId,
    string ResourceType,
    string? ResourceId,
    int StatusCode,
    string? ErrorMessage)
{
    public static AuditLogResponse FromEntity(AuditLog log) => new(
        log.Id,
        log.Timestamp,
        log.Action,
        log.ActorType,
        log.ActorId,
        log.ResourceType,
        log.ResourceId,
        log.StatusCode,
        log.ErrorMessage
    );
}
