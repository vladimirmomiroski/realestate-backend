using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Queries.GetAgencyMembers;

public sealed class GetAgencyMembersHandler
{
    private readonly IAgencyRepository _agencyRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public GetAgencyMembersHandler(
        IAgencyRepository agencyRepository,
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _agencyRepository = agencyRepository;
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    public async Task<ServiceResult<IReadOnlyList<AgencyMemberResponse>>> HandleAsync(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            _currentUserService.UserId is not Guid userId)
        {
            return ServiceResult<IReadOnlyList<AgencyMemberResponse>>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        User? currentUser = await _userRepository.GetByIdReadOnlyAsync(
            userId,
            cancellationToken);

        if (currentUser is null)
        {
            return ServiceResult<IReadOnlyList<AgencyMemberResponse>>.Unauthorized(
                "Current user could not be resolved.",
                ErrorCodes.AuthenticationInvalidPrincipal);
        }

        if (currentUser.Status == UserStatus.Disabled)
        {
            return ServiceResult<IReadOnlyList<AgencyMemberResponse>>.Forbidden(
                "Disabled users cannot view agency members.",
                ErrorCodes.AuthorizationAccountDisabled);
        }

        bool agencyExists = await _agencyRepository.ExistsAsync(
            agencyId,
            cancellationToken);

        if (!agencyExists)
        {
            return ServiceResult<IReadOnlyList<AgencyMemberResponse>>.NotFound(
                "Agency was not found.",
                ErrorCodes.ResourceNotFound);
        }

        bool isActiveMember = await _agencyRepository.IsActiveMemberAsync(
            agencyId,
            userId,
            cancellationToken);

        if (!isActiveMember)
        {
            return ServiceResult<IReadOnlyList<AgencyMemberResponse>>.Forbidden(
                "User is not an active member of this agency.",
                ErrorCodes.AuthorizationForbidden);
        }

        IReadOnlyList<AgencyMemberReadModel> members =
            await _agencyRepository.GetMembersByAgencyIdReadOnlyAsync(
                agencyId,
                cancellationToken);

        IReadOnlyList<AgencyMemberResponse> response = members
            .Select(member => member.ToAgencyMemberResponse())
            .ToList();

        return ServiceResult<IReadOnlyList<AgencyMemberResponse>>.Success(response);
    }
}
