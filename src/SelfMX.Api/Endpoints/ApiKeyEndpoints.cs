using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using SelfMX.Api.Contracts.Responses;
using SelfMX.Api.Entities;
using SelfMX.Api.Services;

namespace SelfMX.Api.Endpoints;

public static class ApiKeyEndpoints
{
    public static RouteGroupBuilder MapApiKeyEndpoints(this RouteGroupBuilder group)
    {
        var keys = group.MapGroup("/api-keys");

        keys.MapGet("/", ListApiKeys);
        keys.MapPost("/", CreateApiKey);
        keys.MapGet("/{id}", GetApiKey);
        keys.MapDelete("/{id}", RevokeApiKey);

        return group;
    }

    private static async Task<Ok<PaginatedResponse<ApiKeyResponse>>> ListApiKeys(
        ApiKeyService apiKeyService,
        int page = 1,
        int limit = 20,
        CancellationToken ct = default)
    {
        var (items, total) = await apiKeyService.ListAsync(page, limit, ct);
        var responses = items.Select(ApiKeyResponse.FromEntity).ToArray();
        return TypedResults.Ok(new PaginatedResponse<ApiKeyResponse>(responses, page, limit, total));
    }

    private static async Task<Results<Created<ApiKeyCreatedResponse>, BadRequest<object>>> CreateApiKey(
        CreateApiKeyRequest request,
        ApiKeyService apiKeyService,
        AuditService auditService,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return TypedResults.BadRequest(ApiError.InvalidRequest.ToResponse());

        // Admin keys don't need domains, regular keys do
        if (!request.IsAdmin && (request.DomainIds is null || request.DomainIds.Length == 0))
            return TypedResults.BadRequest(new ApiError("invalid_request", "Non-admin keys must have at least one domain").ToResponse());

        var (key, plainTextKey) = await apiKeyService.CreateAsync(
            request.Name,
            request.DomainIds ?? [],
            request.IsAdmin,
            ct);

        auditService.Log(new AuditEntry(
            Action: "api_key.create",
            ActorType: user.FindFirst("ActorType")?.Value ?? "admin",
            ActorId: user.FindFirst("KeyPrefix")?.Value,
            ResourceType: "api_key",
            ResourceId: key.Id,
            StatusCode: 201,
            Details: new { key.Name, key.IsAdmin, DomainCount = request.DomainIds?.Length ?? 0 }
        ));

        // Return the plain text key ONCE - it cannot be retrieved again
        return TypedResults.Created(
            $"/v1/api-keys/{key.Id}",
            new ApiKeyCreatedResponse(key.Id, key.Name, plainTextKey, key.KeyPrefix, key.IsAdmin, key.CreatedAt)
        );
    }

    private static async Task<Results<Ok<ApiKeyResponse>, NotFound<object>>> GetApiKey(
        string id,
        ApiKeyService apiKeyService,
        CancellationToken ct = default)
    {
        var key = await apiKeyService.GetByIdAsync(id, ct);
        if (key is null)
            return TypedResults.NotFound(ApiError.NotFound.ToResponse());

        return TypedResults.Ok(ApiKeyResponse.FromEntity(key));
    }

    private static async Task<Results<Ok, NotFound<object>>> RevokeApiKey(
        string id,
        ApiKeyService apiKeyService,
        AuditService auditService,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        var key = await apiKeyService.GetByIdAsync(id, ct);
        if (key is null)
            return TypedResults.NotFound(ApiError.NotFound.ToResponse());

        await apiKeyService.RevokeAsync(id, ct);

        auditService.Log(new AuditEntry(
            Action: "api_key.revoke",
            ActorType: user.FindFirst("ActorType")?.Value ?? "admin",
            ActorId: user.FindFirst("KeyPrefix")?.Value,
            ResourceType: "api_key",
            ResourceId: id,
            StatusCode: 200
        ));

        return TypedResults.Ok();
    }
}

public record CreateApiKeyRequest(string Name, string[]? DomainIds, bool IsAdmin = false);

public record ApiKeyResponse(
    string Id,
    string Name,
    string KeyPrefix,
    bool IsAdmin,
    DateTime CreatedAt,
    DateTime? RevokedAt,
    DateTime? LastUsedAt,
    string[] DomainIds)
{
    public static ApiKeyResponse FromEntity(ApiKey key) => new(
        key.Id,
        key.Name,
        key.KeyPrefix,
        key.IsAdmin,
        key.CreatedAt,
        key.RevokedAt,
        key.LastUsedAt,
        key.AllowedDomains.Select(d => d.DomainId).ToArray()
    );
}

public record ApiKeyCreatedResponse(
    string Id,
    string Name,
    string Key,
    string KeyPrefix,
    bool IsAdmin,
    DateTime CreatedAt);
