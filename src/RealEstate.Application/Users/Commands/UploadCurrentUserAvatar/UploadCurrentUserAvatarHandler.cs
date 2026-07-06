using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Common.Storage;
using RealEstate.Application.Users.Dtos;
using RealEstate.Application.Users.Mappings;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Users.Commands.UploadCurrentUserAvatar;

public sealed class UploadCurrentUserAvatarHandler
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

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

    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _fileStorageService;

    public UploadCurrentUserAvatarHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IFileStorageService fileStorageService)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<ServiceResult<UserProfileResponse>> HandleAsync(
        UploadCurrentUserAvatarCommand command,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return ServiceResult<UserProfileResponse>.Unauthorized(
                "Current user could not be resolved.");
        }

        string? validationError = ValidateFile(command.File);

        if (validationError is not null)
        {
            return ServiceResult<UserProfileResponse>.ValidationError(validationError);
        }

        var user = await _userRepository.GetByIdForUpdateAsync(
            _currentUserService.UserId.Value,
            cancellationToken);

        if (user is null)
        {
            return ServiceResult<UserProfileResponse>.Unauthorized(
                "Current user could not be resolved.");
        }

        if (user.Status == UserStatus.Disabled)
        {
            return ServiceResult<UserProfileResponse>.Forbidden(
                "Disabled users cannot update avatar.");
        }

        string? oldStoredFileName = user.AvatarStoredFileName;

        StoredFileResult storedFile = await _fileStorageService.SaveUserAvatarAsync(
            user.Id,
            command.File!,
            cancellationToken);

        try
        {
            user.SetAvatar(
                storedFile.Url,
                storedFile.StoredFileName,
                storedFile.ContentType,
                storedFile.SizeBytes);

            await _userRepository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _fileStorageService.DeleteUserAvatarAsync(
                user.Id,
                storedFile.StoredFileName,
                cancellationToken);

            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldStoredFileName))
        {
            await _fileStorageService.DeleteUserAvatarAsync(
                user.Id,
                oldStoredFileName,
                cancellationToken);
        }

        return ServiceResult<UserProfileResponse>.Success(
            user.ToProfileResponse());
    }

    private static string? ValidateFile(UploadedFile? file)
    {
        if (file is null)
        {
            return "Avatar file is required.";
        }

        if (file.Length <= 0)
        {
            return "Avatar file is empty.";
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return "Avatar file cannot be larger than 5 MB.";
        }

        var extension = Path.GetExtension(file.FileName);

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return "Only JPG, JPEG, PNG, and WEBP images are allowed.";
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !AllowedContentTypes.Contains(file.ContentType))
        {
            return "Only JPG, JPEG, PNG, and WEBP images are allowed.";
        }

        return null;
    }
}
