using RealEstate.Application.Common.Files;

namespace RealEstate.Application.Common.Storage;

public interface IFileStorageService
{
    Task<StoredFileResult> SaveListingImageAsync(
        Guid listingId,
        UploadedFile file,
        CancellationToken cancellationToken);

    Task DeleteListingImageAsync(
        Guid listingId,
        string storedFileName,
        CancellationToken cancellationToken);
}
