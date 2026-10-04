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
        var skipped = 0;

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync() && listings.Count < 3000)
        {
            
            var id = ParseLong(csv.GetField("id"));
            var latitude = ParseDouble(csv.GetField("latitude"));
            var longitude = ParseDouble(csv.GetField("longitude"));

            if (id is null || latitude is null || longitude is null)
            {
                skipped++;
                continue;
            }
            var listing = new Listing 
            {
                Id = id.Value,

                Name = csv.GetField("name") ?? string.Empty,
                Description = csv.GetField("description"),
                Neighbourhood = csv.GetField("neighbourhood_cleansed"),

                Latitude = latitude.Value,
                Longitude = longitude.Value,

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
                    ParseDate(csv.GetField("last_scraped"))
            };

            listings.Add(listing);
        }

        _context.Listings.AddRange(listings);
        await _context.SaveChangesAsync();
        Console.WriteLine($"Imported {listings.Count}, skipped {skipped}");

    }


    private static long? ParseLong(string? value)
    {
        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
            return result;

        return null;
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
        if (DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result))
        {
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        }

        return null;
    }

}