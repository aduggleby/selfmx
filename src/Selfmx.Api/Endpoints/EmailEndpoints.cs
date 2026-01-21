using Microsoft.AspNetCore.Http.HttpResults;
using Selfmx.Api.Contracts.Requests;
using Selfmx.Api.Contracts.Responses;
using Selfmx.Api.Entities;
using Selfmx.Api.Services;

namespace Selfmx.Api.Endpoints;

public static class EmailEndpoints
{
    public static RouteGroupBuilder MapEmailEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/emails", SendEmail);
        return group;
    }

    private static async Task<Results<Ok<SendEmailResponse>, BadRequest<object>, UnprocessableEntity<object>>> SendEmail(
        SendEmailRequest request,
        DomainService domainService,
        ISesService sesService,
        CancellationToken ct = default)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.From) ||
            request.To is null || request.To.Length == 0 ||
            string.IsNullOrWhiteSpace(request.Subject) ||
            (string.IsNullOrWhiteSpace(request.Html) && string.IsNullOrWhiteSpace(request.Text)))
        {
            return TypedResults.BadRequest(ApiError.InvalidRequest.ToResponse());
        }

        // Extract domain from From address
        var fromParts = request.From.Split('@');
        if (fromParts.Length != 2)
        {
            return TypedResults.BadRequest(new ApiError("invalid_from", "Invalid From address format").ToResponse());
        }

        var domainName = fromParts[1].ToLowerInvariant();
        var domain = await domainService.GetByNameAsync(domainName, ct);

        if (domain is null || domain.Status != DomainStatus.Verified)
        {
            return TypedResults.UnprocessableEntity(ApiError.DomainNotVerified.ToResponse());
        }

        var messageId = await sesService.SendEmailAsync(
            request.From,
            request.To,
            request.Subject,
            request.Html,
            request.Text,
            request.Cc,
            request.Bcc,
            request.ReplyTo,
            ct);

        return TypedResults.Ok(new SendEmailResponse(messageId));
    }
}
