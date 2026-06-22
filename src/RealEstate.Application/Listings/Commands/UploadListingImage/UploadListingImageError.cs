namespace RealEstate.Application.Listings.Commands.UploadListingImage;

public enum UploadListingImageError
{
    None,
    ListingNotFound,
    NotListingOwner,
    FileMissing,
    FileEmpty,
    FileTooLarge,
    InvalidFileType,
    ImageLimitReached
}
