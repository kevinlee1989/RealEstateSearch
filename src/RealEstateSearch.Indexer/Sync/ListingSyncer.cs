using System.Globalization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Microsoft.EntityFrameworkCore;
using RealEstateSearch.Data;
using RealEstateSearch.Data.Models;
using RealEstateSearch.Indexer.Elasticsearch;

namespace RealEstateSearch.Indexer.Sync;

// Copies listings from PostgreSQL into the search index.
// Every row becomes either an index (searchable) or a delete (not searchable),
// so the index always matches the current state of the database.
public class ListingSyncer(
    IServiceScopeFactory scopeFactory,
    ElasticsearchClient client,
    ILogger<ListingSyncer> logger)
{
    // Small enough to keep each bulk request well under a few MB
    private const int BatchSize = 500;

    public async Task BackfillAsync(CancellationToken cancellationToken)
    {
        var total = new SyncResult();
        long lastId = 0;

        while (true)
        {
            var batch = await ReadBatchAsync(lastId, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            total += await SendAsync(batch, cancellationToken);
            lastId = batch[^1].Id;
        }

        logger.LogInformation(
            "Backfill finished: {Indexed} indexed, {Deleted} deleted, " +
            "{AlreadyAbsent} not searchable and already absent, {Failed} failed",
            total.Indexed, total.Deleted, total.AlreadyAbsent, total.Failed);
    }

    // Keyset pagination: "Id > last seen" walks the primary key B-tree, so every page is
    // equally fast, unlike OFFSET which re-scans all skipped rows
    private async Task<List<Listing>> ReadBatchAsync(long afterId, CancellationToken cancellationToken)
    {
        // This class is a singleton but AppDbContext is scoped: borrow a fresh one per batch
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Listings
            .AsNoTracking()
            .Where(l => l.Id > afterId)
            .OrderBy(l => l.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task<SyncResult> SendAsync(
        IReadOnlyList<Listing> listings, CancellationToken cancellationToken)
    {
        var request = new BulkRequest(ListingIndexManager.AliasName)
        {
            // Without this, a missing alias would make ES auto-create a plain "listings" index
            // with guessed field types instead of failing
            RequireAlias = true,
            Operations = new BulkOperationsCollection()
        };

        foreach (var listing in listings)
        {
            var id = listing.Id.ToString(CultureInfo.InvariantCulture);

            if (ListingDocument.IsSearchable(listing))
            {
                request.Operations.Add(
                    new BulkIndexOperation<ListingDocument>(ListingDocument.FromListing(listing))
                    {
                        Id = id
                    });
            }
            else
            {
                request.Operations.Add(new BulkDeleteOperation(id));
            }
        }

        var response = await client.BulkAsync(request, cancellationToken);

        // The whole request failed (e.g. ES unreachable): nothing was applied, so stop
        if (!response.ApiCallDetails.HasSuccessfulStatusCode)
        {
            throw new InvalidOperationException(
                $"Bulk request failed: {response.DebugInformation}");
        }

        // Bulk returns 200 even when some items fail, so check every item.
        // One bad document is logged and skipped rather than blocking the rest.
        var result = new SyncResult();
        foreach (var item in response.Items)
        {
            if (item.Error is not null)
            {
                logger.LogWarning(
                    "Failed to sync listing {Id}: {Type} {Reason}",
                    item.Id, item.Error.Type, item.Error.Reason);
                result = result with { Failed = result.Failed + 1 };
            }
            else if (item.Result == "not_found")
            {
                // Deleting a document that was never indexed is fine
                result = result with { AlreadyAbsent = result.AlreadyAbsent + 1 };
            }
            else if (item.Result == "deleted")
            {
                result = result with { Deleted = result.Deleted + 1 };
            }
            else
            {
                // "created" or "updated"
                result = result with { Indexed = result.Indexed + 1 };
            }
        }

        return result;
    }

    private readonly record struct SyncResult(int Indexed, int Deleted, int AlreadyAbsent, int Failed)
    {
        public static SyncResult operator +(SyncResult a, SyncResult b) => new(
            a.Indexed + b.Indexed,
            a.Deleted + b.Deleted,
            a.AlreadyAbsent + b.AlreadyAbsent,
            a.Failed + b.Failed);
    }
}
