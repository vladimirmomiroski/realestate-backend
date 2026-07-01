using Microsoft.Extensions.Options;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Common.Storage;

namespace RealEstate.Infrastructure.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly LocalFileStorageOptions _options;

    public LocalFileStorageService(IOptions<LocalFileStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<StoredFileResult> SaveListingImageAsync(
        Guid listingId,
        UploadedFile file,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new InvalidOperationException("Local file storage root path is not configured.");
        }

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();

        var storedFileName = $"{Guid.NewGuid():N}{extension}";

        var listingFolderPath = Path.Combine(
            _options.RootPath,
            "listings",
            listingId.ToString());

        Directory.CreateDirectory(listingFolderPath);

        var filePath = Path.Combine(listingFolderPath, storedFileName);

        await using var outputStream = File.Create(filePath);

        await file.Content.CopyToAsync(outputStream, cancellationToken);

        var url = $"{_options.PublicBasePath.TrimEnd('/')}/listings/{listingId}/{storedFileName}";

        return new StoredFileResult(
            originalFileName,
            storedFileName,
            file.ContentType,
            file.Length,
            url);
    }

    public Task DeleteListingImageAsync(
    Guid listingId,
    string storedFileName,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new InvalidOperationException("Local file storage root path is not configured.");
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
}
