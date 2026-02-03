using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SelfMX.Api.Contracts.Responses;
using SelfMX.Api.Data;
using SelfMX.Api.Services;

namespace SelfMX.Api.Endpoints;

public static class SentEmailEndpoints
{
    public static RouteGroupBuilder MapSentEmailEndpoints(this RouteGroupBuilder group)
    {
        var sentEmails = group.MapGroup("/sent-emails");

        sentEmails.MapGet("/", ListSentEmails);
        sentEmails.MapGet("/{id}", GetSentEmail);

        return group;
    }

    private static async Task<Results<Ok<CursorPagedResponse<SentEmailListItem>>, BadRequest<object>>> ListSentEmails(
        AppDbContext db,
        ApiKeyService apiKeyService,
        ClaimsPrincipal user,
        string? domainId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? cursor = null,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var allowedDomains = user.FindAll("AllowedDomain").Select(c => c.Value).ToList();
        var isAdmin = user.FindFirst("ActorType")?.Value == "admin";

        var query = db.SentEmails.AsNoTracking();

        // Authorization filter
        if (!isAdmin)
            query = query.Where(e => allowedDomains.Contains(e.DomainId));

        // Domain filter
        if (!string.IsNullOrEmpty(domainId))
        {
            if (!isAdmin && !allowedDomains.Contains(domainId))
                return TypedResults.BadRequest(ApiError.Unauthorized.ToResponse());

            query = query.Where(e => e.DomainId == domainId);
        }

        // Date range filters
        if (from.HasValue)
            query = query.Where(e => e.SentAt >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.SentAt <= to.Value);

        // Keyset pagination
        if (!string.IsNullOrEmpty(cursor))
        {
            var c = DecodeCursor(cursor);
            if (c != null)
            {
                query = query.Where(e =>
                    e.SentAt < c.SentAt ||
                    (e.SentAt == c.SentAt && e.Id.CompareTo(c.Id) < 0));
            }
        }

        // Explicit projection - never fetch body columns
        var rawItems = await query
            .OrderByDescending(e => e.SentAt)
            .ThenByDescending(e => e.Id)
            .Take(pageSize + 1)
            .Select(e => new
            {
                e.Id,
                e.MessageId,
                e.SentAt,
                e.FromAddress,
                e.ToAddresses,
                e.Subject,
                e.DomainId,
                e.ApiKeyId
            })
            .ToListAsync(ct);

        // Batch fetch API key names for items that have an ApiKeyId
        var apiKeyIds = rawItems.Where(e => e.ApiKeyId != null).Select(e => e.ApiKeyId!).Distinct().ToList();
        var apiKeyNames = apiKeyIds.Count > 0
            ? await db.ApiKeys
                .Where(k => apiKeyIds.Contains(k.Id))
                .Select(k => new { k.Id, k.Name })
                .ToDictionaryAsync(k => k.Id, k => k.Name, ct)
            : new Dictionary<string, string>();

        // Deserialize JSON after query execution
        var items = rawItems.Select(e => new SentEmailListItem(
            e.Id,
            e.MessageId,
            e.SentAt,
            e.FromAddress,
            JsonSerializer.Deserialize<string[]>(e.ToAddresses) ?? Array.Empty<string>(),
            e.Subject,
            e.DomainId,
            e.ApiKeyId,
            e.ApiKeyId != null && apiKeyNames.TryGetValue(e.ApiKeyId, out var name) ? name : null
        )).ToList();

        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);

        var lastItem = items.LastOrDefault();
        var nextCursor = hasMore && lastItem != null
            ? EncodeCursor(lastItem.Id, lastItem.SentAt)
            : null;

        return TypedResults.Ok(new CursorPagedResponse<SentEmailListItem>(
            items.ToArray(), nextCursor, hasMore));
    }

    private static async Task<Results<Ok<SentEmailDetail>, NotFound<object>, ForbidHttpResult>> GetSentEmail(
        string id,
        AppDbContext db,
        ApiKeyService apiKeyService,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        var email = await db.SentEmails
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.Id,
                e.MessageId,
                e.SentAt,
                e.FromAddress,
                e.ToAddresses,
                e.CcAddresses,
                // BCC intentionally excluded
                e.ReplyTo,
                e.Subject,
                e.HtmlBody,
                e.TextBody,
                e.DomainId,
                e.ApiKeyId
            })
            .FirstOrDefaultAsync(ct);

        if (email is null)
            return TypedResults.NotFound(ApiError.NotFound.ToResponse());

        // Authorization check
        if (!apiKeyService.CanAccessDomain(user, email.DomainId))
            return TypedResults.Forbid();

        // Look up API key name if present
        string? apiKeyName = null;
        if (email.ApiKeyId != null)
        {
            apiKeyName = await db.ApiKeys
                .Where(k => k.Id == email.ApiKeyId)
                .Select(k => k.Name)
                .FirstOrDefaultAsync(ct);
        }

        var detail = new SentEmailDetail(
            email.Id,
            email.MessageId,
            email.SentAt,
            email.FromAddress,
            JsonSerializer.Deserialize<string[]>(email.ToAddresses)!,
            email.CcAddresses != null ? JsonSerializer.Deserialize<string[]>(email.CcAddresses) : null,
            email.ReplyTo,
            email.Subject,
            email.HtmlBody,
            email.TextBody,
            email.DomainId,
            email.ApiKeyId,
            apiKeyName
        );

        return TypedResults.Ok(detail);
    }

    private record CursorData(string Id, DateTime SentAt);

    private static string EncodeCursor(string id, DateTime sentAt)
    {
        var data = JsonSerializer.Serialize(new CursorData(id, sentAt));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
    }

    private static CursorData? DecodeCursor(string cursor)
    {
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<CursorData>(json);
        }
        catch
        {
            return null;
        }
    }
}
