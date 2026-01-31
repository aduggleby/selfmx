namespace SelfMX.Api.Entities;

public class SentEmail
{
    public required string Id { get; set; }           // Internal GUID
    public required string MessageId { get; set; }    // SES MessageId for correlation
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public required string FromAddress { get; set; }
    public required string ToAddresses { get; set; }  // JSON array
    public string? CcAddresses { get; set; }          // JSON array
    public string? BccAddresses { get; set; }         // JSON array (NOT returned in API)
    public string? ReplyTo { get; set; }
    public required string Subject { get; set; }
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public required string DomainId { get; set; }
    public string? ApiKeyId { get; set; }             // Nullable for admin sends

    // Navigation
    public Domain Domain { get; set; } = null!;
}
