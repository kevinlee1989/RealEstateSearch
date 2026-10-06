using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;
using RealEstateSearch.Api.Models;
using RealEstateSearch.Data.Search;

namespace RealEstateSearch.Api.Services;

// Turns a ListingSearchRequest into an Elasticsearch query. Reads only from the index;
// PostgreSQL is never touched on the search path.
public class ListingSearchService(ElasticsearchClient client)
{
    private const string NeighbourhoodsAggregation = "neighbourhoods";

    public async Task<ListingSearchResponse> SearchAsync(
        ListingSearchRequest request, CancellationToken cancellationToken)
    {
        var searchRequest = new SearchRequest(ListingDocument.IndexAlias)
        {
            // Every condition is a yes/no filter: no relevance scoring, results are cacheable
            Query = new BoolQuery { Filter = BuildFilters(request) },
            Sort = [BuildSort(request.Sort)],
            From = (request.Page - 1) * request.PageSize,
            Size = request.PageSize,
            // Exact count instead of the default "10,000+" cap
            TrackTotalHits = true
        };

        var response = await client.SearchAsync<ListingDocument>(searchRequest, cancellationToken);
        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException($"Search failed: {response.DebugInformation}");
        }

        var items = response.Hits
            .Where(hit => hit.Source is not null)
            .Select(hit => ToSummary(hit.Id!, hit.Source!))
            .ToList();

        return new ListingSearchResponse(response.Total, request.Page, request.PageSize, items);
    }

    // Neighbourhood names with listing counts, for the location picker.
    // A terms aggregation on the keyword field: no documents are returned, only buckets.
    public async Task<IReadOnlyList<NeighbourhoodCount>> GetNeighbourhoodsAsync(
        CancellationToken cancellationToken)
    {
        var searchRequest = new SearchRequest(ListingDocument.IndexAlias)
        {
            Size = 0,
            Aggregations = new Dictionary<string, Aggregation>
            {
                [NeighbourhoodsAggregation] = new TermsAggregation
                {
                    Field = "neighbourhood",
                    Size = 200 // there are 96 today
                }
            }
        };

        var response = await client.SearchAsync<ListingDocument>(searchRequest, cancellationToken);
        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(
                $"Neighbourhood lookup failed: {response.DebugInformation}");
        }

        var buckets = response.Aggregations?.GetStringTerms(NeighbourhoodsAggregation)?.Buckets;

        return buckets is null
            ? []
            : buckets.Select(b => new NeighbourhoodCount(b.Key.ToString(), b.DocCount)).ToList();
    }

    private static List<Query> BuildFilters(ListingSearchRequest request)
    {
        var filters = new List<Query>();

        if (!string.IsNullOrWhiteSpace(request.Neighbourhood))
        {
            filters.Add(new TermQuery { Field = "neighbourhood", Value = request.Neighbourhood });
        }

        if (request.HasMapArea)
        {
            filters.Add(new GeoBoundingBoxQuery
            {
                Field = "location",
                BoundingBox = GeoBounds.TopLeftBottomRight(new TopLeftBottomRightGeoBounds
                {
                    TopLeft = GeoLocation.LatitudeLongitude(
                        new LatLonGeoLocation { Lat = request.MaxLat!.Value, Lon = request.MinLon!.Value }),
                    BottomRight = GeoLocation.LatitudeLongitude(
                        new LatLonGeoLocation { Lat = request.MinLat!.Value, Lon = request.MaxLon!.Value })
                })
            });
        }

        if (request.MinPrice is not null || request.MaxPrice is not null)
        {
            filters.Add(new NumberRangeQuery("pricePerNight")
            {
                Gte = (double?)request.MinPrice,
                Lte = (double?)request.MaxPrice
            });
        }

        if (!string.IsNullOrWhiteSpace(request.RoomType))
        {
            filters.Add(new TermQuery { Field = "roomType", Value = request.RoomType });
        }

        if (request.Guests is not null)
        {
            filters.Add(new NumberRangeQuery("accommodates") { Gte = request.Guests });
        }

        if (request.Stay == StayLength.Short)
        {
            filters.Add(new NumberRangeQuery("minimumNights") { Lt = 30 });
        }
        else if (request.Stay == StayLength.Long)
        {
            filters.Add(new NumberRangeQuery("minimumNights") { Gte = 30 });
        }

        return filters;
    }

    private static SortOptions BuildSort(ListingSort sort) => new()
    {
        Field = new FieldSort
        {
            Field = "pricePerNight",
            Order = sort == ListingSort.PriceDesc ? SortOrder.Desc : SortOrder.Asc
        }
    };

    private static ListingSummary ToSummary(string id, ListingDocument doc) => new(
        Id: id,
        Name: doc.Name,
        Neighbourhood: doc.Neighbourhood,
        RoomType: doc.RoomType,
        PropertyType: doc.PropertyType,
        PricePerNight: doc.PricePerNight,
        Accommodates: doc.Accommodates,
        Bedrooms: doc.Bedrooms,
        Bathrooms: doc.Bathrooms,
        MinimumNights: doc.MinimumNights,
        ReviewScoreRating: doc.ReviewScoreRating,
        Latitude: doc.Location.Lat,
        Longitude: doc.Location.Lon);
}
