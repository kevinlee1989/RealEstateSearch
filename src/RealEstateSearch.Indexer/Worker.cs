using RealEstateSearch.Indexer.Elasticsearch;

namespace RealEstateSearch.Indexer;

// Orchestrates the sync: decides what runs and in which order.
// The actual Elasticsearch work lives in the classes it calls.
public class Worker(ListingIndexManager indexManager) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await indexManager.EnsureIndexAsync(stoppingToken);

        // Next steps: full backfill, then the incremental sync loop
    }
}
