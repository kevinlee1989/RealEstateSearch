using Microsoft.EntityFrameworkCore;
using RealEstateSearch.Data.Models;

namespace RealEstateSearch.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Listing> Listings => Set<Listing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Listing>()
            .Property(l => l.Id)
            .ValueGeneratedNever();

        // The sync worker polls "changed since checkpoint" ordered by (UpdatedAt, Id);
        // without this B-tree index every poll would scan the whole table
        modelBuilder.Entity<Listing>()
            .HasIndex(l => new { l.UpdatedAt, l.Id });
    }

    // Set timestamps in one place so every save path (importer, API) keeps
    // UpdatedAt accurate for change detection by the search sync worker.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Listing>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

