using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Repositories;

public enum AgencyInvitationAcceptancePersistenceResult
{
    Succeeded = 1,
    MembershipAlreadyExists = 2
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

public interface IAgencyInvitationRepository
{
    Task CreateAsync(
        AgencyInvitation invitation,
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
            CancellationToken cancellationToken);

    Task<bool> ExistsPendingForAgencyEmailAsync(
        Guid agencyId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);
}
