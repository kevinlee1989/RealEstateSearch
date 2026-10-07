using Microsoft.Extensions.Options;
using RealEstateSearch.Indexer.Elasticsearch;
using RealEstateSearch.Indexer.Sync;

namespace RealEstateSearch.Indexer;

// Orchestrates the sync: decides what runs and in which order.
// The actual Elasticsearch work lives in the classes it calls.
public class Worker(
    ListingIndexManager indexManager,
    ListingSyncer syncer,
    IOptions<SyncOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await indexManager.EnsureIndexAsync(stoppingToken);

        // Taken before the backfill so nothing changed during it is missed
        var checkpoint = new SyncCheckpoint(await syncer.GetCheckpointAsync(stoppingToken));

        // Full copy on every startup: the checkpoint lives in memory,
        // so a restart re-sends everything (cheap at this size, and idempotent)
        await syncer.BackfillAsync(checkpoint, stoppingToken);

        var interval = options.Value.PollInterval;
        logger.LogInformation("Watching for changes every {Interval}", interval);

        // Ticks never overlap: if a sync outlasts the interval, the next tick waits for it.
        // That keeps the "one writer, in order" guarantee inside this loop too.
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await syncer.SyncChangesAsync(checkpoint, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // A short outage should not kill the worker. The checkpoint only moved for
                // batches that were sent, so the rest is read again on the next tick.
                logger.LogWarning(ex, "Sync failed, retrying in {Interval}", interval);
            }
        }
    }
}
