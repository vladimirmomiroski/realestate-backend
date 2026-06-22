namespace RealEstate.Application.Listings.Commands.ReorderListingImages;

public enum ReorderListingImagesError
{
    None,
    ListingNotFound,
    NotListingOwner,
    ImageIdsMissing,
    ImageSetMismatch
}