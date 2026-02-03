using Hangfire.Server;
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

    public async Task ExecuteAsync(PerformContext? context)
    {
        var console = new JobConsole(_logger, context);

        console.WriteLine("========================================");
        console.WriteInfo("VerifyDomainsJob started");
        console.WriteLine($"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        console.WriteLine("========================================");

        var domains = await _domainService.GetDomainsNeedingVerificationAsync();
        console.WriteLine($"Found {domains.Length} domain(s) needing verification");

        if (domains.Length == 0)
        {
            console.WriteSuccess("No domains to verify - job completed");
            return;
        }

        var progress = console.WriteProgressBar();
        var current = 0;

        foreach (var domain in domains)
        {
            console.WriteLine("");
            console.WriteLine("----------------------------------------");
            console.WriteInfo($"Verifying: {domain.Name}");
            console.WriteLine("----------------------------------------");
            await VerifyDomainAsync(domain, console);

            current++;
            progress?.SetValue((current * 100) / domains.Length);
        }

        console.WriteLine("");
        console.WriteLine("========================================");
        console.WriteSuccess("VerifyDomainsJob completed");
        console.WriteLine($"Domains processed: {domains.Length}");
        console.WriteLine("========================================");
    }

    private async Task VerifyDomainAsync(Domain domain, JobConsole console)
    {
        console.WriteLine($"  Domain ID: {domain.Id}");
        console.WriteLine($"  Current status: {domain.Status}");
        console.WriteLine($"  Verification started: {domain.VerificationStartedAt:yyyy-MM-dd HH:mm:ss} UTC");
        if (domain.LastCheckedAt.HasValue)
        {
            console.WriteLine($"  Last checked: {domain.LastCheckedAt:yyyy-MM-dd HH:mm:ss} UTC");
        }

        // Update last checked timestamp
        domain.LastCheckedAt = DateTime.UtcNow;

        // Check if timed out
        console.WriteLine("");
        console.WriteLine("  Step 1: Checking for timeout...");
        if (_domainService.IsTimedOut(domain))
        {
            console.WriteError("  Verification TIMED OUT after 72 hours!");
            domain.Status = DomainStatus.Failed;
            domain.FailureReason = "Verification timed out after 72 hours";
            await _domainService.UpdateAsync(domain);
            return;
        }
        console.WriteSuccess("  Timeout check passed (within 72 hours)");

        // Check SES DKIM status (AWS-side verification)
        console.WriteLine("");
        console.WriteInfo("  Step 2: Checking AWS SES DKIM status...");
        console.WriteLine("  (This is AWS-side verification - checking if AWS has detected your DNS records)");

        var sesDetails = await _sesService.GetDkimVerificationDetailsAsync(domain.Name);

        console.WriteLine($"    DKIM Status: {sesDetails.Status}");
        console.WriteLine($"    Is Verified: {sesDetails.IsVerified}");
        console.WriteLine($"    Signing Origin: {sesDetails.SigningAttributesOrigin}");
        console.WriteLine($"    Key Length: {sesDetails.CurrentSigningKeyLength}");
        if (sesDetails.LastKeyGenerationTimestamp.HasValue)
        {
            console.WriteLine($"    Key Generated: {sesDetails.LastKeyGenerationTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
        }
        if (sesDetails.Tokens != null && sesDetails.Tokens.Length > 0)
        {
            console.WriteLine($"    DKIM Tokens: {string.Join(", ", sesDetails.Tokens)}");
        }

        if (sesDetails.IsVerified)
        {
            console.WriteLine("");
            console.WriteSuccess("  AWS SES verification SUCCESSFUL!");
            console.WriteSuccess($"  Domain {domain.Name} is now VERIFIED");
            domain.Status = DomainStatus.Verified;
            domain.VerifiedAt = DateTime.UtcNow;
            await _domainService.UpdateAsync(domain);
            return;
        }

        console.WriteLine($"    AWS SES not yet verified (status: {sesDetails.Status})");

        // Verify DNS records directly (our-side verification)
        console.WriteLine("");
        console.WriteInfo("  Step 3: Verifying DNS records directly...");
        console.WriteLine("  (This is our-side verification - checking if DNS records are publicly visible)");

        var dnsRecords = domain.DnsRecordsJson?.DeserializeDnsRecords();
        if (dnsRecords == null || dnsRecords.Length == 0)
        {
            console.WriteWarning("  No DNS records stored for this domain!");
        }
        else
        {
            var cnameCount = dnsRecords.Count(r => r.Type == "CNAME");
            var txtCount = dnsRecords.Count(r => r.Type == "TXT");
            console.WriteLine($"  Checking {cnameCount} CNAME records (DKIM) and {txtCount} TXT records (SPF/DMARC):");
            var dnsResult = await _dnsVerificationService.VerifyAllRecordsDetailedAsync(dnsRecords);

            foreach (var record in dnsResult.Records)
            {
                console.WriteLine("");
                console.WriteLine($"    Record: {record.RecordName}");
                console.WriteLine($"      Expected: {record.ExpectedValue}");
                if (record.ActualValue != null)
                {
                    if (record.IsVerified)
                    {
                        console.WriteSuccess($"      Found:    {record.ActualValue}");
                        console.WriteSuccess($"      Status:   VERIFIED ({record.DnsServer})");
                    }
                    else
                    {
                        console.WriteWarning($"      Found:    {record.ActualValue}");
                        console.WriteWarning($"      Status:   MISMATCH - value doesn't match expected ({record.DnsServer})");
                    }
                }
                else
                {
                    console.WriteWarning("      Found:    (not found)");
                    console.WriteWarning($"      Status:   NOT PROPAGATED YET ({record.DnsServer})");
                }
            }

            console.WriteLine("");
            if (dnsResult.AllVerified)
            {
                console.WriteSuccess("  All DNS records verified on our side!");
                console.WriteLine("  Waiting for AWS SES to detect the records...");
                console.WriteLine("  (AWS SES polls DNS independently - this can take a few minutes)");
            }
            else
            {
                var verifiedCount = dnsResult.Records.Count(r => r.IsVerified);
                console.WriteWarning($"  DNS records: {verifiedCount}/{dnsResult.Records.Length} verified");
                console.WriteLine("  DNS propagation may still be in progress.");
                console.WriteLine("  If records were recently added, please wait 5-10 minutes.");
            }
        }

        // Save the updated LastCheckedAt timestamp
        await _domainService.UpdateAsync(domain);
        console.WriteLine("");
        console.WriteLine("  Will retry at next scheduled check (every 5 minutes)");
    }

    /// <summary>
    /// Verify a single domain immediately (for manual "Check now" requests).
    /// </summary>
    public async Task VerifySingleDomainAsync(string domainId, PerformContext? context)
    {
        var console = new JobConsole(_logger, context);

        var domain = await _domainService.GetByIdAsync(domainId);
        if (domain == null)
        {
            console.WriteError($"Domain {domainId} not found");
            return;
        }

        if (domain.Status != DomainStatus.Verifying)
        {
            console.WriteWarning($"Domain {domainId} is not in verifying state (current: {domain.Status})");
            return;
        }

        console.WriteLine("========================================");
        console.WriteInfo("Manual verification triggered");
        console.WriteLine("========================================");
        await VerifyDomainAsync(domain, console);
    }
}
