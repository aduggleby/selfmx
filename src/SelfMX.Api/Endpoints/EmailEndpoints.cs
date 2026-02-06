using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using SelfMX.Api.Contracts.Requests;
using SelfMX.Api.Contracts.Responses;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;
using SelfMX.Api.Services;

namespace SelfMX.Api.Endpoints;

public static class EmailEndpoints
{
    public static RouteGroupBuilder MapEmailEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/emails", SendEmail);
        return group;
    }

    private static async Task<Results<Ok<SendEmailResponse>, BadRequest<object>, UnprocessableEntity<object>, ForbidHttpResult>> SendEmail(
        SendEmailRequest request,
        DomainService domainService,
        ApiKeyService apiKeyService,
        AuditService auditService,
        ISesService sesService,
        AppDbContext db,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.From) ||
            request.To is null || request.To.Length == 0 ||
            string.IsNullOrWhiteSpace(request.Subject) ||
            (string.IsNullOrWhiteSpace(request.Html) && string.IsNullOrWhiteSpace(request.Text)))
        {
            auditService.Log(new AuditEntry(
                Action: "email.send",
                ActorType: user.FindFirst("ActorType")?.Value ?? "unknown",
                ActorId: user.FindFirst("KeyPrefix")?.Value,
                ResourceType: "email",
                ResourceId: null,
                StatusCode: 400,
                ErrorMessage: "Invalid request: missing required fields"
            ));
            return TypedResults.BadRequest(ApiError.InvalidRequest.ToResponse());
        }

        // Extract domain from From address
        var fromParts = request.From.Split('@');
        if (fromParts.Length != 2)
        {
            auditService.Log(new AuditEntry(
                Action: "email.send",
                ActorType: user.FindFirst("ActorType")?.Value ?? "unknown",
                ActorId: user.FindFirst("KeyPrefix")?.Value,
                ResourceType: "email",
                ResourceId: null,
                StatusCode: 400,
                ErrorMessage: "Invalid From address format"
            ));
            return TypedResults.BadRequest(new ApiError("invalid_from", "Invalid From address format").ToResponse());
        }

        var domainName = fromParts[1].ToLowerInvariant();
        var domain = await domainService.GetByNameAsync(domainName, ct);

        if (domain is null || domain.Status != DomainStatus.Verified)
        {
            auditService.Log(new AuditEntry(
                Action: "email.send",
                ActorType: user.FindFirst("ActorType")?.Value ?? "unknown",
                ActorId: user.FindFirst("KeyPrefix")?.Value,
                ResourceType: "email",
                ResourceId: null,
                StatusCode: 422,
                ErrorMessage: $"Domain not verified: {domainName}"
            ));
            return TypedResults.UnprocessableEntity(ApiError.DomainNotVerified.ToResponse());
        }

        // Check domain scope for non-admin API keys
        if (!apiKeyService.CanAccessDomain(user, domain.Id))
        {
            auditService.Log(new AuditEntry(
                Action: "email.send",
                ActorType: user.FindFirst("ActorType")?.Value ?? "unknown",
                ActorId: user.FindFirst("KeyPrefix")?.Value,
                ResourceType: "email",
                ResourceId: null,
                StatusCode: 403,
                ErrorMessage: $"API key not authorized for domain: {domainName}"
            ));
            return TypedResults.Forbid();
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

        auditService.Log(new AuditEntry(
            Action: "email.send",
            ActorType: user.FindFirst("ActorType")?.Value ?? "unknown",
            ActorId: user.FindFirst("KeyPrefix")?.Value,
            ResourceType: "email",
            ResourceId: messageId,
            StatusCode: 200,
            Details: new { Domain = domainName, RecipientCount = request.To.Length }
        ));

        // Store sent email in database
        var sentEmail = new SentEmail
        {
            Id = Guid.NewGuid().ToString(),
            MessageId = messageId,
            FromAddress = request.From,
            ToAddresses = JsonSerializer.Serialize(request.To),
            CcAddresses = request.Cc is { Length: > 0 } ? JsonSerializer.Serialize(request.Cc) : null,
            BccAddresses = request.Bcc is { Length: > 0 } ? JsonSerializer.Serialize(request.Bcc) : null,
            ReplyTo = request.ReplyTo is { Length: > 0 } ? JsonSerializer.Serialize(request.ReplyTo) : null,
            Subject = request.Subject,
            HtmlBody = request.Html,
            TextBody = request.Text,
            DomainId = domain.Id,
            ApiKeyId = user.FindFirst("KeyId")?.Value
        };

        try
        {
            db.SentEmails.Add(sentEmail);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Log but don't fail - email was already sent
            var logger = loggerFactory.CreateLogger("EmailEndpoints");
            logger.LogError(ex, "Failed to store sent email {MessageId}", messageId);
        }

        return TypedResults.Ok(new SendEmailResponse(sentEmail.Id));
    }
}
