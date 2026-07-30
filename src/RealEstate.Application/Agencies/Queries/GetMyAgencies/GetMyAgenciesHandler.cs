using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Queries.GetMyAgencies;

public sealed class GetMyAgenciesHandler
{
    private readonly IAgencyRepository _agencyRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public GetMyAgenciesHandler(
        IAgencyRepository agencyRepository,
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _agencyRepository = agencyRepository;
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<ServiceResult<IReadOnlyList<MyAgencyResponse>>> HandleAsync(
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            _currentUserService.UserId is not Guid userId)
        {
            return ServiceResult<IReadOnlyList<MyAgencyResponse>>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        User? currentUser = await _userRepository.GetByIdReadOnlyAsync(
            userId,
            cancellationToken);

        if (currentUser is null)
        {
            return ServiceResult<IReadOnlyList<MyAgencyResponse>>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        IReadOnlyList<UserAgencyMembershipReadModel> memberships =
            await _agencyRepository.GetByUserIdReadOnlyAsync(
                userId,
                cancellationToken);

        IReadOnlyList<MyAgencyResponse> response = memberships
            .Select(membership => membership.ToMyAgencyResponse())
            .ToList();

        return ServiceResult<IReadOnlyList<MyAgencyResponse>>.Success(response);
    }
}
