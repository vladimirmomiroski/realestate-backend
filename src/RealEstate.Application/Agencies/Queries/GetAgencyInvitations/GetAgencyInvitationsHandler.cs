using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Domain.Entities;
using RealEstate.Application.Agencies.Permissions;

namespace RealEstate.Application.Agencies.Queries.GetAgencyInvitations;

public sealed class GetAgencyInvitationsHandler
{
    private readonly AgencyAdminAccessChecker _agencyAdminAccessChecker;
    private readonly IAgencyInvitationRepository _agencyInvitationRepository;

    public GetAgencyInvitationsHandler(
        AgencyAdminAccessChecker agencyAdminAccessChecker,
        IAgencyInvitationRepository agencyInvitationRepository)
    {
        _agencyAdminAccessChecker = agencyAdminAccessChecker;
        _agencyInvitationRepository = agencyInvitationRepository;
    }

    public async Task<ServiceResult<IReadOnlyList<AgencyInvitationResponse>>> HandleAsync(
    GetAgencyInvitationsQuery query,
    CancellationToken cancellationToken)
    {
        AgencyAdminAccessResult<IReadOnlyList<AgencyInvitationResponse>> accessResult =
            await _agencyAdminAccessChecker.EnsureCurrentUserIsActiveOwnerAsync<IReadOnlyList<AgencyInvitationResponse>>(
                query.AgencyId,
                "Only active agency owners can view invitations.",
                cancellationToken);

        if (accessResult.HasFailure)
        {
            return accessResult.Failure!;
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
