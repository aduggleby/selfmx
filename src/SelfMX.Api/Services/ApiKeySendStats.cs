using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;

namespace SelfMX.Api.Services;

/// <summary>Emails sent by one API key. A window is null when it is longer than the sent-email retention.</summary>
/// Daily30d: sends per UTC day for the last 30 days, oldest first (today last); null when the
/// retention is shorter than 30 days.
public record ApiKeyStatsResponse(string ApiKeyId, int Last24h, int? Last7d, int? Last30d, int? Last90d, int[]? Daily30d);

/// <summary>
/// Emails sent from one domain (any key or the admin UI) and the active API keys scoped to it.
/// A window is null when it is longer than the sent-email retention.
/// </summary>
public record DomainStatsResponse(
    string DomainId, int ActiveApiKeys, int Last24h, int? Last7d, int? Last30d, int? Last90d, int[]? Daily30d);

public static class ApiKeySendStats
{
    /// <summary>
    /// Counts sent emails per API key for the last 24 hours, 7, 30 and 90 days in one grouped query.
    /// Keys without sends in the last 90 days are omitted.
    /// </summary>
    public static async Task<ApiKeyStatsResponse[]> GetAsync(
        AppDbContext db, int? retentionDays, DateTime nowUtc, CancellationToken ct = default)
    {
        var source = db.SentEmails.Where(e => e.ApiKeyId != null);
        var counts = await CountAsync(source, e => e.ApiKeyId!, nowUtc, ct);
        var daily = await DailyAsync(source.Select(e => new KeyedSend { Key = e.ApiKeyId!, SentAt = e.SentAt }), retentionDays, nowUtc, ct);
        return counts
            .Select(c => new ApiKeyStatsResponse(c.Key, c.Last24h,
                Window(7, c.Last7d, retentionDays), Window(30, c.Last30d, retentionDays), Window(90, c.Last90d, retentionDays),
                Series(daily, c.Key, retentionDays)))
            .ToArray();
    }

    /// <summary>
    /// Per domain: emails sent in the same windows plus active (non-revoked) API keys scoped to it.
    /// Admin keys can use every domain and are not counted. Domains without sends or keys are omitted.
    /// </summary>
    public static async Task<DomainStatsResponse[]> GetForDomainsAsync(
        AppDbContext db, int? retentionDays, DateTime nowUtc, CancellationToken ct = default)
    {
        var counts = (await CountAsync(db.SentEmails, e => e.DomainId, nowUtc, ct)).ToDictionary(c => c.Key);
        var daily = await DailyAsync(db.SentEmails.Select(e => new KeyedSend { Key = e.DomainId, SentAt = e.SentAt }), retentionDays, nowUtc, ct);
        var activeKeys = await db.ApiKeyDomains
            .Where(x => x.ApiKey.RevokedAt == null)
            .GroupBy(x => x.DomainId)
            .Select(g => new { DomainId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DomainId, x => x.Count, ct);

        return counts.Keys.Union(activeKeys.Keys)
            .Select(id =>
            {
                counts.TryGetValue(id, out var c);
                return new DomainStatsResponse(id, activeKeys.GetValueOrDefault(id), c?.Last24h ?? 0,
                    Window(7, c?.Last7d ?? 0, retentionDays), Window(30, c?.Last30d ?? 0, retentionDays),
                    Window(90, c?.Last90d ?? 0, retentionDays), Series(daily, id, retentionDays));
            })
            .ToArray();
    }

    private sealed class KeyedSend
    {
        public required string Key { get; init; }
        public DateTime SentAt { get; init; }
    }

    private const int DailyDays = 30;

    /// <summary>Sends per key per UTC calendar day over the last 30 days (today included), one grouped query.</summary>
    private static async Task<Dictionary<string, int[]>?> DailyAsync(
        IQueryable<KeyedSend> source, int? retentionDays, DateTime nowUtc, CancellationToken ct)
    {
        if (retentionDays is > 0 && retentionDays < DailyDays)
            return null;

        var firstDay = nowUtc.Date.AddDays(-(DailyDays - 1));
        var rows = await source
            .AsNoTracking()
            .Where(x => x.SentAt >= firstDay)
            .GroupBy(x => new { x.Key, Day = x.SentAt.Date })
            .Select(g => new { g.Key.Key, g.Key.Day, Count = g.Count() })
            .ToListAsync(ct);

        var result = new Dictionary<string, int[]>();
        foreach (var row in rows)
        {
            var index = (int)(DateTime.SpecifyKind(row.Day, DateTimeKind.Utc) - firstDay).TotalDays;
            if (index is < 0 or >= DailyDays) continue;
            if (!result.TryGetValue(row.Key, out var series))
                result[row.Key] = series = new int[DailyDays];
            series[index] += row.Count;
        }
        return result;
    }

    private static int[]? Series(Dictionary<string, int[]>? daily, string key, int? retentionDays) =>
        daily is null ? null : daily.TryGetValue(key, out var series) ? series : new int[DailyDays];

    private sealed record WindowCounts(string Key, int Last24h, int Last7d, int Last30d, int Last90d);

    // One grouped query over the last 90 days. The (SentAt, ApiKeyId, DomainId) index makes it an
    // index-only range scan for both groupings.
    private static async Task<WindowCounts[]> CountAsync(
        IQueryable<SentEmail> source, Expression<Func<SentEmail, string>> key, DateTime nowUtc, CancellationToken ct)
    {
        var since24h = nowUtc.AddHours(-24);
        var since7d = nowUtc.AddDays(-7);
        var since30d = nowUtc.AddDays(-30);
        var since90d = nowUtc.AddDays(-90);

        return await source
            .AsNoTracking()
            .Where(e => e.SentAt >= since90d)
            .GroupBy(key)
            .Select(g => new WindowCounts(
                g.Key,
                g.Count(e => e.SentAt >= since24h),
                g.Count(e => e.SentAt >= since7d),
                g.Count(e => e.SentAt >= since30d),
                g.Count()))
            .ToArrayAsync(ct);
    }

    // Older sends are deleted by the retention cleanup, so longer windows would undercount
    private static int? Window(int days, int count, int? retentionDays) =>
        retentionDays is > 0 && retentionDays < days ? null : count;
}
