namespace RealEstateSearch.Indexer.Sync;

// Where the poll loop is. Worker keeps it in memory between ticks.
// Each poll re-reads an overlap window to catch late commits; SentVersions remembers
// which row versions inside that window were already sent, so only new ones go out again.
public class SyncCheckpoint(DateTime? lastUpdatedAt)
{
    // Newest UpdatedAt sent by the poll loop (null when the table was empty)
    public DateTime? LastUpdatedAt { get; set; } = lastUpdatedAt;

    // Id -> UpdatedAt of every row already sent whose UpdatedAt is still inside the window
    public Dictionary<long, DateTime> SentVersions { get; } = [];
}
