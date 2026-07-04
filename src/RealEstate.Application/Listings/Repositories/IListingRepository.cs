using RealEstate.Application.Common;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Listings.Repositories;

public interface IListingRepository
{
    Task CreateAsync(Listing listing, CancellationToken cancellationToken);

    Task<PagedResult<Listing>> GetFilteredReadOnlyAsync(
    GetListingsQuery query,
    CancellationToken cancellationToken);

    Task<Listing?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task<Listing?> GetByIdForUpdateAsync(
    Guid id,
    CancellationToken cancellationToken);

    Task<int> CountByCreatedByUserIdAsync(
    Guid createdByUserId,
    CancellationToken cancellationToken);

    Task<PagedResult<Listing>> GetByAgencyIdForDashboardReadOnlyAsync(
    Guid agencyId,
    ListingStatus? status,
    int page,
    int pageSize,
    CancellationToken cancellationToken);

    Task<Listing?> GetByIdWithImagesForUpdateAsync(
    Guid id,
    CancellationToken cancellationToken);

    Task<PagedResult<Listing>> GetByCreatedByUserIdAsync(
    Guid createdByUserId,
    int page,
    int pageSize,
    CancellationToken cancellationToken);

    void AddListingImage(ListingImage image);

    void RemoveListingImage(ListingImage image);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
