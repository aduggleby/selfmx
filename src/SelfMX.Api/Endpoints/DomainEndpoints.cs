using System.Security.Claims;
using System.Text.RegularExpressions;
using Hangfire;
using Microsoft.AspNetCore.Http.HttpResults;
using SelfMX.Api.Contracts.Requests;
using SelfMX.Api.Contracts.Responses;
using SelfMX.Api.Entities;
using SelfMX.Api.Jobs;
using SelfMX.Api.Services;

namespace SelfMX.Api.Endpoints;

public static class DomainEndpoints
{
    public static RouteGroupBuilder MapDomainEndpoints(this RouteGroupBuilder group)
    {
        var domains = group.MapGroup("/domains");

        domains.MapGet("/", ListDomains);
        domains.MapPost("/", CreateDomain);
        domains.MapGet("/{id}", GetDomain);
        domains.MapDelete("/{id}", DeleteDomain);
        domains.MapPost("/{id}/verify", VerifyDomain);
        domains.MapPost("/{id}/test-email", SendTestEmail);

        return group;
    }

    private static async Task<Results<Ok<PaginatedResponse<DomainResponse>>, BadRequest<object>>> ListDomains(
        DomainService domainService,
        int page = 1,
        int limit = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (limit < 1 || limit > 100) limit = 20;

        var (items, total) = await domainService.ListAsync(page, limit, ct);

        var responses = items.Select(d =>
        {
            var dnsRecords = d.DnsRecordsJson?.DeserializeDnsRecords();
            var recordResponses = dnsRecords?.Select(r => new DnsRecordResponse(
                r.Type, r.Name, r.Value, r.Priority, false
            )).ToArray();

            return DomainResponse.FromEntity(d, recordResponses);
        }).ToArray();

        var response = new PaginatedResponse<DomainResponse>(
            responses,
            page,
            limit,
            total
        );

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Created<DomainResponse>, Conflict<object>, BadRequest<object>>> CreateDomain(
        CreateDomainRequest request,
        DomainService domainService,
        IBackgroundJobClient backgroundJobs,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest(ApiError.InvalidRequest.ToResponse());
        }

        var existing = await domainService.GetByNameAsync(request.Name, ct);
        if (existing is not null)
        {
            return TypedResults.Conflict(ApiError.DomainAlreadyExists.ToResponse());
        }

        var domain = await domainService.CreateAsync(request.Name, ct);

        // Enqueue SES identity creation (Hangfire injects PerformContext at runtime)
        backgroundJobs.Enqueue<SetupDomainJob>(job => job.ExecuteAsync(domain.Id, null));

        return TypedResults.Created($"/v1/domains/{domain.Id}", DomainResponse.FromEntity(domain));
    }

    private static async Task<Results<Ok<DomainResponse>, NotFound<object>>> GetDomain(
        string id,
        DomainService domainService,
        CancellationToken ct = default)
    {
        var domain = await domainService.GetByIdAsync(id, ct);
        if (domain is null)
        {
            return TypedResults.NotFound(ApiError.NotFound.ToResponse());
        }

        var dnsRecords = domain.DnsRecordsJson?.DeserializeDnsRecords();
        var recordResponses = dnsRecords?.Select(r => new DnsRecordResponse(
            r.Type, r.Name, r.Value, r.Priority, false
        )).ToArray();

        return TypedResults.Ok(DomainResponse.FromEntity(domain, recordResponses));
    }

    private static async Task<Results<Ok<DomainResponse>, NotFound<object>, BadRequest<object>>> VerifyDomain(
        string id,
        DomainService domainService,
        VerifyDomainsJob verifyJob,
        CancellationToken ct = default)
    {
        var domain = await domainService.GetByIdAsync(id, ct);
        if (domain is null)
        {
            return TypedResults.NotFound(ApiError.NotFound.ToResponse());
        }

        if (domain.Status != DomainStatus.Verifying)
        {
            return TypedResults.BadRequest(ApiError.InvalidRequest.ToResponse() as object);
        }

        // Run verification immediately (no Hangfire context when called directly)
        await verifyJob.VerifySingleDomainAsync(id, null);

        // Reload domain to get updated status
        domain = await domainService.GetByIdAsync(id, ct);
        var dnsRecords = domain!.DnsRecordsJson?.DeserializeDnsRecords();
        var recordResponses = dnsRecords?.Select(r => new DnsRecordResponse(
            r.Type, r.Name, r.Value, r.Priority, false
        )).ToArray();

        return TypedResults.Ok(DomainResponse.FromEntity(domain, recordResponses));
    }

