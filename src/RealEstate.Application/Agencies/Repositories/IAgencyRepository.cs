using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Repositories;

public interface IAgencyRepository
{
    Task CreateAsync(Agency agency, CancellationToken cancellationToken);

    Task<Agency?> GetByIdReadOnlyAsync(
        Guid agencyId,
        CancellationToken cancellationToken);

    Task<Agency?> GetBySlugReadOnlyAsync(
        string slug,
        CancellationToken cancellationToken);

    Task<Agency?> GetByIdForUpdateAsync(
        Guid agencyId,
        CancellationToken cancellationToken);

    Task<Agency?> GetByIdWithMembersForUpdateAsync(
        Guid agencyId,
        CancellationToken cancellationToken);

    void AddMember(AgencyMember member);

    Task<IReadOnlyList<UserAgencyMembershipReadModel>> GetByUserIdReadOnlyAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AgencyMemberReadModel>> GetMembersByAgencyIdReadOnlyAsync(
        Guid agencyId,
        CancellationToken cancellationToken);

    Task<AgencyMemberAccessReadModel?> GetMemberAccessReadOnlyAsync(
        Guid agencyId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        Guid agencyId,
        CancellationToken cancellationToken);

    Task<bool> IsActiveMemberAsync(
        Guid agencyId,
        Guid userId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
