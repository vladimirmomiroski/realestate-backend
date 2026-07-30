using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Common.Storage;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Commands.UploadAgencyLogo;

public sealed class UploadAgencyLogoHandler
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

    private readonly AgencyAdminAccessChecker _agencyAdminAccessChecker;
    private readonly IAgencyRepository _agencyRepository;
    private readonly IFileStorageService _fileStorageService;

    public UploadAgencyLogoHandler(
        AgencyAdminAccessChecker agencyAdminAccessChecker,
        IAgencyRepository agencyRepository,
        IFileStorageService fileStorageService)
    {
        _agencyAdminAccessChecker = agencyAdminAccessChecker;
        _agencyRepository = agencyRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<ServiceResult<AgencyResponse>> HandleAsync(
        UploadAgencyLogoCommand command,
        CancellationToken cancellationToken)
    {
        AgencyAdminAccessResult<AgencyResponse> accessResult =
            await _agencyAdminAccessChecker
                .EnsureCurrentUserIsActiveOwnerAsync<AgencyResponse>(
                    command.AgencyId,
                    "Only active agency owners can update the agency logo.",
                    cancellationToken);

        if (accessResult.HasFailure)
        {
            return accessResult.Failure!;
        }

        FileValidationFailure? validationFailure = ValidateFile(command.File);

        if (validationFailure is not null)
        {
            return ServiceResult<AgencyResponse>.ValidationError(
                validationFailure.Error,
                "file",
                validationFailure.ErrorCode);
        }

        Agency? agency = await _agencyRepository.GetByIdForUpdateAsync(
            command.AgencyId,
            cancellationToken);

        if (agency is null)
        {
            return ServiceResult<AgencyResponse>.NotFound(
                "Agency was not found.",
                ErrorCodes.ResourceNotFound);
        }

        string? oldStoredFileName = agency.LogoStoredFileName;

        StoredFileResult storedFile =
            await _fileStorageService.SaveAgencyLogoAsync(
                agency.Id,
                command.File!,
                cancellationToken);

        try
        {
            agency.SetLogo(
                storedFile.Url,
                storedFile.StoredFileName,
                storedFile.ContentType,
                storedFile.SizeBytes);

            await _agencyRepository.SaveChangesAsync(
                cancellationToken);
        }
        catch
        {
            await _fileStorageService.DeleteAgencyLogoAsync(
                agency.Id,
                storedFile.StoredFileName,
                cancellationToken);

            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldStoredFileName))
        {
            await _fileStorageService.DeleteAgencyLogoAsync(
                agency.Id,
                oldStoredFileName,
                cancellationToken);
        }

        return ServiceResult<AgencyResponse>.Success(
            agency.ToResponse());
    }

    private static FileValidationFailure? ValidateFile(UploadedFile? file)
    {
        if (file is null)
        {
            return new FileValidationFailure(
                ErrorCodes.ValidationFileRequired,
                "Logo file is required.");
        }

        if (file.Length <= 0)
        {
            return new FileValidationFailure(
                ErrorCodes.ValidationFileEmpty,
                "Logo file is empty.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return new FileValidationFailure(
                ErrorCodes.ValidationFileTooLarge,
                "Logo file cannot be larger than 5 MB.");
        }

        string extension = Path.GetExtension(file.FileName);

        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedExtensions.Contains(extension))
        {
            return UnsupportedFileType();
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !AllowedContentTypes.Contains(file.ContentType))
        {
            return UnsupportedFileType();
        }

        return null;
    }

    private static FileValidationFailure UnsupportedFileType()
    {
        return new FileValidationFailure(
            ErrorCodes.ValidationFileTypeNotSupported,
            "Only JPG, JPEG, PNG, and WEBP images are allowed.");
    }

    private sealed record FileValidationFailure(
        string ErrorCode,
        string Error);
}
