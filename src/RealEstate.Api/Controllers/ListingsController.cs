using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Queries.GetListingById;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Domain.Enums;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Listings.Commands.UploadListingImage;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/listings")]
public sealed class ListingsController : ControllerBase
{

    private const string GetListingByIdRouteName = "GetListingById";

    private readonly CreateListingHandler _createListingHandler;
    private readonly GetListingsHandler _getListingsHandler;
    private readonly GetListingByIdHandler _getListingByIdHandler;
    private readonly UploadListingImageHandler _uploadListingImageHandler;

    public ListingsController(
        CreateListingHandler createListingHandler,
        GetListingsHandler getListingsHandler,
        GetListingByIdHandler getListingByIdHandler,
        UploadListingImageHandler uploadListingImageHandler)
    {
        _createListingHandler = createListingHandler;
        _getListingsHandler = getListingsHandler;
        _getListingByIdHandler = getListingByIdHandler;
        _uploadListingImageHandler = uploadListingImageHandler;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ListingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    [ProducesResponseType(typeof(PagedResponse<ListingResponse>), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(ListingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ListingImageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadImage(
    Guid id,
    IFormFile? file,
    CancellationToken cancellationToken)
    {
        using var stream = file?.OpenReadStream();

        var uploadedFile = file is null
            ? null
            : new UploadedFile(
                stream!,
                file.FileName,
                file.ContentType,
                file.Length);

        var result = await _uploadListingImageHandler.Handle(
            new UploadListingImageCommand(id, uploadedFile),
            cancellationToken);

        if (result.Succeeded)
        {
            return Created($"/api/listings/{id}/images/{result.Image!.Id}", result.Image);
        }

        return result.Error switch
        {
            UploadListingImageError.ListingNotFound => NotFound(),
            UploadListingImageError.FileMissing => BadRequest("Image file is required."),
            UploadListingImageError.FileEmpty => BadRequest("Image file is empty."),
            UploadListingImageError.FileTooLarge => BadRequest("Image file cannot be larger than 5 MB."),
            UploadListingImageError.InvalidFileType => BadRequest("Only JPG, JPEG, PNG, and WEBP images are allowed."),
            UploadListingImageError.ImageLimitReached => BadRequest("Listing cannot have more than 20 images."),
            _ => BadRequest("Image upload failed.")
        };
    }
}
