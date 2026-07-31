using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Repositories;

public enum AgencyInvitationAcceptancePersistenceResult
{
    Succeeded = 1,
    MembershipAlreadyExists = 2
}

public enum AgencyInvitationCreationPersistenceResult
{
    Succeeded = 1,
    PendingInvitationAlreadyExists = 2
}

public interface IAgencyInvitationTerminalMutationScope
    : IAsyncDisposable
{
    AgencyInvitation Invitation { get; }

    Task PersistTerminalTransitionAsync(
        CancellationToken cancellationToken);

    Task<AgencyInvitationAcceptancePersistenceResult>
        PersistAcceptanceAsync(
            CancellationToken cancellationToken);

    Task CommitAsync(
        CancellationToken cancellationToken);
}

public interface IAgencyInvitationCreationScope
    : IAsyncDisposable
{
    AgencyInvitation? PendingInvitation { get; }

    Task PersistObservedExpiryAsync(
        CancellationToken cancellationToken);

    Task<AgencyInvitationCreationPersistenceResult>
        PersistNewInvitationAsync(
            AgencyInvitation invitation,
            CancellationToken cancellationToken);

    Task CommitAsync(
        CancellationToken cancellationToken);
}

public interface IAgencyInvitationRepository
{
    Task<IAgencyInvitationCreationScope>
        BeginCreateOrReplaceAsync(
            Guid agencyId,
            string normalizedEmail,
            CancellationToken cancellationToken);

    Task<IAgencyInvitationTerminalMutationScope?>
        BeginTerminalMutationByTokenAsync(
            string token,
            CancellationToken cancellationToken);

    Task<IAgencyInvitationTerminalMutationScope?>
        BeginTerminalMutationByIdAsync(
            Guid invitationId,
            CancellationToken cancellationToken);

    Task<IReadOnlyList<AgencyInvitation>>
        GetByAgencyIdReadOnlyAsync(
            Guid agencyId,
            AgencyInvitationStatus? status,
            DateTime utcNow,
            CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}
