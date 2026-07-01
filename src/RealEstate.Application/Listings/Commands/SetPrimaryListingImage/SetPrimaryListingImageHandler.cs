using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Listings.Commands.UploadListingImage;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Repositories;

namespace RealEstate.Application.Listings.Commands.SetPrimaryListingImage;

public sealed class SetPrimaryListingImageHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IListingRepository _listingRepository;

    public SetPrimaryListingImageHandler(IListingRepository listingRepository, ICurrentUserService currentUserService)
    {
        _listingRepository = listingRepository;
        _currentUserService = currentUserService;
    }

    public async Task<SetPrimaryListingImageResult> Handle(
        SetPrimaryListingImageCommand command,
        CancellationToken cancellationToken)
    {
        var listing = await _listingRepository.GetByIdWithImagesForUpdateAsync(
            command.ListingId,
            cancellationToken);

        if (listing is null)
        {
            return SetPrimaryListingImageResult.Failure(SetPrimaryListingImageError.ListingNotFound);
        }

        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException("Authenticated user id is not available.");

        if (listing.CreatedByUserId != userId)
        {
            return SetPrimaryListingImageResult.Failure(SetPrimaryListingImageError.NotListingOwner);
        }

        var selectedImage = listing.Images.FirstOrDefault(image => image.Id == command.ImageId);

        if (selectedImage is null)
        {
            return SetPrimaryListingImageResult.Failure(SetPrimaryListingImageError.ImageNotFound);
        }

        if (selectedImage.IsPrimary)
        {
            return SetPrimaryListingImageResult.Success(ToResponse(selectedImage));
        }

        foreach (var image in listing.Images)
        {
            image.IsPrimary = false;
        }

        // Save in two phases because the database enforces only one primary image per listing.
        // A single SaveChanges call can fail if EF updates the new primary before clearing the old one.
        await _listingRepository.SaveChangesAsync(cancellationToken);

        selectedImage.IsPrimary = true;

        await _listingRepository.SaveChangesAsync(cancellationToken);

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