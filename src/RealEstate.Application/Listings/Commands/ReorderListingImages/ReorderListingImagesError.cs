namespace RealEstate.Application.Listings.Commands.ReorderListingImages;

public enum ReorderListingImagesError
{
    None,
    ListingNotFound,
    InvalidPrincipal,
    AccountDisabled,
    NotListingOwner,
    ImageIdsMissing,
    DuplicateImageIds,
    ImageSetMismatch
}
