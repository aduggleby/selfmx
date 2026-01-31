namespace SelfMX.Api.Entities;

public class AuditLog
{
    public required string Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public required string Action { get; set; }       // "email.send", "api_key.create", etc.
    public required string ActorType { get; set; }    // "api_key", "admin", "system"
    public string? ActorId { get; set; }              // Key prefix or null for admin
    public required string ResourceType { get; set; } // "email", "domain", "api_key"
    public string? ResourceId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Details { get; set; }              // JSON blob
}
