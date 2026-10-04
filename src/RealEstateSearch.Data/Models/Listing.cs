namespace RealEstateSearch.Data.Models;

public class Listing
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string? Neighbourhood { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string? PropertyType { get; set; }
    public string? RoomType { get; set; }

    public int? Accommodates { get; set; }
    public double? Bathrooms { get; set; }
    public int? Bedrooms { get; set; }
    public int? Beds { get; set; }

    public decimal? PricePerNight { get; set; }

    public int? MinimumNights { get; set; }
    public int? Availability365 { get; set; }

    public double? ReviewScoreRating { get; set; }

    public DateTime? LastScraped { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }
}