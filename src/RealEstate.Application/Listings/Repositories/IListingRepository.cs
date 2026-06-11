using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Repositories;

public interface IListingRepository
{
    Task CreateAsync(Listing listing, CancellationToken cancellationToken);

    Task<IReadOnlyList<Listing>> GetAllReadOnlyAsync(CancellationToken cancellationToken);

    Task<Listing?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);
}
