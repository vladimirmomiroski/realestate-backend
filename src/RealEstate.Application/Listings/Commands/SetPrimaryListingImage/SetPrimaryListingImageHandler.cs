using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Repositories;

namespace RealEstate.Application.Listings.Commands.SetPrimaryListingImage;

public sealed class SetPrimaryListingImageHandler
{
    private readonly IListingRepository _listingRepository;

    public SetPrimaryListingImageHandler(IListingRepository listingRepository)
    {
        _listingRepository = listingRepository;
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