using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Queries.GetListingById;
using RealEstate.Application.Listings.Queries.GetListings;

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
    public async Task<ActionResult<IReadOnlyList<ListingResponse>>> GetListings(
        [FromQuery] string lang = "mk",
        CancellationToken cancellationToken = default)
    {
        var listings = await _getListingsHandler.HandleAsync(lang, cancellationToken);

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
