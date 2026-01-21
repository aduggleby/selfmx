using System.Collections.Concurrent;
using Selfmx.Api.Contracts.Responses;

namespace Selfmx.Api.Authentication;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private readonly ConcurrentDictionary<string, RateLimitEntry> _clients = new();

    private const int RequestsPerMinute = 60;
    private const int WindowSeconds = 60;

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for health checks
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path == "/")
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var now = DateTime.UtcNow;

        var entry = _clients.AddOrUpdate(
            clientId,
            _ => new RateLimitEntry { WindowStart = now, RequestCount = 1 },
            (_, existing) =>
            {
                if (now - existing.WindowStart > TimeSpan.FromSeconds(WindowSeconds))
                {
                    return new RateLimitEntry { WindowStart = now, RequestCount = 1 };
                }

                existing.RequestCount++;
                return existing;
            });

        var remaining = Math.Max(0, RequestsPerMinute - entry.RequestCount);
        var resetTime = entry.WindowStart.AddSeconds(WindowSeconds);
        var retryAfterSeconds = (int)Math.Ceiling((resetTime - now).TotalSeconds);

        context.Response.Headers["X-RateLimit-Limit"] = RequestsPerMinute.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(resetTime).ToUnixTimeSeconds().ToString();

        if (entry.RequestCount > RequestsPerMinute)
        {
            _logger.LogWarning(
                "Rate limit exceeded for {ClientId}, requests: {Count}",
                clientId, entry.RequestCount);

            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(ApiError.RateLimited.ToResponse());
            return;
        }

        await _next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Use IP address as client identifier
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // If behind a proxy, check X-Forwarded-For
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var firstIp = forwardedFor.ToString().Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstIp))
            {
                ip = firstIp;
            }
        }

        return ip;
    }

    private class RateLimitEntry
    {
        public DateTime WindowStart { get; set; }
        public int RequestCount { get; set; }
    }
}
