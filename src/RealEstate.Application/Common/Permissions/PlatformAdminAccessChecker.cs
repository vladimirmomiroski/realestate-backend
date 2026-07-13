using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Common.Permissions;

public sealed class PlatformAdminAccessChecker
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public PlatformAdminAccessChecker(
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<ServiceResult<TResponse>?>
        EnsureCurrentUserIsActiveAdminAsync<TResponse>(
            string forbiddenMessage,
            CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            _currentUserService.UserId is not Guid currentUserId)
        {
            return ServiceResult<TResponse>.Unauthorized(
                "Current user could not be resolved.");
        }

        User? currentUser =
            await _userRepository.GetByIdReadOnlyAsync(
                currentUserId,
                cancellationToken);

        if (currentUser is null)
        {
            return ServiceResult<TResponse>.Unauthorized(
                "Current user could not be resolved.");
        }

        if (currentUser.Role != UserRole.Admin ||
            currentUser.Status != UserStatus.Active)
        {
            return ServiceResult<TResponse>.Forbidden(
                forbiddenMessage);
        }

        return null;
    }
}
