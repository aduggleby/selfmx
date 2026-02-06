namespace SelfMX.Api.Entities;

/// <summary>
/// Archived record of a revoked API key.
/// Keys are moved here 90 days after revocation, then the original is deleted.
/// </summary>
public class RevokedApiKey
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string KeyPrefix { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime RevokedAt { get; set; }
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    public string? LastUsedIp { get; set; }
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Comma-separated list of domain IDs this key had access to.
    /// </summary>
    public string? AllowedDomainIds { get; set; }
}
