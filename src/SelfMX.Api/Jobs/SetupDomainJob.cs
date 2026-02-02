using Hangfire.Server;
using SelfMX.Api.Entities;
using SelfMX.Api.Services;

namespace SelfMX.Api.Jobs;

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

    public async Task ExecuteAsync(string domainId, PerformContext? context)
    {
        var console = new JobConsole(_logger, context);

        console.WriteLine("========================================");
        console.WriteInfo($"SetupDomainJob started for domain ID: {domainId}");
        console.WriteLine("========================================");

        // Step 1: Fetch domain from database
        console.WriteLine("Step 1: Fetching domain from database...");
        var domain = await _domainService.GetByIdAsync(domainId);
        if (domain is null)
        {
            console.WriteError($"Domain {domainId} not found in database - job aborted");
            return;
        }

        console.WriteLine($"  Domain found: {domain.Name}");
        console.WriteLine($"  Current status: {domain.Status}");
        console.WriteLine($"  Created at: {domain.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");

        // Step 2: Validate status
        console.WriteLine("Step 2: Validating domain status...");
        if (domain.Status != DomainStatus.Pending)
        {
            console.WriteWarning($"Domain is not in Pending status (current: {domain.Status}), skipping setup");
            return;
        }
        console.WriteSuccess("  Status is Pending - proceeding with setup");

        try
        {
            // Step 3: Create SES domain identity
            console.WriteInfo("Step 3: Creating SES domain identity...");
            console.WriteLine($"  Calling AWS SES CreateEmailIdentity for: {domain.Name}");

            var (identityArn, dnsRecords) = await _sesService.CreateDomainIdentityAsync(domain.Name);

            console.WriteSuccess("  SES identity created successfully!");
            console.WriteLine($"  Identity ARN: {identityArn}");
            console.WriteLine($"  DNS records returned: {dnsRecords.Length}");

            // Log each DNS record
            console.WriteLine("Step 4: DNS Records from SES:");
            foreach (var record in dnsRecords)
            {
                var recordInfo = record.Type switch
                {
                    "CNAME" => $"    [{record.Type}] {record.Name} -> {record.Value}",
                    "TXT" => $"    [{record.Type}] {record.Name} = \"{record.Value}\"",
                    "MX" => $"    [{record.Type}] {record.Name} -> {record.Value} (Priority: {record.Priority})",
                    _ => $"    [{record.Type}] {record.Name} -> {record.Value}"
                };
                console.WriteLine(recordInfo);
            }

            // Step 5: Create DNS records in Cloudflare
            console.WriteInfo("Step 5: Creating DNS records in Cloudflare...");
            var successCount = 0;
            var failCount = 0;

            foreach (var record in dnsRecords)
            {
                console.WriteLine($"  Creating {record.Type} record: {record.Name}");
                try
                {
                    await _cloudflareService.CreateDnsRecordAsync(
                        record.Type,
                        record.Name,
                        record.Value,
                        record.Priority,
                        proxied: false);
                    console.WriteSuccess("    Created successfully");
                    successCount++;
                }
                catch (CloudflareException ex)
                {
                    console.WriteWarning($"    Failed to create: {ex.Message}");
                    failCount++;
                }
            }

            console.WriteLine($"  DNS records created: {successCount} success, {failCount} failed");

            // Step 6: Update domain in database
            console.WriteInfo("Step 6: Updating domain in database...");
            domain.SesIdentityArn = identityArn;
            domain.DnsRecordsJson = dnsRecords.SerializeDnsRecords();
            domain.Status = DomainStatus.Verifying;
            domain.VerificationStartedAt = DateTime.UtcNow;

            await _domainService.UpdateAsync(domain);

            console.WriteLine("========================================");
            console.WriteSuccess("SetupDomainJob completed successfully!");
            console.WriteLine($"  Domain: {domain.Name}");
            console.WriteLine($"  New status: Verifying");
            console.WriteLine($"  DNS records stored: {dnsRecords.Length}");
            console.WriteLine("  Verification will be checked every 5 minutes");
            console.WriteLine("========================================");
        }
        catch (Exception ex)
        {
            console.WriteError($"Failed to set up domain {domainId}: {ex.Message}");

            domain.Status = DomainStatus.Failed;
            domain.FailureReason = $"Setup failed: {ex.Message}";
            await _domainService.UpdateAsync(domain);

            console.WriteLine("Domain marked as Failed in database");
            throw; // Re-throw to mark job as failed in Hangfire
        }
    }
}
