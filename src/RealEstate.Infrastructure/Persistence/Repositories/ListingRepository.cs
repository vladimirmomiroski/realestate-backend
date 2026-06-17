using Microsoft.EntityFrameworkCore;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Queries.GetListings;

namespace RealEstate.Infrastructure.Persistence.Repositories;

public sealed class ListingRepository : IListingRepository
{
    private readonly RealEstateDbContext _dbContext;

    public ListingRepository(RealEstateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateAsync(Listing listing, CancellationToken cancellationToken)
    {
        _dbContext.Listings.Add(listing);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<Listing>> GetFilteredReadOnlyAsync(
        GetListingsQuery query,
        CancellationToken cancellationToken)
    {
        var listingsQuery = _dbContext.Listings
            .AsNoTracking()
            .AsQueryable();

        if (query.ListingType.HasValue)
        {
            listingsQuery = listingsQuery.Where(listing =>
                listing.ListingType == query.ListingType.Value);
        }

        if (query.PropertyType.HasValue)
        {
            listingsQuery = listingsQuery.Where(listing =>
                listing.PropertyType == query.PropertyType.Value);
        }

        if (query.MinPrice.HasValue)
        {
            listingsQuery = listingsQuery.Where(listing =>
                listing.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            listingsQuery = listingsQuery.Where(listing =>
                listing.Price <= query.MaxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();

            listingsQuery = listingsQuery.Where(listing =>
                listing.Translations.Any(translation =>
                    translation.City != null &&
                    EF.Functions.ILike(translation.City, $"%{city}")));
        }

        if (!string.IsNullOrWhiteSpace(query.Municipality))
        {
            var municipality = query.Municipality.Trim();

            listingsQuery = listingsQuery.Where(listing =>
                listing.Translations.Any(translation =>
                    translation.Municipality != null &&
                    EF.Functions.ILike(translation.Municipality, $"%{municipality}%")));
        }

        if (!string.IsNullOrWhiteSpace(query.Neighborhood))
        {
            var neighborhood = query.Neighborhood.Trim();

            listingsQuery = listingsQuery.Where(listing =>
                listing.Translations.Any(translation =>
                    translation.Neighborhood != null &&
                    EF.Functions.ILike(translation.Neighborhood, $"%{neighborhood}%")));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

        var totalCount = await listingsQuery.CountAsync(cancellationToken);

        var listings = await listingsQuery
             .Include(listing => listing.Translations)
             .Include(listing => listing.Images)
             .AsSplitQuery()
             .OrderByDescending(listing => listing.CreatedAtUtc)
             .Skip((page - 1) * pageSize)
             .Take(pageSize)
             .ToListAsync(cancellationToken);

        return new PagedResult<Listing>(listings, totalCount);
    }

    public async Task<Listing?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Translations)
            .Include(listing => listing.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);
    }

    public async Task<Listing?> GetByIdWithImagesForUpdateAsync(
    Guid id,
    CancellationToken cancellationToken)
    {
        return await _dbContext.Listings
            .Include(listing => listing.Images)
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);
    }

    public void AddListingImage(ListingImage image)
    {
        _dbContext.Set<ListingImage>().Add(image);
    }

    public void RemoveListingImage(ListingImage image)
    {
        _dbContext.Set<ListingImage>().Remove(image);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

}