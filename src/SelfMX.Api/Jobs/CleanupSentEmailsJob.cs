using System.Diagnostics;
using Hangfire;
using Hangfire.Server;
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
    public async Task ExecuteAsync(CancellationToken ct, PerformContext? context)
    {
        var console = new JobConsole(_logger, context);

        console.WriteLine("========================================");
        console.WriteInfo("CleanupSentEmailsJob started");
        console.WriteLine($"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        console.WriteLine("========================================");

        var retentionDays = _appSettings.Value.SentEmailRetentionDays;

        if (retentionDays is null or <= 0)
        {
            console.WriteLine("Sent email retention disabled (SentEmailRetentionDays not set)");
            console.WriteWarning("No cleanup will be performed");
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays.Value);
        console.WriteLine($"Retention policy: {retentionDays} days");
        console.WriteLine($"Deleting emails sent before: {cutoff:yyyy-MM-dd HH:mm:ss} UTC");
        console.WriteLine("");

        int totalDeleted = 0;
        int batchCount = 0;
        int batchSize = BaseBatchSize;
        var stopwatch = Stopwatch.StartNew();
        var progress = console.WriteProgressBar();

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
            {
                console.WriteLine("No more records to delete");
                break;
            }

            totalDeleted += deleted;
            batchCount++;

            // Update progress (estimate based on batch size)
            var estimatedProgress = Math.Min(batchCount * 2, 100); // Rough estimate
            progress?.SetValue(estimatedProgress);

            // Adaptive batch sizing based on execution time
            var elapsed = batchStart.ElapsedMilliseconds;
            if (elapsed < 500 && batchSize < MaxBatchSize)
            {
                var oldSize = batchSize;
                batchSize = Math.Min(batchSize * 2, MaxBatchSize);
                console.WriteLine($"  Batch {batchCount}: {deleted} deleted in {elapsed}ms (increasing batch size {oldSize} -> {batchSize})");
            }
            else if (elapsed > 2000 && batchSize > BaseBatchSize)
            {
                var oldSize = batchSize;
                batchSize = Math.Max(batchSize / 2, BaseBatchSize);
                console.WriteWarning($"  Batch {batchCount}: {deleted} deleted in {elapsed}ms (decreasing batch size {oldSize} -> {batchSize})");
            }
            else
            {
                console.WriteLine($"  Batch {batchCount}: {deleted} deleted in {elapsed}ms");
            }

            if (batchCount % 50 == 0)
            {
                console.WriteInfo($"  Progress: {totalDeleted} records deleted in {batchCount} batches");
            }

            // Brief pause between batches to reduce lock contention
            await Task.Delay(100, ct);
        }

        progress?.SetValue(100);

        console.WriteLine("");
        console.WriteLine("========================================");
        console.WriteSuccess("CleanupSentEmailsJob completed");
        console.WriteLine($"Total records deleted: {totalDeleted}");
        console.WriteLine($"Batches processed: {batchCount}");
        console.WriteLine($"Duration: {stopwatch.Elapsed:mm\\:ss}");
        console.WriteLine("========================================");
    }
}
