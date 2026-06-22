using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Common.Storage;
using RealEstate.Application.Listings.Repositories;

namespace RealEstate.Application.Listings.Commands.DeleteListingImage;

public sealed class DeleteListingImageHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IListingRepository _listingRepository;
    private readonly IFileStorageService _fileStorageService;

    public DeleteListingImageHandler(
        IListingRepository listingRepository,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService)
    {
        _listingRepository = listingRepository;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
    }

    public async Task<DeleteListingImageResult> Handle(
        DeleteListingImageCommand command,
        CancellationToken cancellationToken)
    {
        var listing = await _listingRepository.GetByIdWithImagesForUpdateAsync(
            command.ListingId,
            cancellationToken);

        if (listing is null)
        {
            return DeleteListingImageResult.Failure(DeleteListingImageError.ListingNotFound);
        }

        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        if (listing.CreatedByUserId != userId)
        {
            return DeleteListingImageResult.Failure(DeleteListingImageError.NotListingOwner);
        }

        var image = listing.Images.FirstOrDefault(image => image.Id == command.ImageId);

        if (image is null)
        {
            return DeleteListingImageResult.Failure(DeleteListingImageError.ImageNotFound);
        }

        var wasPrimary = image.IsPrimary;
        var storedFileName = image.StoredFileName;

        var nextPrimaryImage = wasPrimary
            ? listing.Images
                .Where(existingImage => existingImage.Id != image.Id)
                .OrderBy(existingImage => existingImage.SortOrder)
                .FirstOrDefault()
            : null;

        _listingRepository.RemoveListingImage(image);

        await _listingRepository.SaveChangesAsync(cancellationToken);

        if (nextPrimaryImage is not null)
        {
            nextPrimaryImage.IsPrimary = true;

            await _listingRepository.SaveChangesAsync(cancellationToken);
        }

        await _fileStorageService.DeleteListingImageAsync(
            listing.Id,
            storedFileName,
            cancellationToken);

        return DeleteListingImageResult.Success();
    }
}