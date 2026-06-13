using RealEstate.Application.Common;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Listings.Repositories;

public interface IListingRepository
{
    Task CreateAsync(Listing listing, CancellationToken cancellationToken);

    Task<PagedResult<Listing>> GetFilteredReadOnlyAsync(
    GetListingsQuery query,
    CancellationToken cancellationToken);

    Task<Listing?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);
}
