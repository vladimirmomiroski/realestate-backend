using RealEstate.Application.Listings.Dtos;

namespace RealEstate.Application.Listings.Commands.UploadListingImage;

public sealed class UploadListingImageResult
{
    private UploadListingImageResult(
        ListingImageResponse? image,
        UploadListingImageError error)
    {
        Image = image;
        Error = error;
    }

    public ListingImageResponse? Image { get; }

    public UploadListingImageError Error { get; }

    public bool Succeeded => Error == UploadListingImageError.None;

    public static UploadListingImageResult Success(ListingImageResponse image)
    {
        return new UploadListingImageResult(image, UploadListingImageError.None);
    }

    public static UploadListingImageResult Failure(UploadListingImageError error)
    {
        return new UploadListingImageResult(null, error);
    }
}
