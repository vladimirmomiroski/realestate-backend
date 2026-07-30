using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Commands.UploadListingImage;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.ReorderListingImages;

public sealed class ReorderListingImagesHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IListingRepository _listingRepository;
    private readonly IUserRepository _userRepository;

    public ReorderListingImagesHandler(
        IListingRepository listingRepository,
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _listingRepository = listingRepository;
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<ReorderListingImagesResult> Handle(
        ReorderListingImagesCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ImageIds.Count == 0)
        {
            return ReorderListingImagesResult.Failure(ReorderListingImagesError.ImageIdsMissing);
        }

        if (command.ImageIds.Count != command.ImageIds.Distinct().Count())
        {
            return ReorderListingImagesResult.Failure(
                ReorderListingImagesError.DuplicateImageIds);
        }

        IListingImageWriteScope? writeScope =
            await _listingRepository.BeginListingImageWriteAsync(
            command.ListingId,
            cancellationToken);

        if (writeScope is null)
        {
            return ReorderListingImagesResult.Failure(ReorderListingImagesError.ListingNotFound);
        }

        List<Domain.Entities.ListingImage> orderedImages;

        await using (writeScope)
        {
            var listing = writeScope.Listing;

            Guid? currentUserId = _currentUserService.UserId;

            if (!currentUserId.HasValue)
            {
                return ReorderListingImagesResult.Failure(
                    ReorderListingImagesError.InvalidPrincipal);
            }

            Guid userId = currentUserId.Value;

            var actor =
                await _userRepository.GetByIdReadOnlyAsync(
                    userId,
                    cancellationToken);

            if (actor is null)
            {
                return ReorderListingImagesResult.Failure(
                    ReorderListingImagesError.InvalidPrincipal);
            }

            if (actor.Status == UserStatus.Disabled)
            {
                return ReorderListingImagesResult.Failure(
                    ReorderListingImagesError.AccountDisabled);
            }

            if (listing.CreatedByUserId != userId)
            {
                return ReorderListingImagesResult.Failure(
                    ReorderListingImagesError.NotListingOwner);
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

            await writeScope.CommitAsync(cancellationToken);

            orderedImages = listing.Images
                .OrderBy(image => image.SortOrder)
                .ToList();
        }

        var response = orderedImages
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
