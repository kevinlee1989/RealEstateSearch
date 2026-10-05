using RealEstateSearch.Data.Models;

namespace RealEstateSearch.Indexer.Elasticsearch;

// Shape of a listing in the search index; property names become camelCase fields.
// The mapping is strict, so this must not carry anything that listings-index.json lacks
// (e.g. Id, which travels as the document _id instead).
public record ListingDocument(
    GeoPoint Location,
    string? Neighbourhood,
    string? RoomType,
    decimal PricePerNight,
    int? Accommodates,
    int? MinimumNights,
    DateTime UpdatedAt,
    string Name,
    string? Description,
    string? PropertyType,
    int? Bedrooms,
    int? Beds,
    double? Bathrooms,
    double? ReviewScoreRating)
{
    // Only bookable listings are searchable; a missing price means "not bookable right now".
    // Everything else stays in PostgreSQL but is removed from the index.
    public static bool IsSearchable(Listing listing) =>
        !listing.IsDeleted && listing.PricePerNight is not null;

    public static ListingDocument FromListing(Listing listing) => new(
        Location: new GeoPoint(listing.Latitude, listing.Longitude),
        Neighbourhood: listing.Neighbourhood,
        RoomType: listing.RoomType,
        PricePerNight: listing.PricePerNight
            ?? throw new InvalidOperationException(
                $"Listing {listing.Id} has no price and must not be indexed"),
        Accommodates: listing.Accommodates,
        MinimumNights: listing.MinimumNights,
        UpdatedAt: listing.UpdatedAt,
        Name: listing.Name,
        Description: listing.Description,
        PropertyType: listing.PropertyType,
        Bedrooms: listing.Bedrooms,
        Beds: listing.Beds,
        Bathrooms: listing.Bathrooms,
        ReviewScoreRating: listing.ReviewScoreRating);
}

// Serializes as { "lat": ..., "lon": ... }, one of the formats geo_point accepts
public record GeoPoint(double Lat, double Lon);
