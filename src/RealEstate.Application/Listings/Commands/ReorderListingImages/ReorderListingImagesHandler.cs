using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Commands.UploadListingImage;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Repositories;

namespace RealEstate.Application.Listings.Commands.ReorderListingImages;

public sealed class ReorderListingImagesHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IListingRepository _listingRepository;

    public ReorderListingImagesHandler(IListingRepository listingRepository, ICurrentUserService currentUserService)
    {
        _listingRepository = listingRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ReorderListingImagesResult> Handle(
        ReorderListingImagesCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ImageIds.Count == 0)
        {
            return ReorderListingImagesResult.Failure(ReorderListingImagesError.ImageIdsMissing);
        }

        var listing = await _listingRepository.GetByIdWithImagesForUpdateAsync(
            command.ListingId,
            cancellationToken);

        if (listing is null)
        {
            return ReorderListingImagesResult.Failure(ReorderListingImagesError.ListingNotFound);
        }

        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        if (listing.CreatedByUserId != userId)
        {
            return ReorderListingImagesResult.Failure(ReorderListingImagesError.NotListingOwner);
        }

        if (listing.Images.Count != command.ImageIds.Count)
        {
            return ReorderListingImagesResult.Failure(ReorderListingImagesError.ImageSetMismatch);
        }

        var listingImageIds = listing.Images
            .Select(image => image.Id)
            .ToHashSet();

        var requestedImageIds = command.ImageIds.ToHashSet();

        if (!listingImageIds.SetEquals(requestedImageIds))
        {
            return ReorderListingImagesResult.Failure(ReorderListingImagesError.ImageSetMismatch);
        }

        var order = 0;

        foreach (var imageId in command.ImageIds)
        {
            var image = listing.Images.First(image => image.Id == imageId);

            image.SortOrder = order;

            order++;
        }

        await _listingRepository.SaveChangesAsync(cancellationToken);

        var response = listing.Images
            .OrderBy(image => image.SortOrder)
            .Select(image => new ListingImageResponse
            {
                Id = image.Id,
                Url = image.Url,
                ContentType = image.ContentType,
                SizeBytes = image.SizeBytes,
                SortOrder = image.SortOrder,
                IsPrimary = image.IsPrimary
            })
            .ToList();

        return ReorderListingImagesResult.Success(response);
    }
}
