using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using SelfMX.Api.Authentication;
using SelfMX.Api.Services;
using SelfMX.Api.Settings;

namespace SelfMX.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin");

        admin.MapPost("/login", Login).AllowAnonymous();
        admin.MapPost("/logout", Logout);
        admin.MapGet("/me", GetCurrentAdmin);

        return group;
    }

    private static async Task<Results<Ok<AdminInfoResponse>, UnauthorizedHttpResult>> Login(
        LoginRequest request,
        IOptions<AppSettings> settings,
        AuditService auditService,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("AdminEndpoints");
        var storedHash = settings.Value.AdminPasswordHash;
        var passwordProvided = !string.IsNullOrWhiteSpace(request.Password);
        var passwordLength = request.Password?.Length ?? 0;

        logger.LogInformation(
            "Login attempt - Password provided: {Provided}, Length: {Length}, Hash starts with: {HashPrefix}, Hash length: {HashLength}",
            passwordProvided,
            passwordLength,
            storedHash?.Length > 20 ? storedHash[..20] + "..." : storedHash,
            storedHash?.Length ?? 0);

        var verifyResult = passwordProvided && Sha512CryptVerifier.Verify(request.Password!, storedHash);
        logger.LogInformation("Verification result: {Result}", verifyResult);

        // Extra debug: compute what the hash would be
        if (passwordProvided && !string.IsNullOrEmpty(storedHash))
        {
            var computed = Sha512CryptVerifier.ComputeForDebug(request.Password!, storedHash);
            logger.LogInformation("Computed hash: {Computed}", computed);
            logger.LogInformation("Stored hash:   {Stored}", storedHash);
        }

        if (!verifyResult)
        {
            auditService.Log(new AuditEntry(
                Action: "admin.login",
                ActorType: "admin",
                ActorId: null,
                ResourceType: "session",
                ResourceId: null,
                StatusCode: 401,
                ErrorMessage: "Invalid password"
            ));

            return TypedResults.Unauthorized();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "admin"),
            new("ActorType", "admin"),
        };

        var identity = new ClaimsIdentity(claims, "Cookie");
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync("Cookie", principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(settings.Value.SessionExpirationDays)
        });

        auditService.Log(new AuditEntry(
            Action: "admin.login",
            ActorType: "admin",
            ActorId: null,
            ResourceType: "session",
            ResourceId: null,
            StatusCode: 200
        ));

        return TypedResults.Ok(new AdminInfoResponse("admin", true));
    }

    private static async Task<Ok> Logout(
        AuditService auditService,
        HttpContext context)
    {
        await context.SignOutAsync("Cookie");

        auditService.Log(new AuditEntry(
            Action: "admin.logout",
            ActorType: "admin",
            ActorId: null,
            ResourceType: "session",
            ResourceId: null,
            StatusCode: 200
        ));

        return TypedResults.Ok();
    }

    private static Results<Ok<AdminInfoResponse>, UnauthorizedHttpResult> GetCurrentAdmin(
        ClaimsPrincipal user)
    {
        var actorType = user.FindFirst("ActorType")?.Value;
        if (actorType != "admin")
            return TypedResults.Unauthorized();

        return TypedResults.Ok(new AdminInfoResponse(
            user.Identity?.Name ?? "admin",
            true
        ));
    }
}

public record LoginRequest(string Password);
public record AdminInfoResponse(string Name, bool IsAuthenticated);
