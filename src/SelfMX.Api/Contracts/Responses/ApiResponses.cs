using SelfMX.Api.Entities;

namespace SelfMX.Api.Contracts.Responses;

using System.Text.Json.Serialization;

public record SendEmailResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object")] string Object = "email"
);

public record DnsRecordResponse(
    string Type,
    string Name,
    string Value,
    int Priority,
    bool Verified
);

public record DomainResponse(
    string Id,
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime? VerifiedAt,
    string? FailureReason,
    DnsRecordResponse[]? DnsRecords,
    DateTime? LastCheckedAt,
    DateTime? NextCheckAt
)
{
    public static DomainResponse FromEntity(Domain domain, DnsRecordResponse[]? dnsRecords = null)
    {
        // Calculate next check time based on Hangfire cron (*/5 * * * *)
        DateTime? nextCheckAt = null;
        if (domain.Status == DomainStatus.Verifying)
        {
            var now = DateTime.UtcNow;
            var minutes = now.Minute;
            var nextMinute = ((minutes / 5) + 1) * 5;
            if (nextMinute >= 60)
            {
                nextCheckAt = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc)
                    .AddHours(1);
            }
            else
            {
                nextCheckAt = new DateTime(now.Year, now.Month, now.Day, now.Hour, nextMinute, 0, DateTimeKind.Utc);
            }
        }

        return new(
            domain.Id,
            domain.Name,
            domain.Status.ToString().ToLowerInvariant(),
            domain.CreatedAt,
            domain.VerifiedAt,
            domain.FailureReason,
            dnsRecords,
            domain.LastCheckedAt,
            nextCheckAt
        );
    }
}

public record PaginatedResponse<T>(
    T[] Data,
    int Page,
    int Limit,
    int Total
);

public record ApiError(string Code, string Message)
{
    public object ToResponse() => new { error = new { code = Code, message = Message } };

    public object ToResendResponse(int statusCode)
    {
        var name = MapResendName(Code);
        return new
        {
            statusCode,
            name,
            message = Message,
            error = new
            {
                code = Code,
                message = Message
            }
        };
    }

    private static string MapResendName(string code)
    {
        return code switch
        {
            "unauthorized" => "invalid_api_key",
            "not_found" => "not_found",
            "rate_limited" => "rate_limit_exceeded",
            "invalid_request" => "validation_error",
            "domain_not_verified" => "validation_error",
            "domain_exists" => "validation_error",
            "internal_error" => "internal_server_error",
            "forbidden" => "invalid_access",
            "invalid_sender_prefix" => "validation_error",
            "invalid_recipient_email" => "validation_error",
            _ => "application_error"
        };
    }

    public static readonly ApiError Unauthorized = new("unauthorized", "Invalid or missing API key");
    public static readonly ApiError NotFound = new("not_found", "Resource not found");
    public static readonly ApiError RateLimited = new("rate_limited", "Too many requests");
    public static readonly ApiError InvalidRequest = new("invalid_request", "Invalid request body");
    public static readonly ApiError DomainNotVerified = new("domain_not_verified", "Domain is not verified for sending");
    public static readonly ApiError DomainAlreadyExists = new("domain_exists", "Domain already exists");
    public static readonly ApiError InternalError = new("internal_error", "An unexpected error occurred");
    public static readonly ApiError Forbidden = new("forbidden", "Access denied to this resource");
    public static readonly ApiError InvalidSenderPrefix = new("invalid_sender_prefix", "Sender prefix contains invalid characters");
    public static readonly ApiError InvalidRecipientEmail = new("invalid_recipient_email", "Invalid recipient email address");
}

public record HealthResponse(string Status, DateTime Timestamp);

// Sent Email responses (list excludes body, detail includes it; BCC never returned)
public record SentEmailListItem(
    string Id,
    string MessageId,
    DateTime SentAt,
    string FromAddress,
    string[] To,
    string Subject,
    string DomainId,
    string? ApiKeyId,
    string? ApiKeyName
);

public record SentEmailDetail(
    string Id,
    string MessageId,
    DateTime SentAt,
    string FromAddress,
    string[] To,
    string[]? Cc,
    // BCC intentionally excluded for privacy
    string? ReplyTo,
    string Subject,
    string? HtmlBody,
    string? TextBody,
    string DomainId,
    string? ApiKeyId,
    string? ApiKeyName
);

public record CursorPagedResponse<T>(
    T[] Data,
    string? NextCursor,
    bool HasMore
);
