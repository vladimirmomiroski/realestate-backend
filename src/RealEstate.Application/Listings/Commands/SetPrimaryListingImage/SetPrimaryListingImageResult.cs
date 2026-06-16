using RealEstate.Application.Listings.Dtos;

namespace RealEstate.Application.Listings.Commands.SetPrimaryListingImage;

public sealed class SetPrimaryListingImageResult
{
    private SetPrimaryListingImageResult(
        ListingImageResponse? image,
        SetPrimaryListingImageError error)
    {
        Image = image;
        Error = error;
    }

    public ListingImageResponse? Image { get; }

    public SetPrimaryListingImageError Error { get; }

    public bool Succeeded => Error == SetPrimaryListingImageError.None;

    public static SetPrimaryListingImageResult Success(ListingImageResponse image)
    {
        return new SetPrimaryListingImageResult(image, SetPrimaryListingImageError.None);
    }

    public static SetPrimaryListingImageResult Failure(SetPrimaryListingImageError error)
    {
        return new SetPrimaryListingImageResult(null, error);
    }
}