namespace RealEstateSearch.Api.Models;

// The API's own contract, separate from ListingDocument (the index shape),
// so the index can change without breaking API clients
public record ListingSearchResponse(
    long Total,
    int Page,
    int PageSize,
    IReadOnlyList<ListingSummary> Items);

public record ListingSummary(
    string Id,
    string Name,
    string? Neighbourhood,
    string? RoomType,
    string? PropertyType,
    decimal PricePerNight,
    int? Accommodates,
    int? Bedrooms,
    double? Bathrooms,
    int? MinimumNights,
    double? ReviewScoreRating,
    double Latitude,
    double Longitude);

public record NeighbourhoodCount(string Name, long Count);
