using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Repositories;

public interface IAgencyRepository
{
    Task CreateAsync(Agency agency, CancellationToken cancellationToken);

    Task<Agency?> GetByIdReadOnlyAsync(
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