    private static async Task<Results<NoContent, NotFound<object>>> DeleteDomain(
        string id,
        DomainService domainService,
        ISesService sesService,
        ICloudflareService cloudflareService,
        CancellationToken ct = default)
    {
        var domain = await domainService.GetByIdAsync(id, ct);
        if (domain is null)
        {
            return TypedResults.NotFound(ApiError.NotFound.ToResponse());
        }

        // Delete SES identity
        await sesService.DeleteDomainIdentityAsync(domain.Name, ct);

        // Delete DNS records from Cloudflare
        await cloudflareService.DeleteDnsRecordsForDomainAsync(domain.Name, ct);

        await domainService.DeleteAsync(domain, ct);

        return TypedResults.NoContent();
    }

    private static readonly Regex SenderPrefixRegex = new(@"^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private static async Task<Results<Ok<SendEmailResponse>, BadRequest<object>, NotFound<object>, UnprocessableEntity<object>, ForbidHttpResult>> SendTestEmail(
        string id,
        SendTestEmailRequest request,
        DomainService domainService,
        ApiKeyService apiKeyService,
        AuditService auditService,
        ISesService sesService,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        var actorType = user.FindFirst("ActorType")?.Value ?? "unknown";
        var actorId = user.FindFirst("KeyPrefix")?.Value;

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.SenderPrefix) ||
            string.IsNullOrWhiteSpace(request.To) ||
            string.IsNullOrWhiteSpace(request.Subject) ||
            string.IsNullOrWhiteSpace(request.Text))
        {
            auditService.Log(new AuditEntry(
                Action: "domain.test_email",
                ActorType: actorType,
                ActorId: actorId,
                ResourceType: "domain",
                ResourceId: id,
                StatusCode: 400,
                ErrorMessage: "Missing required fields"
            ));
            return TypedResults.BadRequest(ApiError.InvalidRequest.ToResponse());
        }

        // Validate sender prefix format
        if (!SenderPrefixRegex.IsMatch(request.SenderPrefix))
        {
            auditService.Log(new AuditEntry(
                Action: "domain.test_email",
                ActorType: actorType,
                ActorId: actorId,
                ResourceType: "domain",
                ResourceId: id,
                StatusCode: 400,
                ErrorMessage: $"Invalid sender prefix: {request.SenderPrefix}"
            ));
            return TypedResults.BadRequest(ApiError.InvalidSenderPrefix.ToResponse());
        }

        // Validate recipient email format
        if (!EmailRegex.IsMatch(request.To))
        {
            auditService.Log(new AuditEntry(
                Action: "domain.test_email",
                ActorType: actorType,
                ActorId: actorId,
                ResourceType: "domain",
                ResourceId: id,
                StatusCode: 400,
                ErrorMessage: $"Invalid recipient email: {request.To}"
            ));
            return TypedResults.BadRequest(ApiError.InvalidRecipientEmail.ToResponse());
        }

        // Get domain
        var domain = await domainService.GetByIdAsync(id, ct);
        if (domain is null)
        {
            auditService.Log(new AuditEntry(
                Action: "domain.test_email",
                ActorType: actorType,
                ActorId: actorId,
                ResourceType: "domain",
                ResourceId: id,
                StatusCode: 404,
                ErrorMessage: "Domain not found"
            ));
            return TypedResults.NotFound(ApiError.NotFound.ToResponse());
        }

        // Check domain is verified
        if (domain.Status != DomainStatus.Verified)
        {
            auditService.Log(new AuditEntry(
                Action: "domain.test_email",
                ActorType: actorType,
                ActorId: actorId,
                ResourceType: "domain",
                ResourceId: id,
                StatusCode: 422,
                ErrorMessage: $"Domain not verified: {domain.Name}"
            ));
            return TypedResults.UnprocessableEntity(ApiError.DomainNotVerified.ToResponse());
        }

        // Check authorization
        if (!apiKeyService.CanAccessDomain(user, domain.Id))
        {
            auditService.Log(new AuditEntry(
                Action: "domain.test_email",
                ActorType: actorType,
                ActorId: actorId,
                ResourceType: "domain",
                ResourceId: id,
                StatusCode: 403,
                ErrorMessage: $"Not authorized for domain: {domain.Name}"
            ));
            return TypedResults.Forbid();
        }

        // Construct from address and send
        var fromAddress = $"{request.SenderPrefix}@{domain.Name}";
        var messageId = await sesService.SendEmailAsync(
            fromAddress,
            [request.To],
            request.Subject,
            html: null,
            text: request.Text,
            ct: ct);

        auditService.Log(new AuditEntry(
            Action: "domain.test_email",
            ActorType: actorType,
            ActorId: actorId,
            ResourceType: "domain",
            ResourceId: id,
            StatusCode: 200,
            Details: new { From = fromAddress, To = request.To, MessageId = messageId }
        ));

        return TypedResults.Ok(new SendEmailResponse(messageId));
    }
}
