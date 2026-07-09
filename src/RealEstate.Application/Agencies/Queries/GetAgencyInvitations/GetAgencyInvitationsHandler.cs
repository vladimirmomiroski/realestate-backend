using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Application.Common.Authentication;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Queries.GetAgencyInvitations;

public sealed class GetAgencyInvitationsHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IAgencyRepository _agencyRepository;
    private readonly IAgencyInvitationRepository _agencyInvitationRepository;

    public GetAgencyInvitationsHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IAgencyRepository agencyRepository,
        IAgencyInvitationRepository agencyInvitationRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _agencyRepository = agencyRepository;
        _agencyInvitationRepository = agencyInvitationRepository;
    }

    public async Task<ServiceResult<IReadOnlyList<AgencyInvitationResponse>>> HandleAsync(
        GetAgencyInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            _currentUserService.UserId is not Guid currentUserId)
        {
            return ServiceResult<IReadOnlyList<AgencyInvitationResponse>>.Unauthorized(
                "Current user could not be resolved.");
        }

        User? currentUser = await _userRepository.GetByIdReadOnlyAsync(
            currentUserId,
            cancellationToken);

        if (currentUser is null)
        {
            return ServiceResult<IReadOnlyList<AgencyInvitationResponse>>.Unauthorized(
                "Current user could not be resolved.");
        }

        if (currentUser.Status == UserStatus.Disabled)
        {
            return ServiceResult<IReadOnlyList<AgencyInvitationResponse>>.Forbidden(
                "Disabled users cannot view agency invitations.");
        }

        bool agencyExists = await _agencyRepository.ExistsAsync(
            query.AgencyId,
            cancellationToken);

        if (!agencyExists)
        {
            return ServiceResult<IReadOnlyList<AgencyInvitationResponse>>.NotFound(
                "Agency was not found.");
        }

        var memberAccess = await _agencyRepository.GetMemberAccessReadOnlyAsync(
            query.AgencyId,
            currentUserId,
            cancellationToken);

        if (memberAccess is null ||
            memberAccess.Status != AgencyMemberStatus.Active ||
            memberAccess.Role != AgencyMemberRole.Owner)
        {
            return ServiceResult<IReadOnlyList<AgencyInvitationResponse>>.Forbidden(
                "Only active agency owners can view invitations.");
        }

        IReadOnlyList<AgencyInvitation> invitations =
            await _agencyInvitationRepository.GetByAgencyIdReadOnlyAsync(
                query.AgencyId,
                query.Status,
                cancellationToken);

        IReadOnlyList<AgencyInvitationResponse> response = invitations
            .Select(invitation => invitation.ToResponse())
            .ToList();

        return ServiceResult<IReadOnlyList<AgencyInvitationResponse>>.Success(response);
    }
}
