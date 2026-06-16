using RealEstate.Application.Common.Storage;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Commands.UploadListingImage;

public sealed class UploadListingImageHandler
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private const int MaxImagesPerListing = 20;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly IListingRepository _listingRepository;
    private readonly IFileStorageService _fileStorageService;

    public UploadListingImageHandler(
        IListingRepository listingRepository,
        IFileStorageService fileStorageService)
    {
        _listingRepository = listingRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<UploadListingImageResult> Handle(
        UploadListingImageCommand command,
        CancellationToken cancellationToken)
    {
        var fileValidationError = ValidateFile(command.File);

        if (fileValidationError is not UploadListingImageError.None)
        {
            return UploadListingImageResult.Failure(fileValidationError);
        }

        var listing = await _listingRepository.GetByIdWithImagesForUpdateAsync(
            command.ListingId,
            cancellationToken);

        if (listing is null)
        {
            return UploadListingImageResult.Failure(UploadListingImageError.ListingNotFound);
        }

        if (listing.Images.Count >= MaxImagesPerListing)
        {
            return UploadListingImageResult.Failure(UploadListingImageError.ImageLimitReached);
        }

        var storedFile = await _fileStorageService.SaveListingImageAsync(
            listing.Id,
            command.File!,
            cancellationToken);

        var sortOrder = listing.Images.Count == 0
            ? 0
            : listing.Images.Max(image => image.SortOrder) + 1;

        var isPrimary = listing.Images.Count == 0;

        var image = new ListingImage
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            OriginalFileName = storedFile.OriginalFileName,
            StoredFileName = storedFile.StoredFileName,
            ContentType = storedFile.ContentType,
            SizeBytes = storedFile.SizeBytes,
            Url = storedFile.Url,
            SortOrder = sortOrder,
            IsPrimary = isPrimary
        };


        _listingRepository.AddListingImage(image);

        await _listingRepository.SaveChangesAsync(cancellationToken);

        var response = new ListingImageResponse
        {
            Id = image.Id,
            Url = image.Url,
            ContentType = image.ContentType,
            SizeBytes = image.SizeBytes,
            SortOrder = image.SortOrder,
            IsPrimary = image.IsPrimary
        };

        return UploadListingImageResult.Success(response);
    }

    private static UploadListingImageError ValidateFile(
        Common.Files.UploadedFile? file)
    {
        if (file is null)
        {
            return UploadListingImageError.FileMissing;
        }

        if (file.Length <= 0)
        {
            return UploadListingImageError.FileEmpty;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return UploadListingImageError.FileTooLarge;
        }

        var extension = Path.GetExtension(file.FileName);

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return UploadListingImageError.InvalidFileType;
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !AllowedContentTypes.Contains(file.ContentType))
        {
            return UploadListingImageError.InvalidFileType;
        }

        return UploadListingImageError.None;
    }
}
