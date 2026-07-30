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

    public async Task<
        ServiceResult<AgencyInvitationListItemResponse>>
        HandleAsync(
            Guid agencyId,
            Guid invitationId,
            CancellationToken cancellationToken)
    {
        AgencyAdminAccessResult<
            AgencyInvitationListItemResponse>
            accessResult =
                await _agencyAdminAccessChecker
                    .EnsureCurrentUserIsActiveOwnerAsync<
                        AgencyInvitationListItemResponse>(
                        agencyId,
                        "Only active agency owners can cancel invitations.",
                        cancellationToken);

        if (accessResult.HasFailure)
        {
            return accessResult.Failure!;
        }

        IAgencyInvitationTerminalMutationScope?
            terminalMutationScope =
                await _agencyInvitationRepository
                    .BeginTerminalMutationByIdAsync(
                        invitationId,
                        cancellationToken);

        if (terminalMutationScope is null)
        {
            return ServiceResult<
                AgencyInvitationListItemResponse>
                .NotFound(
                    "Invitation was not found.",
                    ErrorCodes.ResourceNotFound);
        }

        await using (terminalMutationScope)
        {
            AgencyInvitation invitation =
                terminalMutationScope.Invitation;

            if (invitation.AgencyId != agencyId)
            {
                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .NotFound(
                        "Invitation was not found.",
                        ErrorCodes.ResourceNotFound);
            }

            if (invitation.Status ==
                AgencyInvitationStatus.Accepted)
            {
                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .Conflict(
                        "Accepted invitation cannot be cancelled.",
                        ErrorCodes.ConflictResourceState);
            }

            if (invitation.Status ==
                AgencyInvitationStatus.Cancelled)
            {
                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .Conflict(
                        "Invitation has already been cancelled.",
                        ErrorCodes.ConflictResourceState);
            }

            if (invitation.Status ==
                AgencyInvitationStatus.Expired)
            {
                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .Conflict(
                        "Expired invitation cannot be cancelled.",
                        ErrorCodes.ConflictResourceState);
            }

            DateTime utcNow = DateTime.UtcNow;

            if (invitation.ExpiresAtUtc <= utcNow)
            {
                invitation.MarkExpired(utcNow);

                await terminalMutationScope
                    .PersistTerminalTransitionAsync(
                        cancellationToken);

                await terminalMutationScope.CommitAsync(
                    cancellationToken);

                return ServiceResult<
                    AgencyInvitationListItemResponse>
                    .Conflict(
                        "Expired invitation cannot be cancelled.",
                        ErrorCodes.ConflictResourceState);
            }

            invitation.Cancel(utcNow);

            await terminalMutationScope
                .PersistTerminalTransitionAsync(
                    cancellationToken);

            await terminalMutationScope.CommitAsync(
                cancellationToken);

            AgencyInvitationListItemResponse response =
                invitation.ToListItemResponse();

            return ServiceResult<
                AgencyInvitationListItemResponse>
                .Success(response);
        }
    }
}
