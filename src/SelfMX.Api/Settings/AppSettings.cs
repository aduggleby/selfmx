namespace SelfMX.Api.Settings;

public class AppSettings
{
    public required string ApiKeyHash { get; set; }
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
