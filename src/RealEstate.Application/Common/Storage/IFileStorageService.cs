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

    Task<StoredFileResult> SaveUserAvatarAsync(
        Guid userId,
        UploadedFile file,
        CancellationToken cancellationToken);

    Task DeleteUserAvatarAsync(
        Guid userId,
        string storedFileName,
        CancellationToken cancellationToken);
}
