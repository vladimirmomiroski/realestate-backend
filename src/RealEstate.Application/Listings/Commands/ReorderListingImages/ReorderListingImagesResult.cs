using RealEstate.Application.Listings.Dtos;

namespace RealEstate.Application.Listings.Commands.ReorderListingImages;

public sealed class ReorderListingImagesResult
{
    private ReorderListingImagesResult(
        List<ListingImageResponse> images,
        ReorderListingImagesError error)
    {
        Images = images;
        Error = error;
    }

    public List<ListingImageResponse> Images { get; }

    public ReorderListingImagesError Error { get; }

    public bool Succeeded => Error == ReorderListingImagesError.None;

    public static ReorderListingImagesResult Success(List<ListingImageResponse> images)
    {
        ArgumentNullException.ThrowIfNull(images);

        if (images.Count == 0)
        {
            throw new ArgumentException(
                "A successful reorder result requires at least one image.",
                nameof(images));
        }

        return new ReorderListingImagesResult(images, ReorderListingImagesError.None);
    }

    public static ReorderListingImagesResult Failure(ReorderListingImagesError error)
    {
        if (error == ReorderListingImagesError.None || !Enum.IsDefined(error))
        {
            throw new ArgumentOutOfRangeException(
                nameof(error),
                error,
                "A failure result requires a defined non-success error.");
        }

        return new ReorderListingImagesResult(new List<ListingImageResponse>(), error);
    }
}
