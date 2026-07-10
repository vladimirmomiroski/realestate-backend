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

    public async Task<ServiceResult<IReadOnlyList<AgencyInvitationListItemResponse>>> HandleAsync(
        GetAgencyInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        AgencyAdminAccessResult<IReadOnlyList<AgencyInvitationListItemResponse>> accessResult =
            await _agencyAdminAccessChecker.EnsureCurrentUserIsActiveOwnerAsync<IReadOnlyList<AgencyInvitationListItemResponse>>(
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

        IReadOnlyList<AgencyInvitationListItemResponse> response = invitations
            .Select(invitation => invitation.ToListItemResponse())
            .ToList();

        return ServiceResult<IReadOnlyList<AgencyInvitationListItemResponse>>.Success(response);
    }
}
