using Microsoft.AspNetCore.Authorization;

namespace SelfMX.Api.Authentication;

/// <summary>
/// Diagnostic authorization handler that logs detailed user state during authorization.
/// This helps debug authorization issues.
/// </summary>
public class DiagnosticAuthorizationHandler : IAuthorizationHandler
{
    private readonly ILogger<DiagnosticAuthorizationHandler> _logger;

    public DiagnosticAuthorizationHandler(ILogger<DiagnosticAuthorizationHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        var user = context.User;
        var identities = user.Identities.ToList();
        var authenticatedIdentities = identities.Where(i => i.IsAuthenticated).ToList();

        _logger.LogInformation(
            "AuthZ Check: " +
            "User.Identity.IsAuthenticated={IsAuth}, " +
            "User.Identity.AuthenticationType={AuthType}, " +
            "User.Identity.Name={Name}, " +
            "IdentityCount={IdentityCount}, " +
            "AuthenticatedIdentityCount={AuthIdentityCount}",
            user.Identity?.IsAuthenticated,
            user.Identity?.AuthenticationType ?? "(null)",
            user.Identity?.Name ?? "(null)",
            identities.Count,
            authenticatedIdentities.Count);

        // Log all claims
        var claims = user.Claims.ToList();
        if (claims.Any())
        {
            _logger.LogInformation(
                "AuthZ Claims ({Count}): [{Claims}]",
                claims.Count,
                string.Join(", ", claims.Select(c => $"{c.Type}={c.Value}")));
        }
        else
        {
            _logger.LogWarning("AuthZ: No claims found on user!");
        }

        // Log each identity separately
        for (int i = 0; i < identities.Count; i++)
        {
            var identity = identities[i];
            _logger.LogInformation(
                "AuthZ Identity[{Index}]: IsAuthenticated={IsAuth}, AuthType={AuthType}, Name={Name}, ClaimCount={ClaimCount}",
                i,
                identity.IsAuthenticated,
                identity.AuthenticationType ?? "(null)",
                identity.Name ?? "(null)",
                identity.Claims.Count());
        }

        // Log pending requirements
        var pendingRequirements = context.PendingRequirements.ToList();
        if (pendingRequirements.Any())
        {
            _logger.LogInformation(
                "AuthZ Pending Requirements: [{Requirements}]",
                string.Join(", ", pendingRequirements.Select(r => r.GetType().Name)));
        }

        // Log the resource being accessed if available
        if (context.Resource != null)
        {
            _logger.LogInformation(
                "AuthZ Resource: {ResourceType}",
                context.Resource.GetType().Name);
        }

        // Don't handle any requirements - just log and continue
        return Task.CompletedTask;
    }
}
