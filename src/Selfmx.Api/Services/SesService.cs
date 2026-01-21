using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Options;
using Selfmx.Api.Entities;
using Selfmx.Api.Settings;
using System.Text.Json;

namespace Selfmx.Api.Services;

public record DnsRecordInfo(
    string Type,
    string Name,
    string Value,
    int Priority = 0
);

public class SesService : ISesService
{
    private readonly IAmazonSimpleEmailServiceV2 _ses;
    private readonly ILogger<SesService> _logger;

    public SesService(IAmazonSimpleEmailServiceV2 ses, ILogger<SesService> logger)
    {
        _ses = ses;
        _logger = logger;
    }

    public async Task<(string IdentityArn, DnsRecordInfo[] DnsRecords)> CreateDomainIdentityAsync(
        string domainName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating SES domain identity for {Domain}", domainName);

        var request = new CreateEmailIdentityRequest
        {
            EmailIdentity = domainName,
            DkimSigningAttributes = new DkimSigningAttributes
            {
                NextSigningKeyLength = DkimSigningKeyLength.RSA_2048_BIT
            }
        };

        var response = await _ses.CreateEmailIdentityAsync(request, ct);

        var dnsRecords = new List<DnsRecordInfo>();

        // Add DKIM records
        if (response.DkimAttributes?.Tokens != null)
        {
            foreach (var token in response.DkimAttributes.Tokens)
            {
                dnsRecords.Add(new DnsRecordInfo(
                    "CNAME",
                    $"{token}._domainkey.{domainName}",
                    $"{token}.dkim.amazonses.com"
                ));
            }
        }

        _logger.LogInformation(
            "Created SES domain identity for {Domain}, DKIM records: {Count}",
            domainName,
            dnsRecords.Count);

        // Get the identity ARN
        var getRequest = new GetEmailIdentityRequest { EmailIdentity = domainName };
        var identity = await _ses.GetEmailIdentityAsync(getRequest, ct);

        return (identity.IdentityType.ToString(), dnsRecords.ToArray());
    }

    public async Task<bool> CheckDkimVerificationAsync(string domainName, CancellationToken ct = default)
    {
        try
        {
            var request = new GetEmailIdentityRequest { EmailIdentity = domainName };
            var response = await _ses.GetEmailIdentityAsync(request, ct);

            var status = response.DkimAttributes?.Status;
            _logger.LogDebug("DKIM status for {Domain}: {Status}", domainName, status);

            return status == DkimStatus.SUCCESS;
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("Domain identity not found in SES: {Domain}", domainName);
            return false;
        }
    }

    public async Task DeleteDomainIdentityAsync(string domainName, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting SES domain identity for {Domain}", domainName);

        try
        {
            var request = new DeleteEmailIdentityRequest { EmailIdentity = domainName };
            await _ses.DeleteEmailIdentityAsync(request, ct);
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("Domain identity not found when attempting deletion: {Domain}", domainName);
        }
    }

    public async Task<string> SendEmailAsync(
        string from,
        string[] to,
        string subject,
        string? html,
        string? text,
        string[]? cc = null,
        string[]? bcc = null,
        string[]? replyTo = null,
        CancellationToken ct = default)
    {
        var destination = new Destination
        {
            ToAddresses = to.ToList()
        };

        if (cc?.Length > 0)
        {
            destination.CcAddresses = cc.ToList();
        }

        if (bcc?.Length > 0)
        {
            destination.BccAddresses = bcc.ToList();
        }

        var body = new Body();
        if (!string.IsNullOrEmpty(html))
        {
            body.Html = new Content { Data = html };
        }
        if (!string.IsNullOrEmpty(text))
        {
            body.Text = new Content { Data = text };
        }

        var request = new SendEmailRequest
        {
            FromEmailAddress = from,
            Destination = destination,
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject },
                    Body = body
                }
            }
        };

        if (replyTo?.Length > 0)
        {
            request.ReplyToAddresses = replyTo.ToList();
        }

        _logger.LogInformation(
            "Sending email from {From} to {To}, subject: {Subject}",
            from,
            string.Join(", ", to),
            subject);

        var response = await _ses.SendEmailAsync(request, ct);

        _logger.LogInformation("Email sent successfully, MessageId: {MessageId}", response.MessageId);

        return response.MessageId;
    }
}

public static class DnsRecordExtensions
{
    public static string SerializeDnsRecords(this DnsRecordInfo[] records)
    {
        return JsonSerializer.Serialize(records);
    }

    public static DnsRecordInfo[]? DeserializeDnsRecords(this string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<DnsRecordInfo[]>(json);
    }
}
