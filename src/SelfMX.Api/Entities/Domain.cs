namespace SelfMX.Api.Entities;

public enum DomainStatus
{
    Pending,
    Verifying,
    Verified,
    Failed
}

public class Domain
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public DomainStatus Status { get; set; } = DomainStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerificationStartedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? FailureReason { get; set; }

    // SES Identity ARN once created
    public string? SesIdentityArn { get; set; }

    // DNS records for verification (stored as JSON)
    public string? DnsRecordsJson { get; set; }
}
