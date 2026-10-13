using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace SelfMX.Api.Endpoints;

public static class TokenEndpoints
{
    public static RouteGroupBuilder MapTokenEndpoints(this RouteGroupBuilder group)
    {
        var tokens = group.MapGroup("/tokens");

        // Returns effective permissions for the caller (cookie admin or API key).
        tokens.MapGet("/me", GetMe);

        return group;
    }

    private static Ok<TokenInfoResponse> GetMe(ClaimsPrincipal user)
    {
        var actorType = user.FindFirst("ActorType")?.Value ?? "unknown";
        var isAdmin = actorType == "admin";
        var name = user.Identity?.Name;

        var keyId = user.FindFirst("KeyId")?.Value;
        var keyPrefix = user.FindFirst("KeyPrefix")?.Value;
        var allowedDomainIds = user.FindAll("AllowedDomain").Select(c => c.Value).Distinct().ToArray();

        return TypedResults.Ok(new TokenInfoResponse(
            Authenticated: true,
            ActorType: actorType,
            IsAdmin: isAdmin,
            Name: name,
            KeyId: keyId,
            KeyPrefix: keyPrefix,
            AllowedDomainIds: allowedDomainIds
        ));
    }
}

public record TokenInfoResponse(
    bool Authenticated,
    string ActorType,
    bool IsAdmin,
    string? Name,
    string? KeyId,
    string? KeyPrefix,
    string[] AllowedDomainIds
);

