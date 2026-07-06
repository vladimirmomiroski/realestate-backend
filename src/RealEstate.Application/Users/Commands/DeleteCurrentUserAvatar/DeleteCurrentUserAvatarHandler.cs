using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Common.Storage;
using RealEstate.Application.Users.Dtos;
using RealEstate.Application.Users.Mappings;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Users.Commands.DeleteCurrentUserAvatar;

public sealed class DeleteCurrentUserAvatarHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _fileStorageService;

    public DeleteCurrentUserAvatarHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IFileStorageService fileStorageService)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<ServiceResult<UserProfileResponse>> HandleAsync(
        DeleteCurrentUserAvatarCommand command,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return ServiceResult<UserProfileResponse>.Unauthorized(
                "Current user could not be resolved.");
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
                "Disabled users cannot delete avatar.");
        }

        string? oldStoredFileName = user.AvatarStoredFileName;

        user.RemoveAvatar();

        await _userRepository.SaveChangesAsync(cancellationToken);

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
}
