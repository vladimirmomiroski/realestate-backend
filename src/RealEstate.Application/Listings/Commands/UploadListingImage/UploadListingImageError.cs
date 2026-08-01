namespace RealEstate.Application.Listings.Commands.UploadListingImage;

public enum UploadListingImageError
{
    None,
    ListingNotFound,
    InvalidPrincipal,
    AccountDisabled,
    NotListingOwner,
    FileMissing,
    FileEmpty,
    FileTooLarge,
    InvalidFileType,
    ImageLimitReached
}
