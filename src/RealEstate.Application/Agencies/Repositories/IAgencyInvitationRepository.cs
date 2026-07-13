using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Repositories;

public interface IAgencyInvitationRepository
{
    Task CreateAsync(
        AgencyInvitation invitation,
        CancellationToken cancellationToken);

    Task<AgencyInvitation?> GetByTokenForUpdateAsync(
        string token,
        CancellationToken cancellationToken);

    Task<AgencyInvitation?> GetByIdForUpdateAsync(
        Guid invitationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AgencyInvitation>> GetByAgencyIdReadOnlyAsync(
        Guid agencyId,
        AgencyInvitationStatus? status,
        CancellationToken cancellationToken);

    Task<bool> ExistsPendingForAgencyEmailAsync(
        Guid agencyId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
