using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Selfmx.Api.Settings;

namespace Selfmx.Api.Authentication;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";
    private readonly AppSettings _settings;
    private readonly ILogger<ApiKeyAuthHandler> _logger;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        IOptions<AppSettings> settings)
        : base(options, loggerFactory, encoder)
    {
        _settings = settings.Value;
        _logger = loggerFactory.CreateLogger<ApiKeyAuthHandler>();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeader, out var authHeader))
        {
            _logger.LogWarning("Auth failed: missing header, IP={Ip}, Path={Path}",
                Context.Connection.RemoteIpAddress, Request.Path);
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));
        }

        var headerValue = authHeader.ToString();
        if (!headerValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Auth failed: invalid format, IP={Ip}, Path={Path}",
                Context.Connection.RemoteIpAddress, Request.Path);
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header format"));
        }

        var apiKey = headerValue[BearerPrefix.Length..];

        if (!BCrypt.Net.BCrypt.Verify(apiKey, _settings.ApiKeyHash))
        {
            _logger.LogWarning("Auth failed: invalid key, IP={Ip}, Path={Path}",
                Context.Connection.RemoteIpAddress, Request.Path);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "api-user") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
