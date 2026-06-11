using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Repositories;

public interface IListingRepository
{
    Task CreateAsync(Listing listing, CancellationToken cancellationToken);

    Task<IReadOnlyList<Listing>> GetAllAsync(CancellationToken cancellationToken);

    Task<Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
