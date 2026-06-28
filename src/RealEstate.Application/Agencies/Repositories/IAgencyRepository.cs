using RealEstate.Domain.Entities;
using RealEstate.Application.Agencies.ReadModels;

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

    Task<IReadOnlyList<UserAgencyMembershipReadModel>> GetByUserIdReadOnlyAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AgencyMemberReadModel>> GetMembersByAgencyIdReadOnlyAsync(
    Guid agencyId,
    CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        Guid agencyId,
        CancellationToken cancellationToken);

    Task<bool> IsActiveMemberAsync(
        Guid agencyId,
        Guid userId,
        CancellationToken cancellationToken);
}