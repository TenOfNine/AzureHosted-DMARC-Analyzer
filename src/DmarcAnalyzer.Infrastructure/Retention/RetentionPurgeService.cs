using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RetentionSettingsEntity = DmarcAnalyzer.Core.Entities.RetentionSettings;

namespace DmarcAnalyzer.Infrastructure.Retention;

/// <summary>
/// Batched daily purge of aggregate report data older than the configured retention window, so the
/// database doesn't grow unbounded. Child rows (records, auth results, evaluation results) are removed
/// via the database's own ON DELETE CASCADE — configured on the relevant foreign keys — when their
/// parent AggregateReport row is deleted.
/// </summary>
public class RetentionPurgeService(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    ILogger<RetentionPurgeService> logger) : BackgroundService
{
    private const int BatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.PurgeIntervalHours));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Retention purge cycle failed unexpectedly");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmarcAnalyzerDbContext>();

        var settings = await db.RetentionSettings.FindAsync([RetentionSettingsEntity.SingletonId], cancellationToken);
        if (settings is null)
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-settings.RetentionDays);
        var totalDeleted = 0;

        while (true)
        {
            var idsToDelete = await db.AggregateReports
                .Where(r => r.DateRangeEndUtc < cutoff)
                .OrderBy(r => r.DateRangeEndUtc)
                .Select(r => r.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (idsToDelete.Count == 0)
            {
                break;
            }

            var deleted = await db.AggregateReports
                .Where(r => idsToDelete.Contains(r.Id))
                .ExecuteDeleteAsync(cancellationToken);

            totalDeleted += deleted;

            if (idsToDelete.Count < BatchSize)
            {
                break;
            }
        }

        settings.LastPurgeRunUtc = DateTime.UtcNow;
        settings.LastPurgeRowsDeleted = totalDeleted;
        await db.SaveChangesAsync(cancellationToken);

        if (totalDeleted > 0)
        {
            logger.LogInformation("Retention purge deleted {Count} aggregate report(s) older than {RetentionDays} days", totalDeleted, settings.RetentionDays);
        }
    }
}
