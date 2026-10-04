using Microsoft.EntityFrameworkCore;
using RealEstateSearch.Api.Models;

namespace RealEstateSearch.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Listing> Listings => Set<Listing>();
}