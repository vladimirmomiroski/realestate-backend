using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Common.Storage;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.DeleteListingImage;

public sealed class DeleteListingImageHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IListingRepository _listingRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUserRepository _userRepository;

    public DeleteListingImageHandler(
        IListingRepository listingRepository,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _listingRepository = listingRepository;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<DeleteListingImageResult> Handle(
        DeleteListingImageCommand command,
        CancellationToken cancellationToken)
    {
        IListingImageWriteScope? writeScope =
            await _listingRepository.BeginListingImageWriteAsync(
            command.ListingId,
            cancellationToken);

        if (writeScope is null)
        {
            return DeleteListingImageResult.Failure(DeleteListingImageError.ListingNotFound);
        }

        Guid listingId;
        string storedFileName;

        await using (writeScope)
        {
            var listing = writeScope.Listing;

            Guid? currentUserId = _currentUserService.UserId;

            if (!currentUserId.HasValue)
            {
                return DeleteListingImageResult.Failure(
                    DeleteListingImageError.InvalidPrincipal);
            }

            Guid userId = currentUserId.Value;

            var actor =
                await _userRepository.GetByIdReadOnlyAsync(
                    userId,
                    cancellationToken);

            if (actor is null)
            {
                return DeleteListingImageResult.Failure(
                    DeleteListingImageError.InvalidPrincipal);
            }

            if (actor.Status == UserStatus.Disabled)
            {
                return DeleteListingImageResult.Failure(
                    DeleteListingImageError.AccountDisabled);
            }

            if (listing.CreatedByUserId != userId)
            {
                return DeleteListingImageResult.Failure(
                    DeleteListingImageError.NotListingOwner);
            }

            var image = listing.Images.FirstOrDefault(image => image.Id == command.ImageId);

            if (image is null)
            {
                return DeleteListingImageResult.Failure(DeleteListingImageError.ImageNotFound);
            }

            listingId = listing.Id;
            storedFileName = image.StoredFileName;

            var nextPrimaryImage = image.IsPrimary
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

            await writeScope.CommitAsync(cancellationToken);
        }

        await _fileStorageService.DeleteListingImageAsync(
            listingId,
            storedFileName,
            cancellationToken);

        return DeleteListingImageResult.Success();
    }
}
