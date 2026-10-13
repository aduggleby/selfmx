using Microsoft.EntityFrameworkCore;
using SelfMX.Api.Data;

namespace SelfMX.Api.Services;

/// <summary>Emails sent by one API key. A window is null when it is longer than the sent-email retention.</summary>
public record ApiKeyStatsResponse(string ApiKeyId, int Last24h, int? Last7d, int? Last30d, int? Last90d);

public static class ApiKeySendStats
{
    /// <summary>
    /// Counts sent emails per API key for the last 24 hours, 7, 30 and 90 days in one grouped query.
    /// The (SentAt, ApiKeyId) index makes this an index-only range scan over the last 90 days.
    /// Keys without sends in the last 90 days are omitted.
    /// </summary>
    public static async Task<ApiKeyStatsResponse[]> GetAsync(
        AppDbContext db, int? retentionDays, DateTime nowUtc, CancellationToken ct = default)
    {
        var since24h = nowUtc.AddHours(-24);
        var since7d = nowUtc.AddDays(-7);
        var since30d = nowUtc.AddDays(-30);
        var since90d = nowUtc.AddDays(-90);

        var counts = await db.SentEmails
            .AsNoTracking()
            .Where(e => e.SentAt >= since90d && e.ApiKeyId != null)
            .GroupBy(e => e.ApiKeyId!)
            .Select(g => new
            {
                ApiKeyId = g.Key,
                Last24h = g.Count(e => e.SentAt >= since24h),
                Last7d = g.Count(e => e.SentAt >= since7d),
                Last30d = g.Count(e => e.SentAt >= since30d),
                Last90d = g.Count()
            })
            .ToArrayAsync(ct);

        // Older sends are deleted by the retention cleanup, so longer windows would undercount
        int? Window(int days, int count) => retentionDays is > 0 && retentionDays < days ? null : count;

        return counts
            .Select(c => new ApiKeyStatsResponse(c.ApiKeyId, c.Last24h, Window(7, c.Last7d), Window(30, c.Last30d), Window(90, c.Last90d)))
            .ToArray();
    }
}
