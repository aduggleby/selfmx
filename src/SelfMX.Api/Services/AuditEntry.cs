namespace SelfMX.Api.Services;

public record AuditEntry(
    string Action,
    string ActorType,
    string? ActorId,
    string ResourceType,
    string? ResourceId,
    int StatusCode,
    string? ErrorMessage = null,
    object? Details = null
);
