namespace RealEstate.Application.Listings.Commands.UploadListingImage;

public enum UploadListingImageError
{
    None,
    ListingNotFound,
    FileMissing,
    FileEmpty,
    FileTooLarge,
    InvalidFileType,
    ImageLimitReached
}
