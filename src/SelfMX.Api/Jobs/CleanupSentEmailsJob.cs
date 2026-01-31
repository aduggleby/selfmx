using System.Diagnostics;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SelfMX.Api.Data;
using SelfMX.Api.Settings;

namespace SelfMX.Api.Jobs;

public class CleanupSentEmailsJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AppSettings> _appSettings;
    private readonly ILogger<CleanupSentEmailsJob> _logger;

    private const int BaseBatchSize = 1000;
    private const int MaxBatchSize = 5000;
    private const int MaxBatchesPerRun = 500;

    public CleanupSentEmailsJob(
        IServiceScopeFactory scopeFactory,
        IOptions<AppSettings> appSettings,
        ILogger<CleanupSentEmailsJob> logger)
    {
        _scopeFactory = scopeFactory;
        _appSettings = appSettings;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var retentionDays = _appSettings.Value.SentEmailRetentionDays;

        if (retentionDays is null or <= 0)
        {
            _logger.LogDebug("Sent email retention disabled, skipping cleanup");
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays.Value);
        _logger.LogInformation("Cleaning up sent emails older than {Cutoff}", cutoff);

        int totalDeleted = 0;
        int batchCount = 0;
        int batchSize = BaseBatchSize;
        var stopwatch = Stopwatch.StartNew();

        while (batchCount < MaxBatchesPerRun)
        {
            ct.ThrowIfCancellationRequested();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var batchStart = Stopwatch.StartNew();
            var deleted = await db.SentEmails
                .Where(e => e.SentAt < cutoff)
                .OrderBy(e => e.SentAt)
                .Take(batchSize)
                .ExecuteDeleteAsync(ct);

            if (deleted == 0)
                break;

            totalDeleted += deleted;
            batchCount++;

            // Adaptive batch sizing based on execution time
            var elapsed = batchStart.ElapsedMilliseconds;
            if (elapsed < 500 && batchSize < MaxBatchSize)
                batchSize = Math.Min(batchSize * 2, MaxBatchSize);
            else if (elapsed > 2000 && batchSize > BaseBatchSize)
                batchSize = Math.Max(batchSize / 2, BaseBatchSize);

            if (batchCount % 50 == 0)
            {
                _logger.LogInformation(
                    "Cleanup progress: {Deleted} records in {Batches} batches",
                    totalDeleted, batchCount);
            }

            // Brief pause between batches to reduce lock contention
            await Task.Delay(100, ct);
        }

        _logger.LogInformation(
            "Cleanup complete: {Total} records in {Batches} batches, Duration: {Duration:mm\\:ss}",
            totalDeleted, batchCount, stopwatch.Elapsed);
    }
}
