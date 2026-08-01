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
        ArgumentNullException.ThrowIfNull(image);

        return new UploadListingImageResult(image, UploadListingImageError.None);
    }

    public static UploadListingImageResult Failure(UploadListingImageError error)
    {
        if (error == UploadListingImageError.None || !Enum.IsDefined(error))
        {
            throw new ArgumentOutOfRangeException(
                nameof(error),
                error,
                "A failure result requires a defined non-success error.");
        }

        return new UploadListingImageResult(null, error);
    }
}
