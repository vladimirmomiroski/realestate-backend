namespace RealEstate.Application.Listings.Commands.DeleteListingImage;

public enum DeleteListingImageError
{
    None,
    ListingNotFound,
    InvalidPrincipal,
    AccountDisabled,
    NotListingOwner,
    ImageNotFound
}
