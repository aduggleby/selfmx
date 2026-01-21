using Selfmx.Api.Entities;
using Selfmx.Api.Services;

namespace Selfmx.Api.Jobs;

public class SetupDomainJob
{
    private readonly DomainService _domainService;
    private readonly ISesService _sesService;
    private readonly ICloudflareService _cloudflareService;
    private readonly ILogger<SetupDomainJob> _logger;

    public SetupDomainJob(
        DomainService domainService,
        ISesService sesService,
        ICloudflareService cloudflareService,
        ILogger<SetupDomainJob> logger)
    {
        _domainService = domainService;
        _sesService = sesService;
        _cloudflareService = cloudflareService;
        _logger = logger;
    }

    public async Task ExecuteAsync(string domainId)
    {
        _logger.LogInformation("Setting up domain {DomainId}", domainId);

        var domain = await _domainService.GetByIdAsync(domainId);
        if (domain is null)
        {
            _logger.LogWarning("Domain {DomainId} not found", domainId);
            return;
        }

        if (domain.Status != DomainStatus.Pending)
        {
            _logger.LogInformation("Domain {DomainId} is not in Pending status, skipping setup", domainId);
            return;
        }

        try
        {
            // Create SES domain identity
            var (identityArn, dnsRecords) = await _sesService.CreateDomainIdentityAsync(domain.Name);

            // Create DNS records in Cloudflare
            foreach (var record in dnsRecords)
            {
                try
                {
                    await _cloudflareService.CreateDnsRecordAsync(
                        record.Type,
                        record.Name,
                        record.Value,
                        record.Priority,
                        proxied: false);
                }
                catch (CloudflareException ex)
                {
                    _logger.LogWarning(ex, "Failed to create DNS record {Name}, continuing...", record.Name);
                }
            }

            // Update domain with SES info
            domain.SesIdentityArn = identityArn;
            domain.DnsRecordsJson = dnsRecords.SerializeDnsRecords();
            domain.Status = DomainStatus.Verifying;
            domain.VerificationStartedAt = DateTime.UtcNow;

            await _domainService.UpdateAsync(domain);

            _logger.LogInformation(
                "Domain {DomainId} SES identity created, DNS records created, status set to Verifying",
                domainId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set up domain {DomainId}", domainId);

            domain.Status = DomainStatus.Failed;
            domain.FailureReason = $"Setup failed: {ex.Message}";
            await _domainService.UpdateAsync(domain);
        }
    }
}
