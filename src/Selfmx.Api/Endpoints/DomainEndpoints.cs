using Hangfire;
using Microsoft.AspNetCore.Http.HttpResults;
using Selfmx.Api.Contracts.Requests;
using Selfmx.Api.Contracts.Responses;
using Selfmx.Api.Jobs;
using Selfmx.Api.Services;

namespace Selfmx.Api.Endpoints;

public static class DomainEndpoints
{
    public static RouteGroupBuilder MapDomainEndpoints(this RouteGroupBuilder group)
    {
        var domains = group.MapGroup("/domains");

        domains.MapGet("/", ListDomains);
        domains.MapPost("/", CreateDomain);
        domains.MapGet("/{id}", GetDomain);
        domains.MapDelete("/{id}", DeleteDomain);

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

        // Enqueue SES identity creation
        backgroundJobs.Enqueue<SetupDomainJob>(job => job.ExecuteAsync(domain.Id));

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
}
