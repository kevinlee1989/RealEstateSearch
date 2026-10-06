using Microsoft.AspNetCore.Mvc;
using RealEstateSearch.Api.Models;
using RealEstateSearch.Api.Services;

namespace RealEstateSearch.Api.Controllers;

// [ApiController] binds query strings to the request object and answers 400
// with the validation errors automatically, so the actions stay thin.
[ApiController]
[Route("api/listings")]
public class ListingsController(ListingSearchService searchService) : ControllerBase
{
    // GET /api/listings/search?neighbourhood=Mission%20Bay&minPrice=100&maxPrice=300&guests=4
    [HttpGet("search")]
    public async Task<ActionResult<ListingSearchResponse>> Search(
        [FromQuery] ListingSearchRequest request, CancellationToken cancellationToken)
    {
        return await searchService.SearchAsync(request, cancellationToken);
    }

    // GET /api/listings/neighbourhoods
    [HttpGet("neighbourhoods")]
    public async Task<ActionResult<IReadOnlyList<NeighbourhoodCount>>> Neighbourhoods(
        CancellationToken cancellationToken)
    {
        var neighbourhoods = await searchService.GetNeighbourhoodsAsync(cancellationToken);
        return Ok(neighbourhoods);
    }
}
