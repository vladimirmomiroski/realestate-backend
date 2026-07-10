using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.Mappings;
using RealEstate.Application.Agencies.Permissions;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Application.Common;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Commands.CancelAgencyInvitation;

public sealed class CancelAgencyInvitationHandler
{
    private readonly AgencyAdminAccessChecker _agencyAdminAccessChecker;
    private readonly IAgencyInvitationRepository _agencyInvitationRepository;

    public CancelAgencyInvitationHandler(
        AgencyAdminAccessChecker agencyAdminAccessChecker,
        IAgencyInvitationRepository agencyInvitationRepository)
    {
        _agencyAdminAccessChecker = agencyAdminAccessChecker;
        _agencyInvitationRepository = agencyInvitationRepository;
    }

    public async Task<ServiceResult<AgencyInvitationListItemResponse>> HandleAsync(
        Guid agencyId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        AgencyAdminAccessResult<AgencyInvitationListItemResponse> accessResult =
            await _agencyAdminAccessChecker.EnsureCurrentUserIsActiveOwnerAsync<AgencyInvitationListItemResponse>(
                agencyId,
                "Only active agency owners can cancel invitations.",
                cancellationToken);

        if (accessResult.HasFailure)
        {
            return accessResult.Failure!;
        }

        AgencyInvitation? invitation =
            await _agencyInvitationRepository.GetByIdForUpdateAsync(
                invitationId,
                cancellationToken);

        if (invitation is null ||
            invitation.AgencyId != agencyId)
        {
            return ServiceResult<AgencyInvitationListItemResponse>.NotFound(
                "Invitation was not found.");
        }

        if (invitation.Status == AgencyInvitationStatus.Accepted)
        {
            return ServiceResult<AgencyInvitationListItemResponse>.ValidationError(
                "Accepted invitation cannot be cancelled.");
        }

        if (invitation.Status == AgencyInvitationStatus.Cancelled)
        {
            return ServiceResult<AgencyInvitationListItemResponse>.ValidationError(
                "Invitation has already been cancelled.");
        }

        if (invitation.Status == AgencyInvitationStatus.Expired)
        {
            return ServiceResult<AgencyInvitationListItemResponse>.ValidationError(
                "Expired invitation cannot be cancelled.");
        }

        DateTime utcNow = DateTime.UtcNow;

        if (invitation.ExpiresAtUtc <= utcNow)
        {
            invitation.MarkExpired(utcNow);

            await _agencyInvitationRepository.SaveChangesAsync(cancellationToken);

            return ServiceResult<AgencyInvitationListItemResponse>.ValidationError(
                "Expired invitation cannot be cancelled.");
        }

        invitation.Cancel(utcNow);

        await _agencyInvitationRepository.SaveChangesAsync(cancellationToken);

        AgencyInvitationListItemResponse response = invitation.ToListItemResponse();

        return ServiceResult<AgencyInvitationListItemResponse>.Success(response);
    }
}
