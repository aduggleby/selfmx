namespace SelfMX.Api.Settings;

public class AppSettings
{
    // Legacy single API key - keep for backward compatibility during migration
    public string? ApiKeyHash { get; set; }

    // Admin password (BCrypt hash) for browser UI login
    public required string AdminPasswordHash { get; set; }

    // Session settings
    public int SessionExpirationDays { get; set; } = 30;

    // Rate limiting
    public int MaxLoginAttemptsPerMinute { get; set; } = 5;
    public int MaxApiRequestsPerMinute { get; set; } = 100;

    // Domain verification
    public TimeSpan VerificationTimeout { get; set; } = TimeSpan.FromHours(72);
    public TimeSpan VerificationPollInterval { get; set; } = TimeSpan.FromMinutes(5);
}

public class AwsSettings
{
    public required string Region { get; set; }
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
}

public class CloudflareSettings
{
    public required string ApiToken { get; set; }
    public required string ZoneId { get; set; }
}
