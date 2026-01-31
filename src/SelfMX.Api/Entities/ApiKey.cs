namespace SelfMX.Api.Entities;

public class ApiKey
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string KeyHash { get; set; }      // SHA256(key + salt) base64
    public required string KeySalt { get; set; }      // Random 16-byte salt base64
    public required string KeyPrefix { get; set; }    // "re_xxxxxxxx" (first 11 chars)
    public bool IsAdmin { get; set; }                 // Admin keys have full access
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? LastUsedIp { get; set; }

    // Navigation
    public ICollection<ApiKeyDomain> AllowedDomains { get; set; } = [];
}
