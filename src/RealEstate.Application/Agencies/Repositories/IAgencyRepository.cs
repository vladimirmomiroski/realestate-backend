namespace RealEstate.Application.Agencies.Repositories;

public interface IAgencyRepository
{
    Task<bool> ExistsAsync(
        Guid agencyId,
        CancellationToken cancellationToken);

    Task<bool> IsActiveMemberAsync(
        Guid agencyId,
        Guid userId,
        CancellationToken cancellationToken);
}