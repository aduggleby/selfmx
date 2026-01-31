namespace SelfMX.Api.Entities;

public class ApiKeyDomain
{
    public required string ApiKeyId { get; set; }
    public required string DomainId { get; set; }

    // Navigation
    public ApiKey ApiKey { get; set; } = null!;
    public Domain Domain { get; set; } = null!;
}
