using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
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
        group.MapGet("/emails/{id}", GetEmail);
        group.MapGet("/emails", ListEmails);
        group.MapPost("/emails/batch", SendBatch);
        return group;
    }

    private static async Task<Results<JsonHttpResult<SendEmailResponse>, BadRequest<object>, UnprocessableEntity<object>, JsonHttpResult<object>>> SendEmail(
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
        var validation = await ValidateRequestAsync(
            request,
            domainService,
            apiKeyService,
            auditService,
            user,
            ct);

        if (!validation.Success)
        {
            if (validation.StatusCode == StatusCodes.Status422UnprocessableEntity)
            {
                return TypedResults.UnprocessableEntity(ResendError(
                    validation.StatusCode,
                    validation.ErrorName!,
                    validation.ErrorMessage!,
                    validation.ErrorCode));
            }

            if (validation.StatusCode == StatusCodes.Status400BadRequest)
            {
                return TypedResults.BadRequest(ResendError(
                    validation.StatusCode,
                    validation.ErrorName!,
                    validation.ErrorMessage!,
                    validation.ErrorCode));
            }

            return TypedResults.Json(
                ResendError(validation.StatusCode, validation.ErrorName!, validation.ErrorMessage!, validation.ErrorCode),
                statusCode: validation.StatusCode);
        }

        var sentEmail = await SendAndStoreAsync(
            request,
            validation.Domain!,
            auditService,
            sesService,
            db,
            loggerFactory,
            user,
            ct);

        return TypedResults.Json(new SendEmailResponse(sentEmail.Id), statusCode: StatusCodes.Status200OK);
    }

    private static async Task<Results<JsonHttpResult<ResendEmailReceipt>, NotFound<object>, JsonHttpResult<object>>> GetEmail(
        string id,
        AppDbContext db,
        ApiKeyService apiKeyService,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(id, out var parsedId))
        {
            return TypedResults.Json(
                ResendError(StatusCodes.Status400BadRequest, "invalid_parameter", "Invalid email id", "invalid_parameter"),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var email = await db.SentEmails
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new SentEmailProjection(
                e.Id,
                e.MessageId,
                e.SentAt,
                e.FromAddress,
                e.ToAddresses,
                e.CcAddresses,
                e.BccAddresses,
                e.ReplyTo,
                e.Subject,
                e.HtmlBody,
                e.TextBody,
                e.DomainId))
            .FirstOrDefaultAsync(ct);

        if (email is null)
        {
            return TypedResults.NotFound(ApiError.NotFound.ToResendResponse(StatusCodes.Status404NotFound));
        }

        if (!apiKeyService.CanAccessDomain(user, email.DomainId))
        {
            return TypedResults.Json(
                ResendError(StatusCodes.Status403Forbidden, "invalid_access", "Access denied to this resource", "forbidden"),
                statusCode: StatusCodes.Status403Forbidden);
        }

        var receipt = BuildReceipt(parsedId, email);

        return TypedResults.Json(receipt, statusCode: StatusCodes.Status200OK);
    }

    private static async Task<Results<JsonHttpResult<ResendPaginatedResult<ResendEmailReceipt>>, JsonHttpResult<object>>> ListEmails(
        AppDbContext db,
        ClaimsPrincipal user,
        string? before = null,
        string? after = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var allowedDomains = user.FindAll("AllowedDomain").Select(c => c.Value).ToList();
        var isAdmin = user.FindFirst("ActorType")?.Value == "admin";

        var query = db.SentEmails.AsNoTracking();

        if (!isAdmin)
        {
            query = query.Where(e => allowedDomains.Contains(e.DomainId));
        }

        if (!string.IsNullOrEmpty(before))
        {
            var beforeEmail = await db.SentEmails.AsNoTracking()
                .Where(e => e.Id == before)
                .Select(e => new { e.Id, e.SentAt })
                .FirstOrDefaultAsync(ct);

            if (beforeEmail is null)
            {
                return TypedResults.Json(
                    ResendError(StatusCodes.Status400BadRequest, "invalid_parameter", "Invalid before cursor", "invalid_parameter"),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            query = query.Where(e =>
                e.SentAt > beforeEmail.SentAt ||
                (e.SentAt == beforeEmail.SentAt && string.Compare(e.Id, beforeEmail.Id, StringComparison.Ordinal) > 0));
        }

        if (!string.IsNullOrEmpty(after))
        {
            var afterEmail = await db.SentEmails.AsNoTracking()
                .Where(e => e.Id == after)
                .Select(e => new { e.Id, e.SentAt })
                .FirstOrDefaultAsync(ct);

            if (afterEmail is null)
            {
                return TypedResults.Json(
                    ResendError(StatusCodes.Status400BadRequest, "invalid_parameter", "Invalid after cursor", "invalid_parameter"),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            query = query.Where(e =>
                e.SentAt < afterEmail.SentAt ||
                (e.SentAt == afterEmail.SentAt && string.Compare(e.Id, afterEmail.Id, StringComparison.Ordinal) < 0));
        }

        var rawItems = await query
            .OrderByDescending(e => e.SentAt)
            .ThenByDescending(e => e.Id)
            .Take(limit + 1)
            .Select(e => new SentEmailProjection(
                e.Id,
                e.MessageId,
                e.SentAt,
                e.FromAddress,
                e.ToAddresses,
                e.CcAddresses,
                e.BccAddresses,
                e.ReplyTo,
                e.Subject,
                e.HtmlBody,
                e.TextBody,
                e.DomainId))
            .ToListAsync(ct);

        var hasMore = rawItems.Count > limit;
        if (hasMore) rawItems.RemoveAt(rawItems.Count - 1);

        var receipts = new List<ResendEmailReceipt>(rawItems.Count);
        foreach (var item in rawItems)
        {
            if (!Guid.TryParse(item.Id, out var guid))
            {
                return TypedResults.Json(
                    ResendError(StatusCodes.Status500InternalServerError, "internal_server_error", "Stored email id is invalid", "internal_error"),
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            receipts.Add(BuildReceipt(guid, item));
        }

        var result = new ResendPaginatedResult<ResendEmailReceipt>
        {
            Data = receipts,
            HasMore = hasMore
        };

        return TypedResults.Json(result, statusCode: StatusCodes.Status200OK);
    }

    private static async Task<Results<JsonHttpResult<object>, BadRequest<object>, UnprocessableEntity<object>>> SendBatch(
        SendEmailRequest[] requests,
        DomainService domainService,
        ApiKeyService apiKeyService,
        AuditService auditService,
        ISesService sesService,
        AppDbContext db,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        CancellationToken ct = default)
    {
        var validationHeader = httpRequest.Headers["x-batch-validation"].ToString();
        var mode = string.IsNullOrWhiteSpace(validationHeader) ? "strict" : validationHeader.ToLowerInvariant();
        var permissive = mode == "permissive";

        if (requests is null || requests.Length == 0)
        {
            return TypedResults.BadRequest(ResendError(
                StatusCodes.Status400BadRequest,
                "missing_required_field",
                "Invalid request: missing required fields",
                "invalid_request"));
        }

        if (!permissive && mode != "strict")
        {
            return TypedResults.BadRequest(ResendError(
                StatusCodes.Status400BadRequest,
                "invalid_request",
                "Invalid x-batch-validation header",
                "invalid_request"));
        }

        if (!permissive)
        {
            var validations = new List<(SendEmailRequest Request, Domain Domain)>();

            for (var i = 0; i < requests.Length; i++)
            {
                var validation = await ValidateRequestAsync(
                    requests[i],
                    domainService,
                    apiKeyService,
                    auditService,
                    user,
                    ct);

                if (!validation.Success)
                {
                    if (validation.StatusCode == StatusCodes.Status422UnprocessableEntity)
                    {
                        return TypedResults.UnprocessableEntity(ResendError(
                            validation.StatusCode,
                            validation.ErrorName!,
                            validation.ErrorMessage!,
                            validation.ErrorCode));
                    }

                    return TypedResults.BadRequest(ResendError(
                        validation.StatusCode,
                        validation.ErrorName!,
                        validation.ErrorMessage!,
                        validation.ErrorCode));
                }

                validations.Add((requests[i], validation.Domain!));
            }

            var data = new List<ResendObjectId>();
            foreach (var item in validations)
            {
                var sentEmail = await SendAndStoreAsync(
                    item.Request,
                    item.Domain,
                    auditService,
                    sesService,
                    db,
                    loggerFactory,
                    user,
                    ct);

                data.Add(new ResendObjectId(Guid.Parse(sentEmail.Id)));
            }

            return TypedResults.Json<object>(new ResendListOf<ResendObjectId>
            {
                Data = data
            }, statusCode: StatusCodes.Status200OK);
        }

        var batchData = new List<ResendObjectId>();
        var batchErrors = new List<ResendBatchError>();

        for (var i = 0; i < requests.Length; i++)
        {
            var validation = await ValidateRequestAsync(
                requests[i],
                domainService,
                apiKeyService,
                auditService,
                user,
                ct);

            if (!validation.Success)
            {
                batchErrors.Add(new ResendBatchError
                {
                    Index = i,
                    Message = validation.ErrorMessage ?? "Invalid request"
                });
                continue;
            }

            var sentEmail = await SendAndStoreAsync(
                requests[i],
                validation.Domain!,
                auditService,
                sesService,
                db,
                loggerFactory,
                user,
                ct);

            batchData.Add(new ResendObjectId(Guid.Parse(sentEmail.Id)));
        }

        return TypedResults.Json<object>(new ResendEmailBatchResponse
        {
            Data = batchData,
            Errors = batchErrors.Count > 0 ? batchErrors : null
        }, statusCode: StatusCodes.Status200OK);
    }

    private static async Task<ValidationResult> ValidateRequestAsync(
        SendEmailRequest request,
        DomainService domainService,
        ApiKeyService apiKeyService,
        AuditService auditService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
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
            return ValidationResult.Fail(
                StatusCodes.Status400BadRequest,
                "missing_required_field",
                "Invalid request: missing required fields",
                "invalid_request");
        }

        MailAddress fromAddress;
        try
        {
            fromAddress = new MailAddress(request.From);
        }
        catch
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
            return ValidationResult.Fail(
                StatusCodes.Status400BadRequest,
                "invalid_from_address",
                "Invalid From address format",
                "invalid_from");
        }

        var domainName = fromAddress.Address.Split('@')[1].ToLowerInvariant();
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
            return ValidationResult.Fail(
                StatusCodes.Status422UnprocessableEntity,
                "validation_error",
                $"Domain not verified: {domainName}",
                "domain_not_verified");
        }

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
            return ValidationResult.Fail(
                StatusCodes.Status403Forbidden,
                "invalid_access",
                $"API key not authorized for domain: {domainName}",
                "forbidden");
        }

        return ValidationResult.Ok(domain);
    }

    private static async Task<SentEmail> SendAndStoreAsync(
        SendEmailRequest request,
        Domain domain,
        AuditService auditService,
        ISesService sesService,
        AppDbContext db,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
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

        var domainName = domain.Name.ToLowerInvariant();

        auditService.Log(new AuditEntry(
            Action: "email.send",
            ActorType: user.FindFirst("ActorType")?.Value ?? "unknown",
            ActorId: user.FindFirst("KeyPrefix")?.Value,
            ResourceType: "email",
            ResourceId: messageId,
            StatusCode: 200,
            Details: new { Domain = domainName, RecipientCount = request.To.Length }
        ));

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
            var logger = loggerFactory.CreateLogger("EmailEndpoints");
            logger.LogError(ex, "Failed to store sent email {MessageId}", messageId);
        }

        return sentEmail;
    }

    private static ResendEmailReceipt BuildReceipt(Guid id, SentEmailProjection email)
    {
        var to = JsonSerializer.Deserialize<string[]>(email.ToAddresses) ?? Array.Empty<string>();
        var cc = email.CcAddresses != null ? JsonSerializer.Deserialize<string[]>(email.CcAddresses) : null;
        var bcc = email.BccAddresses != null ? JsonSerializer.Deserialize<string[]>(email.BccAddresses) : null;
        var replyTo = email.ReplyTo != null ? JsonSerializer.Deserialize<string[]>(email.ReplyTo) : null;

        return new ResendEmailReceipt
        {
            Id = id,
            From = email.FromAddress,
            To = to,
            Cc = cc,
            Bcc = bcc,
            ReplyTo = replyTo,
            Subject = email.Subject,
            HtmlBody = email.HtmlBody,
            TextBody = email.TextBody,
            MomentCreated = email.SentAt,
            LastEvent = null
        };
    }

    private record SentEmailProjection(
        string Id,
        string MessageId,
        DateTime SentAt,
        string FromAddress,
        string ToAddresses,
        string? CcAddresses,
        string? BccAddresses,
        string? ReplyTo,
        string Subject,
        string? HtmlBody,
        string? TextBody,
        string DomainId);

    private record ValidationResult(
        bool Success,
        Domain? Domain,
        int StatusCode,
        string? ErrorName,
        string? ErrorMessage,
        string? ErrorCode)
    {
        public static ValidationResult Ok(Domain domain) =>
            new(true, domain, StatusCodes.Status200OK, null, null, null);

        public static ValidationResult Fail(int statusCode, string name, string message, string? code) =>
            new(false, null, statusCode, name, message, code);
    }

    private class ResendPaginatedResult<T>
    {
        [JsonPropertyName("data")]
        public required List<T> Data { get; set; }

        [JsonPropertyName("has_more")]
        public required bool HasMore { get; set; }
    }

    private class ResendEmailReceipt
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("from")]
        public string From { get; set; } = "";

        [JsonPropertyName("to")]
        public string[] To { get; set; } = Array.Empty<string>();

        [JsonPropertyName("cc")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? Cc { get; set; }

        [JsonPropertyName("bcc")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? Bcc { get; set; }

        [JsonPropertyName("reply_to")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? ReplyTo { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = "";

        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TextBody { get; set; }

        [JsonPropertyName("html")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? HtmlBody { get; set; }

        [JsonPropertyName("scheduled_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? MomentSchedule { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime MomentCreated { get; set; }

        [JsonPropertyName("last_event")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LastEvent { get; set; }
    }

    private class ResendObjectId
    {
        public ResendObjectId(Guid id)
        {
            Id = id;
        }

        [JsonPropertyName("id")]
        public Guid Id { get; set; }
    }

    private class ResendListOf<T>
    {
        [JsonPropertyName("data")]
        public required List<T> Data { get; set; }
    }

    private class ResendBatchError
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    private class ResendEmailBatchResponse
    {
        [JsonPropertyName("data")]
        public required List<ResendObjectId> Data { get; set; }

        [JsonPropertyName("errors")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ResendBatchError>? Errors { get; set; }
    }

    private static object ResendError(int statusCode, string name, string message, string? code = null)
    {
        return new
        {
            statusCode,
            name,
            message,
            error = new
            {
                code = code ?? name,
                message
            }
        };
    }
}
