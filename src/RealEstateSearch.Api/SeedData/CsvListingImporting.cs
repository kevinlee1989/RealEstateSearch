using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using RealEstateSearch.Api.Data;
using RealEstateSearch.Api.Models;
using System.Globalization;

namespace RealEstateSearch.Api.SeedData;

public class CsvListingImporter
{
    private readonly AppDbContext _context;

    public CsvListingImporter(AppDbContext context)
    {
        _context = context;
    }

    public async Task ImportAsync(string filePath)
    {
        // Prevent duplicate inserting 
        if (await _context.Listings.AnyAsync())
        {
            return;
        }

        using var reader = new StreamReader(filePath);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null
        };

        using var csv = new CsvReader(reader, config);

        var listings = new List<Listing>();

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync() && listings.Count < 1000)
        {
            var listing = new Listing
            {
                Id = ParseLong(csv.GetField("id")),

                Name = csv.GetField("name") ?? string.Empty,
                Description = csv.GetField("description"),
                Neighbourhood = csv.GetField("neighbourhood_cleansed"),

                Latitude = ParseDouble(csv.GetField("latitude")) ?? 0,
                Longitude = ParseDouble(csv.GetField("longitude")) ?? 0,

                PropertyType = csv.GetField("property_type"),
                RoomType = csv.GetField("room_type"),

                Accommodates = ParseInt(csv.GetField("accommodates")),
                Bathrooms = ParseDouble(csv.GetField("bathrooms")),
                Bedrooms = ParseInt(csv.GetField("bedrooms")),
                Beds = ParseInt(csv.GetField("beds")),

                PricePerNight = ParsePrice(csv.GetField("price")),

                MinimumNights = ParseInt(csv.GetField("minimum_nights")),
                Availability365 = ParseInt(csv.GetField("availability_365")),

                ReviewScoreRating =
                    ParseDouble(csv.GetField("review_scores_rating")),

                LastScraped =
                    ParseDate(csv.GetField("last_scraped")),

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            listings.Add(listing);
        }

        _context.Listings.AddRange(listings);
        await _context.SaveChangesAsync();
    }

    // Id has scientific notation 
    // try long and if not try double --> long 
    private static long ParseLong(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        if (long.TryParse(value, out var direct))
            return direct;

        if (double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var scientific))
        {
            return Convert.ToInt64(scientific);
        }

        return 0;
    }

    private static int? ParseInt(string? value)
    {
        if (int.TryParse(value, out var result))
            return result;

        return null;
    }

    private static double? ParseDouble(string? value)
    {
        if (double.TryParse(
            value,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var result))
        {
            return result;
        }

        return null;
    }

    private static decimal? ParsePrice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = value
            .Replace("$", "")
            .Replace(",", "")
            .Trim();

        if (decimal.TryParse(
            cleaned,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var result))
        {
            return result;
        }

        return null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (DateTime.TryParse(value, out var result))
            return result;

        return null;
    }
}