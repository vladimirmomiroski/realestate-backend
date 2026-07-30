using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Application.Listings.Commands.DeleteListingImage;
using RealEstate.Application.Listings.Commands.ReorderListingImages;
using RealEstate.Application.Listings.Commands.SetPrimaryListingImage;
using RealEstate.Application.Listings.Commands.UploadListingImage;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Queries.GetListingById;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using RealEstate.Application.Listings.Queries.GetMyListings;
using RealEstate.Application.Listings.Commands.PublishListing;
using RealEstate.Application.Listings.Commands.UnpublishListing;
using RealEstate.Application.Listings.Commands.ArchiveListing;
using RealEstate.Application.Listings.Queries.GetComparableListings;
using RealEstate.Api.Errors;

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
    private readonly DeleteListingImageHandler _deleteListingImageHandler;
    private readonly SetPrimaryListingImageHandler _setPrimaryListingImageHandler;
    private readonly ReorderListingImagesHandler _reorderListingImagesHandler;
    private readonly GetMyListingsHandler _getMyListingsHandler;
    private readonly PublishListingHandler _publishListingHandler;
    private readonly UnpublishListingHandler _unpublishListingHandler;
    private readonly ArchiveListingHandler _archiveListingHandler;
    private readonly GetComparableListingsHandler _getComparableListingsHandler;
    private readonly ApiFailureService _failureService;

    public ListingsController(
        CreateListingHandler createListingHandler,
        GetListingsHandler getListingsHandler,
        GetListingByIdHandler getListingByIdHandler,
        GetComparableListingsHandler getComparableListingsHandler,
        UploadListingImageHandler uploadListingImageHandler,
        DeleteListingImageHandler deleteListingImageHandler,
        SetPrimaryListingImageHandler setPrimaryListingImageHandler,
        ReorderListingImagesHandler reorderListingImagesHandler,
        GetMyListingsHandler getMyListingsHandler,  
        PublishListingHandler publishListingHandler,
        UnpublishListingHandler unpublishListingHandler,
        ArchiveListingHandler archiveListingHandler,
        ApiFailureService failureService
        )
    {
        _createListingHandler = createListingHandler;
        _getListingsHandler = getListingsHandler;
        _getListingByIdHandler = getListingByIdHandler;
        _getComparableListingsHandler = getComparableListingsHandler;
        _uploadListingImageHandler = uploadListingImageHandler;
        _deleteListingImageHandler = deleteListingImageHandler;
        _setPrimaryListingImageHandler = setPrimaryListingImageHandler;
        _reorderListingImagesHandler = reorderListingImagesHandler;
        _getMyListingsHandler = getMyListingsHandler;
        _publishListingHandler = publishListingHandler;
        _unpublishListingHandler = unpublishListingHandler;
        _archiveListingHandler = archiveListingHandler;
        _failureService = failureService;
        
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(ListingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateListing(
    [FromBody] CreateListingRequest request,
    CancellationToken cancellationToken)
    {
        var result = await _createListingHandler.HandleAsync(request, cancellationToken);

        switch (result.Status)
        {
            case ServiceResultStatus.Success:
                ListingResponse response = result.Value
                    ?? throw new InvalidOperationException(
                        "A successful create-listing result must provide a value.");

                return CreatedAtRoute(
                    GetListingByIdRouteName,
                    new { id = response.Id, lang = response.LanguageCode ?? "mk" },
                    response);

            case ServiceResultStatus.ValidationError:
            case ServiceResultStatus.NotFound:
            case ServiceResultStatus.Unauthorized:
            case ServiceResultStatus.Forbidden:
                return CreateFailureResult(result);

            default:
                throw new InvalidOperationException(
                    "The create-listing result was not mapped.");
        }
    }

    [HttpGet]
    [ProducesResponseType(
    typeof(PagedResponse<ListingResponse>),
    StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetListings(
        [FromQuery] string lang = "mk",
        [FromQuery] string? q = null,
        [FromQuery] ListingType? listingType = null,
        [FromQuery] Guid? agencyId = null,
        [FromQuery] PropertyType? propertyType = null,
        [FromQuery] HeatingType? heatingType = null,
        [FromQuery] FurnishingStatus? furnishingStatus = null,
        [FromQuery] PropertyCondition? condition = null,
        [FromQuery] bool? hasBasement = null,
        [FromQuery] bool? hasElevator = null,
        [FromQuery] ApartmentType? apartmentType = null,
        [FromQuery] HouseType? houseType = null,
        [FromQuery] decimal? minYardAreaSquareMeters = null,
        [FromQuery] decimal? maxYardAreaSquareMeters = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? currency = null,
        [FromQuery] decimal? minAreaSquareMeters = null,
        [FromQuery] decimal? maxAreaSquareMeters = null,
        [FromQuery] decimal? minRooms = null,
        [FromQuery] decimal? maxRooms = null,
        [FromQuery] string sort = "newest",
        [FromQuery] string? city = null,
        [FromQuery] string? municipality = null,
        [FromQuery] string? neighborhood = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetListingsQuery
        {
            LanguageCode = lang,
            SearchText = q,
            AgencyId = agencyId,
            ListingType = listingType,
            PropertyType = propertyType,
            HeatingType = heatingType,
            FurnishingStatus = furnishingStatus,
            Condition = condition,
            HasBasement = hasBasement,
            HasElevator = hasElevator,
            ApartmentType = apartmentType,
            HouseType = houseType,
            MinYardAreaSquareMeters = minYardAreaSquareMeters,
            MaxYardAreaSquareMeters = maxYardAreaSquareMeters,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            Currency = currency,
            MinAreaSquareMeters = minAreaSquareMeters,
            MaxAreaSquareMeters = maxAreaSquareMeters,
            MinRooms = minRooms,
            MaxRooms = maxRooms,
            Sort = sort,
            City = city,
            Municipality = municipality,
            Neighborhood = neighborhood,
            Page = page,
            PageSize = pageSize
        };

        ServiceResult<PagedResponse<ListingResponse>> result =
            await _getListingsHandler.HandleAsync(
                query,
                cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success => Ok(
                result.Value ?? throw new InvalidOperationException(
                    "A successful listing-search result must provide a value.")),
            ServiceResultStatus.ValidationError => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The listing-search result was not mapped.")
        };
    }

    [Authorize]
    [HttpGet("my")]
    [ProducesResponseType(typeof(PagedResult<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyListings(
    [FromQuery] string? lang,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var query = new GetMyListingsQuery(
            lang,
            page,
            pageSize);

        ServiceResult<PagedResult<ListingResponse>> result =
            await _getMyListingsHandler.HandleAsync(query, cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success => Ok(
                result.Value ?? throw new InvalidOperationException(
                    "A successful my-listings result must provide a value.")),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The my-listings result was not mapped.")
        };
    }

    [HttpGet("{id:guid}", Name = GetListingByIdRouteName)]
    [ProducesResponseType(typeof(ListingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetListingById(
        Guid id,
        [FromQuery] string lang = "mk",
        CancellationToken cancellationToken = default)
    {
        var result = await _getListingByIdHandler.HandleAsync(id, lang, cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success => Ok(
                result.Value ?? throw new InvalidOperationException(
                    "A successful listing-details result must provide a value.")),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The listing-details result was not mapped.")
        };
    }

    [HttpGet("{id:guid}/comparables")]
    [ProducesResponseType(
    typeof(IReadOnlyList<ListingResponse>),
    StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult>
    GetComparableListings(
        Guid id,
        [FromQuery] string lang = "mk",
        [FromQuery] int limit = 6,
        CancellationToken cancellationToken = default)
    {
        var query = new GetComparableListingsQuery
        {
            ListingId = id,
            LanguageCode = lang,
            Limit = limit
        };

        ServiceResult<IReadOnlyList<ListingResponse>> result =
            await _getComparableListingsHandler.HandleAsync(
                query,
                cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success => Ok(
                result.Value ?? throw new InvalidOperationException(
                    "A successful comparable-listings result must provide a value.")),
            ServiceResultStatus.ValidationError => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The comparable-listings result was not mapped.")
        };
    }

    [Authorize]
    [HttpPut("{id:guid}/publish")]
    [ProducesResponseType(typeof(ListingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishListing(
    Guid id,
    [FromQuery] string? lang,
    CancellationToken cancellationToken)
    {
        var result = await _publishListingHandler.HandleAsync(
            new PublishListingCommand(id, lang),
            cancellationToken);

        return MapLifecycleResult(result, "publish-listing");
    }

    [Authorize]
    [HttpPut("{id:guid}/unpublish")]
    [ProducesResponseType(typeof(ListingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnpublishListing(
    Guid id,
    [FromQuery] string? lang,
    CancellationToken cancellationToken)
    {
        var result = await _unpublishListingHandler.HandleAsync(
            new UnpublishListingCommand(id, lang),
            cancellationToken);

        return MapLifecycleResult(result, "unpublish-listing");
    }

    [Authorize]
    [HttpPut("{id:guid}/archive")]
    [ProducesResponseType(typeof(ListingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveListing(
    Guid id,
    [FromQuery] string? lang,
    CancellationToken cancellationToken)
    {
        var result = await _archiveListingHandler.HandleAsync(
            new ArchiveListingCommand(id, lang),
            cancellationToken);

        return MapLifecycleResult(result, "archive-listing");
    }

    private IActionResult MapLifecycleResult(
        ServiceResult<ListingResponse> result,
        string operation)
    {
        return result.Status switch
        {
            ServiceResultStatus.Success => Ok(
                result.Value ?? throw new InvalidOperationException(
                    $"A successful {operation} result must provide a value.")),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            ServiceResultStatus.Conflict => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                $"The {operation} result was not mapped.")
        };
    }

    private IActionResult CreateFailureResult<T>(ServiceResult<T> result)
    {
        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return _failureService.CreateValidationResult(
                HttpContext,
                result.ValidationKey ?? throw new InvalidOperationException(
                    "A validation result must provide a validation key."),
                result.Error ?? throw new InvalidOperationException(
                    "A validation result must provide an error."),
                result.ErrorCode ?? throw new InvalidOperationException(
                    "A validation result must provide an error code."));
        }

        string errorCode = result.ErrorCode ?? throw new InvalidOperationException(
            "A failure result must provide an error code.");

        return CreateFailureResult(errorCode);
    }

    private IActionResult CreateFailureResult(string errorCode)
    {
        if (errorCode == ErrorCodes.AuthenticationInvalidPrincipal)
        {
            Response.Headers["WWW-Authenticate"] = "Bearer";
        }

        return _failureService.CreateResult(
            HttpContext,
            ApiFailureDescriptor.ForCode(errorCode));
    }

    [Authorize]
    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ListingImageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

        return result.Error switch
        {
            UploadListingImageError.None when result.Image is not null =>
                Created(
                    $"/api/listings/{id}/images/{result.Image.Id}",
                    result.Image),
            UploadListingImageError.None => throw new InvalidOperationException(
                "A successful image upload result requires an image."),
            UploadListingImageError.ListingNotFound =>
                CreateFailureResult(ErrorCodes.ResourceNotFound),
            UploadListingImageError.InvalidPrincipal =>
                CreateFailureResult(ErrorCodes.AuthenticationInvalidPrincipal),
            UploadListingImageError.AccountDisabled =>
                CreateFailureResult(ErrorCodes.AuthorizationAccountDisabled),
            UploadListingImageError.NotListingOwner =>
                CreateFailureResult(ErrorCodes.AuthorizationForbidden),
            UploadListingImageError.FileMissing =>
                _failureService.CreateValidationResult(
                    HttpContext,
                    "file",
                    "Image file is required.",
                    ErrorCodes.ValidationFileRequired),
            UploadListingImageError.FileEmpty =>
                _failureService.CreateValidationResult(
                    HttpContext,
                    "file",
                    "Image file is empty.",
                    ErrorCodes.ValidationFileEmpty),
            UploadListingImageError.FileTooLarge =>
                _failureService.CreateValidationResult(
                    HttpContext,
                    "file",
                    "Image file cannot be larger than 5 MB.",
                    ErrorCodes.ValidationFileTooLarge),
            UploadListingImageError.InvalidFileType =>
                _failureService.CreateValidationResult(
                    HttpContext,
                    "file",
                    "Only JPG, JPEG, PNG, and WEBP images are allowed.",
                    ErrorCodes.ValidationFileTypeNotSupported),
            UploadListingImageError.ImageLimitReached =>
                CreateFailureResult(ErrorCodes.ConflictResourceCapacity),
            _ => throw new InvalidOperationException(
                $"The image upload result '{result.Error}' was not mapped.")
        };
    }

    [Authorize]
    [HttpDelete("{listingId:guid}/images/{imageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(
    Guid listingId,
    Guid imageId,
    CancellationToken cancellationToken)
    {
        var result = await _deleteListingImageHandler.Handle(
            new DeleteListingImageCommand(listingId, imageId),
            cancellationToken);

        return result.Error switch
        {
            DeleteListingImageError.None => NoContent(),
            DeleteListingImageError.ListingNotFound =>
                CreateFailureResult(ErrorCodes.ResourceNotFound),
            DeleteListingImageError.InvalidPrincipal =>
                CreateFailureResult(ErrorCodes.AuthenticationInvalidPrincipal),
            DeleteListingImageError.AccountDisabled =>
                CreateFailureResult(ErrorCodes.AuthorizationAccountDisabled),
            DeleteListingImageError.NotListingOwner =>
                CreateFailureResult(ErrorCodes.AuthorizationForbidden),
            DeleteListingImageError.ImageNotFound =>
                CreateFailureResult(ErrorCodes.ResourceNotFound),
            _ => throw new InvalidOperationException(
                $"The image delete result '{result.Error}' was not mapped.")
        };
    }

    [Authorize]
    [HttpPut("{listingId:guid}/images/{imageId:guid}/primary")]
    [ProducesResponseType(typeof(ListingImageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrimaryImage(
    Guid listingId,
    Guid imageId,
    CancellationToken cancellationToken)
    {
        var result = await _setPrimaryListingImageHandler.Handle(
            new SetPrimaryListingImageCommand(listingId, imageId),
            cancellationToken);

        return result.Error switch
        {
            SetPrimaryListingImageError.None when result.Image is not null =>
                Ok(result.Image),
            SetPrimaryListingImageError.None => throw new InvalidOperationException(
                "A successful set-primary result requires an image."),
            SetPrimaryListingImageError.ListingNotFound =>
                CreateFailureResult(ErrorCodes.ResourceNotFound),
            SetPrimaryListingImageError.InvalidPrincipal =>
                CreateFailureResult(ErrorCodes.AuthenticationInvalidPrincipal),
            SetPrimaryListingImageError.AccountDisabled =>
                CreateFailureResult(ErrorCodes.AuthorizationAccountDisabled),
            SetPrimaryListingImageError.NotListingOwner =>
                CreateFailureResult(ErrorCodes.AuthorizationForbidden),
            SetPrimaryListingImageError.ImageNotFound =>
                CreateFailureResult(ErrorCodes.ResourceNotFound),
            _ => throw new InvalidOperationException(
                $"The set-primary result '{result.Error}' was not mapped.")
        };
    }

    [Authorize]
    [HttpPut("{listingId:guid}/images/order")]
    [ProducesResponseType(typeof(List<ListingImageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReorderImages(
    Guid listingId,
    [FromBody] ReorderListingImagesRequest request,
    CancellationToken cancellationToken)
    {
        var result = await _reorderListingImagesHandler.Handle(
            new ReorderListingImagesCommand(listingId, request.ImageIds),
            cancellationToken);

        return result.Error switch
        {
            ReorderListingImagesError.None when result.Images.Count > 0 =>
                Ok(result.Images),
            ReorderListingImagesError.None => throw new InvalidOperationException(
                "A successful image reorder result requires images."),
            ReorderListingImagesError.ListingNotFound =>
                CreateFailureResult(ErrorCodes.ResourceNotFound),
            ReorderListingImagesError.InvalidPrincipal =>
                CreateFailureResult(ErrorCodes.AuthenticationInvalidPrincipal),
            ReorderListingImagesError.AccountDisabled =>
                CreateFailureResult(ErrorCodes.AuthorizationAccountDisabled),
            ReorderListingImagesError.NotListingOwner =>
                CreateFailureResult(ErrorCodes.AuthorizationForbidden),
            ReorderListingImagesError.ImageIdsMissing =>
                _failureService.CreateValidationResult(
                    HttpContext,
                    "imageIds",
                    "Image ids are required.",
                    ErrorCodes.ValidationFailed),
            ReorderListingImagesError.DuplicateImageIds =>
                _failureService.CreateValidationResult(
                    HttpContext,
                    "imageIds",
                    "Image ids must not contain duplicates.",
                    ErrorCodes.ValidationFailed),
            ReorderListingImagesError.ImageSetMismatch =>
                CreateFailureResult(ErrorCodes.ConflictResourceSetChanged),
            _ => throw new InvalidOperationException(
                $"The image reorder result '{result.Error}' was not mapped.")
        };
    }
}
