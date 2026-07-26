using RealEstate.Application.Common.Storage;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Commands.UploadListingImage;

public sealed class UploadListingImageHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
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
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _listingRepository = listingRepository;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<UploadListingImageResult> Handle(
    UploadListingImageCommand command,
    CancellationToken cancellationToken)
    {
        var fileValidationError = ValidateFile(command.File);

        if (fileValidationError is not UploadListingImageError.None)
        {
            return UploadListingImageResult.Failure(
                fileValidationError);
        }

        ListingImageUploadProbeReadModel? uploadProbe =
            await _listingRepository
                .GetListingImageUploadProbeReadOnlyAsync(
                    command.ListingId,
                    cancellationToken);

        if (uploadProbe is null)
        {
            return UploadListingImageResult.Failure(
                UploadListingImageError.ListingNotFound);
        }

        Guid userId = _currentUserService.UserId
            ?? throw new InvalidOperationException(
                "Authenticated user id is not available.");

        User? actor =
            await _userRepository.GetByIdReadOnlyAsync(
                userId,
                cancellationToken);

        if (actor is null ||
            actor.Status == UserStatus.Disabled ||
            uploadProbe.CreatedByUserId != userId)
        {
            return UploadListingImageResult.Failure(
                UploadListingImageError.NotListingOwner);
        }

        if (uploadProbe.ImageCount >= MaxImagesPerListing)
        {
            return UploadListingImageResult.Failure(
                UploadListingImageError.ImageLimitReached);
        }

        StoredFileResult storedFile =
            await _fileStorageService.SaveListingImageAsync(
                uploadProbe.ListingId,
                command.File!,
                cancellationToken);

        ListingImage? image = null;

        UploadListingImageError protectedError =
            UploadListingImageError.None;

        bool commitReturned = false;

        try
        {
            IListingImageWriteScope? writeScope =
                await _listingRepository
                    .BeginListingImageWriteAsync(
                        uploadProbe.ListingId,
                        cancellationToken);

            if (writeScope is null)
            {
                protectedError =
                    UploadListingImageError.ListingNotFound;
            }
            else
            {
                await using (writeScope)
                {
                    Listing protectedListing =
                        writeScope.Listing;

                    User? protectedActor =
                        await _userRepository.GetByIdReadOnlyAsync(
                            userId,
                            cancellationToken);

                    if (protectedActor is null ||
                        protectedActor.Status == UserStatus.Disabled ||
                        protectedListing.CreatedByUserId != userId)
                    {
                        protectedError =
                            UploadListingImageError.NotListingOwner;
                    }
                    else if (
                        protectedListing.Images.Count >=
                        MaxImagesPerListing)
                    {
                        protectedError =
                            UploadListingImageError.ImageLimitReached;
                    }
                    else
                    {
                        int sortOrder =
                            protectedListing.Images.Count == 0
                                ? 0
                                : protectedListing.Images
                                    .Max(existingImage =>
                                        existingImage.SortOrder) + 1;

                        bool isPrimary =
                            protectedListing.Images.Count == 0;

                        image = new ListingImage
                        {
                            Id = Guid.NewGuid(),
                            ListingId = protectedListing.Id,
                            OriginalFileName =
                                storedFile.OriginalFileName,
                            StoredFileName =
                                storedFile.StoredFileName,
                            ContentType =
                                storedFile.ContentType,
                            SizeBytes =
                                storedFile.SizeBytes,
                            Url =
                                storedFile.Url,
                            SortOrder =
                                sortOrder,
                            IsPrimary =
                                isPrimary
                        };

                        _listingRepository.AddListingImage(
                            image);

                        await _listingRepository.SaveChangesAsync(
                            cancellationToken);

                        await writeScope.CommitAsync(
                            cancellationToken);

                        commitReturned = true;
                    }
                }
            }
        }
        catch (Exception persistenceFailure)
            when (!commitReturned)
        {
            try
            {
                await _fileStorageService
                    .DeleteListingImageAsync(
                        uploadProbe.ListingId,
                        storedFile.StoredFileName,
                        CancellationToken.None);
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    persistenceFailure,
                    cleanupFailure);
            }

            throw;
        }

        if (protectedError is not UploadListingImageError.None)
        {
            await _fileStorageService.DeleteListingImageAsync(
                uploadProbe.ListingId,
                storedFile.StoredFileName,
                CancellationToken.None);

            return UploadListingImageResult.Failure(
                protectedError);
        }

        if (image is null)
        {
            throw new InvalidOperationException(
                "Listing image persistence completed without an image.");
        }

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
