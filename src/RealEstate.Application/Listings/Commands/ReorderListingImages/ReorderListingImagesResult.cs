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
        return new ReorderListingImagesResult(images, ReorderListingImagesError.None);
    }

    public static ReorderListingImagesResult Failure(ReorderListingImagesError error)
    {
        return new ReorderListingImagesResult(new List<ListingImageResponse>(), error);
    }
}
