using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SelfMX.Api.Data;
using SelfMX.Api.Entities;

namespace SelfMX.Api.Services;

public class AuditService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;
    private readonly Channel<AuditLog> _channel = Channel.CreateBounded<AuditLog>(10_000);
    private Task? _consumerTask;

    public AuditService(
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    // Non-blocking log - never awaits database
    public void Log(AuditEntry entry)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid().ToString(),
            Action = entry.Action,
            ActorType = entry.ActorType,
            ActorId = entry.ActorId,
            ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId,
            StatusCode = entry.StatusCode,
            ErrorMessage = entry.ErrorMessage,
            Details = entry.Details is not null ? JsonSerializer.Serialize(entry.Details) : null,
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
        };

        // TryWrite returns false if channel full - log warning but don't block
        if (!_channel.Writer.TryWrite(log))
        {
            _logger.LogWarning("Audit channel full, dropping entry: {Action}", entry.Action);
        }
    }

    // Background consumer batches writes
    public Task StartAsync(CancellationToken ct)
    {
        _consumerTask = ConsumeAsync(ct);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _channel.Writer.Complete();
        if (_consumerTask is not null)
        {
            try
            {
                await _consumerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var batch = new List<AuditLog>(50);

        try
        {
            while (await _channel.Reader.WaitToReadAsync(ct))
            {
                // Drain available items up to batch size
                while (_channel.Reader.TryRead(out var log) && batch.Count < 50)
                {
                    batch.Add(log);
                }

                if (batch.Count > 0)
                {
                    try
                    {
                        // Create scope for each batch to get fresh DbContext
                        using var scope = _scopeFactory.CreateScope();
                        var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

                        auditDb.AuditLogs.AddRange(batch);
                        await auditDb.SaveChangesAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to write {Count} audit logs", batch.Count);
                    }
                    finally
                    {
                        batch.Clear();
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Drain remaining items on shutdown
            while (_channel.Reader.TryRead(out var log))
            {
                batch.Add(log);
            }

            if (batch.Count > 0)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
                    auditDb.AuditLogs.AddRange(batch);
                    await auditDb.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write {Count} audit logs on shutdown", batch.Count);
                }
            }
        }
    }

    // Query interface - uses scoped DbContext
    public async Task<(AuditLog[] Items, int Total)> ListAsync(
        int page,
        int limit,
        string? action = null,
        string? actorId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        var query = auditDb.AuditLogs.AsQueryable();

        if (action is not null)
            query = query.Where(l => l.Action == action);
        if (actorId is not null)
            query = query.Where(l => l.ActorId == actorId);
        if (from.HasValue)
            query = query.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.Timestamp <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToArrayAsync(ct);

        return (items, total);
    }
}
