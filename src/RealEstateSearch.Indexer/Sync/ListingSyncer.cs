using System.Globalization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RealEstateSearch.Data;
using RealEstateSearch.Data.Models;
using RealEstateSearch.Data.Search;

namespace RealEstateSearch.Indexer.Sync;

// Copies listings from PostgreSQL into the search index.
// Every row becomes either an index (searchable) or a delete (not searchable),
// so the index always matches the current state of the database.
public class ListingSyncer(
    IServiceScopeFactory scopeFactory,
    ElasticsearchClient client,
    IOptions<SyncOptions> options,
    ILogger<ListingSyncer> logger)
{
    // Small enough to keep each bulk request well under a few MB
    private const int BatchSize = 500;

    // Latest UpdatedAt in the table (null when empty). Read before the backfill starts,
    // so rows changed while it runs have a later UpdatedAt and are caught by the first poll.
    // Comparing UpdatedAt with UpdatedAt avoids trusting this machine's clock.
    public async Task<DateTime?> GetCheckpointAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Listings.MaxAsync(l => (DateTime?)l.UpdatedAt, cancellationToken);
    }

    // Sends rows changed since the checkpoint and returns the new checkpoint.
    // Throws if PostgreSQL or Elasticsearch is unreachable; the caller then keeps the old
    // checkpoint, so the same range is read again on the next poll.
    public async Task<DateTime?> SyncChangesAsync(
        DateTime? checkpoint, CancellationToken cancellationToken)
    {
        // Step back by the overlap to catch transactions that committed late.
        // Rows sent last time are sent again, which is harmless: same _id, same content.
        var from = (checkpoint ?? DateTime.UnixEpoch) - options.Value.Overlap;

        var total = new SyncResult();
        var lastUpdatedAt = from;
        var lastId = long.MaxValue; // first page: plain "UpdatedAt > from" (see ReadChangedBatchAsync)
        var newCheckpoint = checkpoint;

        while (true)
        {
            var batch = await ReadChangedBatchAsync(lastUpdatedAt, lastId, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            total += await SendAsync(batch, cancellationToken);

            lastUpdatedAt = batch[^1].UpdatedAt;
            lastId = batch[^1].Id;

            // Rows are ordered by UpdatedAt, so the last row holds the newest value seen.
            // Max() keeps the checkpoint from moving back when only overlap rows were read.
            if (newCheckpoint is null || lastUpdatedAt > newCheckpoint)
            {
                newCheckpoint = lastUpdatedAt;
            }
        }

        if (total.Indexed + total.Deleted + total.AlreadyAbsent + total.Failed > 0)
        {
            logger.LogInformation(
                "Synced changes: {Indexed} indexed, {Deleted} deleted, " +
                "{AlreadyAbsent} not searchable and already absent, {Failed} failed",
                total.Indexed, total.Deleted, total.AlreadyAbsent, total.Failed);
        }

        return newCheckpoint;
    }

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

    // Keyset pagination on (UpdatedAt, Id), served by IX_Listings_UpdatedAt_Id.
    // UpdatedAt alone is not unique (one save stamps many rows with the same time),
    // so Id breaks ties; otherwise rows sharing a timestamp across a page boundary are skipped.
    private async Task<List<Listing>> ReadChangedBatchAsync(
        DateTime afterUpdatedAt, long afterId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Listings
            .AsNoTracking()
            .Where(l => l.UpdatedAt > afterUpdatedAt
                     || (l.UpdatedAt == afterUpdatedAt && l.Id > afterId))
            .OrderBy(l => l.UpdatedAt)
            .ThenBy(l => l.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task<SyncResult> SendAsync(
        IReadOnlyList<Listing> listings, CancellationToken cancellationToken)
    {
        var request = new BulkRequest(ListingDocument.IndexAlias)
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

        // The whole request failed (e.g. ES unreachable): nothing was applied, so stop.
        // Backfill lets this crash the worker; the poll loop catches it and retries.
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
