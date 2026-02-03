using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SelfMX.Api.Services;
using SelfMX.Api.Settings;

namespace SelfMX.Api.Authentication;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";
    private readonly AppSettings _settings;
    private readonly ApiKeyService _apiKeyService;
    private readonly ILogger<ApiKeyAuthHandler> _logger;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        IOptions<AppSettings> settings,
        ApiKeyService apiKeyService)
        : base(options, loggerFactory, encoder)
    {
        _settings = settings.Value;
        _apiKeyService = apiKeyService;
        _logger = loggerFactory.CreateLogger<ApiKeyAuthHandler>();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // First, check if there's a valid Cookie authentication
        var cookieResult = await Context.AuthenticateAsync("Cookie");
        if (cookieResult.Succeeded)
        {
            _logger.LogInformation("Auth: Cookie auth succeeded for user {User}, Path={Path}",
                cookieResult.Principal?.Identity?.Name, Request.Path);
            return AuthenticateResult.Success(cookieResult.Ticket!);
        }

        if (cookieResult.Failure != null)
        {
            _logger.LogDebug("Auth: Cookie auth failed: {Error}, Path={Path}",
                cookieResult.Failure.Message, Request.Path);
        }

        // No cookie, try API key
        if (!Request.Headers.TryGetValue(ApiKeyHeader, out var authHeader))
        {
            _logger.LogDebug("Auth: no Authorization header and no valid cookie, IP={Ip}, Path={Path}",
                Context.Connection.RemoteIpAddress, Request.Path);
            return AuthenticateResult.NoResult();
        }

        var headerValue = authHeader.ToString();
        if (!headerValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Auth failed: invalid format, IP={Ip}, Path={Path}",
                Context.Connection.RemoteIpAddress, Request.Path);
            return AuthenticateResult.Fail("Invalid Authorization header format");
        }

        var apiKey = headerValue[BearerPrefix.Length..];
        var ipAddress = Context.Connection.RemoteIpAddress?.ToString();

        // Try multi-key validation first (new system)
        if (apiKey.StartsWith("re_"))
        {
            var validatedKey = await _apiKeyService.ValidateAsync(apiKey, ipAddress, Context.RequestAborted);
            if (validatedKey is not null)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, validatedKey.Name),
                    new("ActorType", validatedKey.IsAdmin ? "admin" : "api_key"),
                    new("KeyPrefix", validatedKey.KeyPrefix),
                    new("KeyId", validatedKey.Id)
                };

                // Add allowed domains as claims (for non-admin keys)
                if (!validatedKey.IsAdmin)
                {
                    _logger.LogInformation("Auth: Non-admin key {Prefix}, AllowedDomains count: {Count}, DomainIds: [{Domains}]",
                        validatedKey.KeyPrefix,
                        validatedKey.AllowedDomains.Count,
                        string.Join(", ", validatedKey.AllowedDomains.Select(d => d.DomainId)));

                    foreach (var domain in validatedKey.AllowedDomains)
                    {
                        claims.Add(new Claim("AllowedDomain", domain.DomainId));
                    }
                }

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                _logger.LogInformation("Auth: API key validated - Scheme={Scheme}, IsAuthenticated={IsAuth}, Identity.AuthType={AuthType}, ClaimsCount={Count}",
                    Scheme.Name, identity.IsAuthenticated, identity.AuthenticationType, claims.Count);

                return AuthenticateResult.Success(ticket);
            }
        }

        // Fallback to legacy single-key mode (backward compatibility)
        if (!string.IsNullOrEmpty(_settings.ApiKeyHash))
        {
            if (Sha512CryptVerifier.Verify(apiKey, _settings.ApiKeyHash))
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, "legacy-api-user"),
                    new("ActorType", "admin") // Legacy keys have full access
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }
        }

        _logger.LogWarning("Auth failed: invalid key, IP={Ip}, Path={Path}",
            ipAddress, Request.Path);
        return AuthenticateResult.Fail("Invalid API key");
    }
}
