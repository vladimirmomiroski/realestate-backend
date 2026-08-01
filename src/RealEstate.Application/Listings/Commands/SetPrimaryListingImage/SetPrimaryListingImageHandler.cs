using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Commands.UploadListingImage;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.SetPrimaryListingImage;

public sealed class SetPrimaryListingImageHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IListingRepository _listingRepository;
    private readonly IUserRepository _userRepository;

    public SetPrimaryListingImageHandler(
        IListingRepository listingRepository,
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _listingRepository = listingRepository;
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<SetPrimaryListingImageResult> Handle(
        SetPrimaryListingImageCommand command,
        CancellationToken cancellationToken)
    {
        IListingImageWriteScope? writeScope =
            await _listingRepository.BeginListingImageWriteAsync(
            command.ListingId,
            cancellationToken);

        if (writeScope is null)
        {
            return SetPrimaryListingImageResult.Failure(SetPrimaryListingImageError.ListingNotFound);
        }

        Domain.Entities.ListingImage selectedImage;

        await using (writeScope)
        {
            var listing = writeScope.Listing;

            Guid? currentUserId = _currentUserService.UserId;

            if (!currentUserId.HasValue)
            {
                return SetPrimaryListingImageResult.Failure(
                    SetPrimaryListingImageError.InvalidPrincipal);
            }

            Guid userId = currentUserId.Value;

            var actor =
                await _userRepository.GetByIdReadOnlyAsync(
                    userId,
                    cancellationToken);

            if (actor is null)
            {
                return SetPrimaryListingImageResult.Failure(
                    SetPrimaryListingImageError.InvalidPrincipal);
            }

            if (actor.Status == UserStatus.Disabled)
            {
                return SetPrimaryListingImageResult.Failure(
                    SetPrimaryListingImageError.AccountDisabled);
            }

            if (listing.CreatedByUserId != userId)
            {
                return SetPrimaryListingImageResult.Failure(
                    SetPrimaryListingImageError.NotListingOwner);
            }

            var protectedSelectedImage = listing.Images.FirstOrDefault(
                image => image.Id == command.ImageId);

            if (protectedSelectedImage is null)
            {
                return SetPrimaryListingImageResult.Failure(SetPrimaryListingImageError.ImageNotFound);
            }

            selectedImage = protectedSelectedImage;

            if (!selectedImage.IsPrimary)
            {
                foreach (var image in listing.Images)
                {
                    image.IsPrimary = false;
                }

                // Save in two phases because the database enforces only one primary image per listing.
                // A single SaveChanges call can fail if EF updates the new primary before clearing the old one.
                await _listingRepository.SaveChangesAsync(cancellationToken);

                selectedImage.IsPrimary = true;

                await _listingRepository.SaveChangesAsync(cancellationToken);

                await writeScope.CommitAsync(cancellationToken);
            }
        }

        return SetPrimaryListingImageResult.Success(ToResponse(selectedImage));
    }

    private static ListingImageResponse ToResponse(Domain.Entities.ListingImage image)
    {
        return new ListingImageResponse
        {
            Id = image.Id,
            Url = image.Url,
            ContentType = image.ContentType,
            SizeBytes = image.SizeBytes,
            SortOrder = image.SortOrder,
            IsPrimary = image.IsPrimary
        };
    }
}
