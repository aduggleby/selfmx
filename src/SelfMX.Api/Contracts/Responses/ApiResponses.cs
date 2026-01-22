using SelfMX.Api.Entities;

namespace SelfMX.Api.Contracts.Responses;

public record SendEmailResponse(string Id);

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
    DnsRecordResponse[]? DnsRecords
)
{
    public static DomainResponse FromEntity(Domain domain, DnsRecordResponse[]? dnsRecords = null) =>
        new(
            domain.Id,
            domain.Name,
            domain.Status.ToString().ToLowerInvariant(),
            domain.CreatedAt,
            domain.VerifiedAt,
            domain.FailureReason,
            dnsRecords
        );
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

    public static readonly ApiError Unauthorized = new("unauthorized", "Invalid or missing API key");
    public static readonly ApiError NotFound = new("not_found", "Resource not found");
    public static readonly ApiError RateLimited = new("rate_limited", "Too many requests");
    public static readonly ApiError InvalidRequest = new("invalid_request", "Invalid request body");
    public static readonly ApiError DomainNotVerified = new("domain_not_verified", "Domain is not verified for sending");
    public static readonly ApiError DomainAlreadyExists = new("domain_exists", "Domain already exists");
    public static readonly ApiError InternalError = new("internal_error", "An unexpected error occurred");
}

public record HealthResponse(string Status, DateTime Timestamp);
