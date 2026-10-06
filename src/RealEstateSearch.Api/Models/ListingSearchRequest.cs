using System.ComponentModel.DataAnnotations;

namespace RealEstateSearch.Api.Models;

// Query string of GET /api/listings/search. Every filter is optional.
// [ApiController] runs these validation rules and returns 400 before the action runs.
public class ListingSearchRequest : IValidatableObject
{
    // Location, option 1: pick a neighbourhood (see GET /api/listings/neighbourhoods)
    public string? Neighbourhood { get; set; }

    // Location, option 2: the visible map area. All four must be sent together.
    [Range(-90, 90)] public double? MinLat { get; set; }
    [Range(-90, 90)] public double? MaxLat { get; set; }
    [Range(-180, 180)] public double? MinLon { get; set; }
    [Range(-180, 180)] public double? MaxLon { get; set; }

    [Range(0, 100_000)] public decimal? MinPrice { get; set; }
    [Range(0, 100_000)] public decimal? MaxPrice { get; set; }

    // "Entire home/apt", "Private room", "Hotel room" or "Shared room"
    public string? RoomType { get; set; }

    // Minimum number of guests the listing must accommodate
    [Range(1, 50)] public int? Guests { get; set; }

    public StayLength? Stay { get; set; }

    public ListingSort Sort { get; set; } = ListingSort.PriceAsc;

    // page * pageSize stays under Elasticsearch's 10,000 result window
    [Range(1, 100)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;

    public bool HasMapArea => MinLat is not null;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var bounds = new[] { MinLat, MaxLat, MinLon, MaxLon };
        if (bounds.Any(b => b is not null) && bounds.Any(b => b is null))
        {
            yield return new ValidationResult(
                "minLat, maxLat, minLon and maxLon must be sent together",
                [nameof(MinLat), nameof(MaxLat), nameof(MinLon), nameof(MaxLon)]);
        }
        else if (MinLat > MaxLat || MinLon > MaxLon)
        {
            yield return new ValidationResult(
                "minLat/minLon must not be greater than maxLat/maxLon",
                [nameof(MinLat), nameof(MinLon)]);
        }

        if (MinPrice > MaxPrice)
        {
            yield return new ValidationResult(
                "minPrice must not be greater than maxPrice", [nameof(MinPrice)]);
        }
    }
}

public enum StayLength
{
    // Minimum stay under 30 nights
    Short,

    // Monthly or longer only (30+ nights), common in San Diego because of rental rules
    Long
}

public enum ListingSort
{
    PriceAsc,
    PriceDesc
}
