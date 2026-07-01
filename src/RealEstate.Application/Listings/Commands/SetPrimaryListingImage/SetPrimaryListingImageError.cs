namespace RealEstate.Application.Listings.Commands.SetPrimaryListingImage;

public enum SetPrimaryListingImageError
{
    None,
    ListingNotFound,
    NotListingOwner,
    ImageNotFound
}