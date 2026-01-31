using SelfMX.Api.Entities;
using SelfMX.Api.Services;

namespace SelfMX.Api.Jobs;

public class VerifyDomainsJob
{
    private readonly DomainService _domainService;
    private readonly ISesService _sesService;
    private readonly IDnsVerificationService _dnsVerificationService;
    private readonly ILogger<VerifyDomainsJob> _logger;

    public VerifyDomainsJob(
        DomainService domainService,
        ISesService sesService,
        IDnsVerificationService dnsVerificationService,
        ILogger<VerifyDomainsJob> logger)
    {
        _domainService = domainService;
        _sesService = sesService;
        _dnsVerificationService = dnsVerificationService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting domain verification job");

        var domains = await _domainService.GetDomainsNeedingVerificationAsync();
        _logger.LogInformation("Found {Count} domains needing verification", domains.Length);

        foreach (var domain in domains)
        {
            await VerifyDomainAsync(domain);
        }

        _logger.LogInformation("Domain verification job completed");
    }

    private async Task VerifyDomainAsync(Domain domain)
    {
        _logger.LogDebug("Checking verification for domain {DomainId} ({Name})", domain.Id, domain.Name);

        // Check if timed out
        if (_domainService.IsTimedOut(domain))
        {
            _logger.LogWarning("Domain {DomainId} verification timed out", domain.Id);
            domain.Status = DomainStatus.Failed;
            domain.FailureReason = "Verification timed out after 72 hours";
            await _domainService.UpdateAsync(domain);
            return;
        }

        // Check SES DKIM status
        var sesVerified = await _sesService.CheckDkimVerificationAsync(domain.Name);
        if (sesVerified)
        {
            _logger.LogInformation("Domain {DomainId} ({Name}) verified via SES", domain.Id, domain.Name);
            domain.Status = DomainStatus.Verified;
            domain.VerifiedAt = DateTime.UtcNow;
            await _domainService.UpdateAsync(domain);
            return;
        }

        // Optionally verify DNS records directly
        var dnsRecords = domain.DnsRecordsJson?.DeserializeDnsRecords();
        if (dnsRecords != null && dnsRecords.Length > 0)
        {
            var dnsVerified = await _dnsVerificationService.VerifyAllDkimRecordsAsync(dnsRecords);
            if (dnsVerified)
            {
                _logger.LogInformation(
                    "Domain {DomainId} DNS records verified, waiting for SES propagation",
                    domain.Id);
            }
        }

        _logger.LogDebug("Domain {DomainId} not yet verified, will retry", domain.Id);
    }
}
