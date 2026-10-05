using RealEstateSearch.Indexer.Elasticsearch;
using RealEstateSearch.Indexer.Sync;

namespace RealEstateSearch.Indexer;

// Orchestrates the sync: decides what runs and in which order.
// The actual Elasticsearch work lives in the classes it calls.
public class Worker(ListingIndexManager indexManager, ListingSyncer syncer) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await indexManager.EnsureIndexAsync(stoppingToken);

        // Full copy on every startup: the sync checkpoint lives in memory,
        // so a restart re-sends everything (cheap at this size, and idempotent)
        await syncer.BackfillAsync(stoppingToken);

        // Next step: the incremental sync loop
    }
}
