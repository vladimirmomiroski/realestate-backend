using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Users.Dtos;
using RealEstate.Application.Users.Mappings;
using RealEstate.Application.Users.Repositories;

namespace RealEstate.Application.Users.Queries.GetCurrentUser;

public sealed class GetCurrentUserHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public GetCurrentUserHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<ServiceResult<UserProfileResponse>> HandleAsync(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return ServiceResult<UserProfileResponse>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        var user = await _userRepository.GetByIdReadOnlyAsync(
            _currentUserService.UserId.Value,
            cancellationToken);

        if (user is null)
        {
            return ServiceResult<UserProfileResponse>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        return ServiceResult<UserProfileResponse>.Success(
            user.ToProfileResponse());
    }
}
