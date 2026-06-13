using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Queries.GetListingById;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Domain.Enums;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/listings")]
public sealed class ListingsController : ControllerBase
{

    private const string GetListingByIdRouteName = "GetListingById";

    private readonly CreateListingHandler _createListingHandler;
    private readonly GetListingsHandler _getListingsHandler;
    private readonly GetListingByIdHandler _getListingByIdHandler;

    public ListingsController(
        CreateListingHandler createListingHandler,
        GetListingsHandler getListingsHandler,
        GetListingByIdHandler getListingByIdHandler)
    {
        _createListingHandler = createListingHandler;
        _getListingsHandler = getListingsHandler;
        _getListingByIdHandler = getListingByIdHandler;
    }

    [HttpPost]
    public async Task<ActionResult<ListingResponse>> CreateListing(
        [FromBody] CreateListingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createListingHandler.HandleAsync(request, cancellationToken);

        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtRoute(
            GetListingByIdRouteName,
            new { id = result.Value!.Id, lang = result.Value.LanguageCode ?? "mk" },
            result.Value);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ListingResponse>>> GetListings(
    [FromQuery] string lang = "mk",
    [FromQuery] ListingType? listingType = null,
    [FromQuery] PropertyType? propertyType = null,
    [FromQuery] decimal? minPrice = null,
    [FromQuery] decimal? maxPrice = null,
    [FromQuery] string? city = null,
    [FromQuery] string? neighborhood = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var query = new GetListingsQuery
        {
            LanguageCode = lang,
            ListingType = listingType,
            PropertyType = propertyType,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            City = city,
            Neighborhood = neighborhood,
            Page = page,
            PageSize = pageSize
        };

        var listings = await _getListingsHandler.HandleAsync(query, cancellationToken);

        return Ok(listings);
    }

    [HttpGet("{id:guid}", Name = GetListingByIdRouteName)]
    public async Task<ActionResult<ListingResponse>> GetListingById(
        Guid id,
        [FromQuery] string lang = "mk",
        CancellationToken cancellationToken = default)
    {
        var result = await _getListingByIdHandler.HandleAsync(id, lang, cancellationToken);

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(result.Error);
        }

        return Ok(result.Value);
    }
}
