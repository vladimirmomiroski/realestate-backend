namespace RealEstate.Application.Listings.Commands.SetPrimaryListingImage;

public enum SetPrimaryListingImageError
{
    None,
    ListingNotFound,
    InvalidPrincipal,
    AccountDisabled,
    NotListingOwner,
    ImageNotFound
}
