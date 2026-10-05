namespace RealEstateSearch.Indexer.Sync;

// Bound from the "Sync" section of appsettings.json
public class SyncOptions
{
    // How often to look for changed rows: the maximum delay before a change is searchable
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);

    // How far back each poll re-reads. Covers transactions that commit after their
    // UpdatedAt was set; must be longer than the slowest write transaction
    public TimeSpan Overlap { get; set; } = TimeSpan.FromSeconds(30);
}
