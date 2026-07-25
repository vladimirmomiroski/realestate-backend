using Microsoft.Extensions.Options;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Common.Storage;

namespace RealEstate.Infrastructure.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly LocalFileStorageOptions _options;

    public LocalFileStorageService(
        IOptions<LocalFileStorageOptions> options)
    {
        _options = options.Value;
    }

    public Task<StoredFileResult> SaveListingImageAsync(
        Guid listingId,
        UploadedFile file,
        CancellationToken cancellationToken)
    {
        return SaveFileAsync(
            file,
            Path.Combine(
                "listings",
                listingId.ToString()),
            $"listings/{listingId}",
            cancellationToken);
    }

    public Task DeleteListingImageAsync(
        Guid listingId,
        string storedFileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new InvalidOperationException(
                "Local file storage root path is not configured.");
        }

        var safeFileName = Path.GetFileName(storedFileName);

        var filePath = Path.Combine(
            _options.RootPath,
            "listings",
            listingId.ToString(),
            safeFileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public Task<StoredFileResult> SaveUserAvatarAsync(
        Guid userId,
        UploadedFile file,
        CancellationToken cancellationToken)
    {
        return SaveFileAsync(
            file,
            Path.Combine(
                "users",
                userId.ToString(),
                "avatar"),
            $"users/{userId}/avatar",
            cancellationToken);
    }

    public Task DeleteUserAvatarAsync(
        Guid userId,
        string storedFileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new InvalidOperationException(
                "Local file storage root path is not configured.");
        }

        var safeFileName = Path.GetFileName(storedFileName);

        var filePath = Path.Combine(
            _options.RootPath,
            "users",
            userId.ToString(),
            "avatar",
            safeFileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public Task<StoredFileResult> SaveAgencyLogoAsync(
        Guid agencyId,
        UploadedFile file,
        CancellationToken cancellationToken)
    {
        return SaveFileAsync(
            file,
            Path.Combine(
                "agencies",
                agencyId.ToString(),
                "logo"),
            $"agencies/{agencyId}/logo",
            cancellationToken);
    }

    public Task DeleteAgencyLogoAsync(
        Guid agencyId,
        string storedFileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new InvalidOperationException(
                "Local file storage root path is not configured.");
        }

        var safeFileName = Path.GetFileName(storedFileName);

        var filePath = Path.Combine(
            _options.RootPath,
            "agencies",
            agencyId.ToString(),
            "logo",
            safeFileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private async Task<StoredFileResult> SaveFileAsync(
        UploadedFile file,
        string relativeFolderPath,
        string relativeUrlPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new InvalidOperationException(
                "Local file storage root path is not configured.");
        }

        string originalFileName =
            Path.GetFileName(file.FileName);

        string extension =
            Path.GetExtension(originalFileName)
                .ToLowerInvariant();

        string storedFileName =
            $"{Guid.NewGuid():N}{extension}";

        string destinationDirectory =
            Path.Combine(
                _options.RootPath,
                relativeFolderPath);

        Directory.CreateDirectory(destinationDirectory);

        string finalPath =
            Path.Combine(
                destinationDirectory,
                storedFileName);

        string temporaryFileName =
            $".{storedFileName}.{Guid.NewGuid():N}.tmp";

        string temporaryPath =
            Path.Combine(
                destinationDirectory,
                temporaryFileName);

        bool ownsTemporaryFile = false;

        try
        {
            await using (var outputStream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                useAsync: true))
            {
                ownsTemporaryFile = true;

                await file.Content.CopyToAsync(
                    outputStream,
                    cancellationToken);
            }

            File.Move(
                temporaryPath,
                finalPath);

            ownsTemporaryFile = false;
        }
        catch
        {
            if (ownsTemporaryFile &&
                File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }

        string url =
            $"{_options.PublicBasePath.TrimEnd('/')}" +
            $"/{relativeUrlPath}/{storedFileName}";

        return new StoredFileResult(
            originalFileName,
            storedFileName,
            file.ContentType,
            file.Length,
            url);
    }
}
