using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Permissions;

public sealed class AgencyAdminAccessChecker
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IAgencyRepository _agencyRepository;

    public AgencyAdminAccessChecker(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IAgencyRepository agencyRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _agencyRepository = agencyRepository;
    }

    public async Task<AgencyAdminAccessResult<TResponse>> EnsureCurrentUserIsActiveOwnerAsync<TResponse>(
        Guid agencyId,
        string forbiddenMessage,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            _currentUserService.UserId is not Guid currentUserId)
        {
            return AgencyAdminAccessResult<TResponse>.Failed(
                ServiceResult<TResponse>.Unauthorized(
                    "Current user could not be resolved."));
        }

        User? currentUser = await _userRepository.GetByIdReadOnlyAsync(
            currentUserId,
            cancellationToken);

        if (currentUser is null)
        {
            return AgencyAdminAccessResult<TResponse>.Failed(
                ServiceResult<TResponse>.Unauthorized(
                    "Current user could not be resolved."));
        }

        if (currentUser.Status == UserStatus.Disabled)
        {
            return AgencyAdminAccessResult<TResponse>.Failed(
                ServiceResult<TResponse>.Forbidden(forbiddenMessage));
        }

        bool agencyExists = await _agencyRepository.ExistsAsync(
            agencyId,
            cancellationToken);

        if (!agencyExists)
        {
            return AgencyAdminAccessResult<TResponse>.Failed(
                ServiceResult<TResponse>.NotFound(
                    "Agency was not found."));
        }

        var memberAccess = await _agencyRepository.GetMemberAccessReadOnlyAsync(
            agencyId,
            currentUserId,
            cancellationToken);

        if (memberAccess is null ||
            memberAccess.Status != AgencyMemberStatus.Active ||
            memberAccess.Role != AgencyMemberRole.Owner)
        {
            return AgencyAdminAccessResult<TResponse>.Failed(
                ServiceResult<TResponse>.Forbidden(forbiddenMessage));
        }

        return AgencyAdminAccessResult<TResponse>.Succeeded(currentUserId);
    }
}
