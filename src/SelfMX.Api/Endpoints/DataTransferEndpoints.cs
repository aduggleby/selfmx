using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using SelfMX.Api.Contracts;
using SelfMX.Api.Contracts.Responses;
using SelfMX.Api.Services;

namespace SelfMX.Api.Endpoints;

public static class DataTransferEndpoints
{
    // Exports without sent emails stay small; this leaves room for long audit trails
    private const long MaxImportBytes = 256L * 1024 * 1024;

    public static RouteGroupBuilder MapDataTransferEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/admin/export", Export);
        group.MapPost("/admin/import", Import);
        return group;
    }

    private static IResult Export(
        DataTransferService dataTransfer,
        AuditService auditService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var fileName = $"selfmx-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json.gz";
        return TypedResults.Stream(async stream =>
        {
            // Audited after the file is written, so a failed download is not recorded as a success
            var status = StatusCodes.Status200OK;
            string? error = null;
            try
            {
                await dataTransfer.WriteExportAsync(stream, ct);
            }
            catch (Exception ex)
            {
                status = StatusCodes.Status500InternalServerError;
                error = ex.Message;
                throw;
            }
            finally
            {
                auditService.Log(new AuditEntry(
                    Action: "data.export",
                    ActorType: user.FindFirst("ActorType")?.Value ?? "admin",
                    ActorId: user.FindFirst("KeyPrefix")?.Value,
                    ResourceType: "instance",
                    ResourceId: null,
                    StatusCode: status,
                    ErrorMessage: error));
            }
        }, "application/gzip", fileName);
    }

    private static async Task<IResult> Import(
        HttpContext context,
        DataTransferService dataTransfer,
        AuditService auditService,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var bodySize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySize is { IsReadOnly: false })
            bodySize.MaxRequestBodySize = MaxImportBytes;

        try
        {
            var data = await DataTransferService.ReadExportAsync(context.Request.Body, ct);
            var result = await dataTransfer.ImportAsync(data, ct);

            auditService.Log(new AuditEntry(
                Action: "data.import",
                ActorType: user.FindFirst("ActorType")?.Value ?? "admin",
                ActorId: user.FindFirst("KeyPrefix")?.Value,
                ResourceType: "instance",
                ResourceId: null,
                StatusCode: 200,
                Details: new { data.SourceVersion, data.SourceDatabase, data.ExportedAt, result }));

            return TypedResults.Ok(result);
        }
        catch (DataImportException ex)
        {
            var status = ex.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
            auditService.Log(new AuditEntry(
                Action: "data.import",
                ActorType: user.FindFirst("ActorType")?.Value ?? "admin",
                ActorId: user.FindFirst("KeyPrefix")?.Value,
                ResourceType: "instance",
                ResourceId: null,
                StatusCode: status,
                ErrorMessage: ex.Message));

            return TypedResults.Json(new ApiError("import_rejected", ex.Message).ToResendResponse(status), statusCode: status);
        }
    }
}
