using Microsoft.EntityFrameworkCore;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Infrastructure.Persistence.Repositories;

public sealed class ListingRepository : IListingRepository
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

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

    public async Task<int> CountByCreatedByUserIdAsync(
        Guid createdByUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Listings
            .CountAsync(
                listing => listing.CreatedByUserId == createdByUserId,
                cancellationToken);
    }

    public async Task<PagedResult<Listing>> GetFilteredReadOnlyAsync(
        GetListingsQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<Listing> listingsQuery = _dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == ListingStatus.Active);

        listingsQuery = ApplyBasicFilters(listingsQuery, query);
        listingsQuery = ApplyPropertyDetailFilters(listingsQuery, query);
        listingsQuery = ApplyLocationFilters(listingsQuery, query);

        (int page, int pageSize) = NormalizePagination(query.Page, query.PageSize);

        int totalCount = await listingsQuery.CountAsync(cancellationToken);

        IOrderedQueryable<Listing> orderedQuery = ApplyOrdering(
            listingsQuery,
            query.SortOption);

        List<Listing> listings = await ApplyListingIncludes(orderedQuery)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Listing>(
            listings,
            page,
            pageSize,
            totalCount);
    }

    public async Task<Listing?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken)
    {
        return await ApplyListingIncludes(_dbContext.Listings.AsNoTracking())
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Listing>> GetByCreatedByUserIdAsync(
        Guid createdByUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        (page, pageSize) = NormalizePagination(page, pageSize);

        IQueryable<Listing> query = _dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.CreatedByUserId == createdByUserId);

        int totalCount = await query.CountAsync(cancellationToken);

        List<Listing> listings = await ApplyListingIncludes(query)
            .OrderByDescending(listing => listing.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Listing>(
            listings,
            page,
            pageSize,
            totalCount);
    }

    public async Task<PagedResult<Listing>> GetByAgencyIdForDashboardReadOnlyAsync(
    Guid agencyId,
    ListingStatus? status,
    int page,
    int pageSize,
    CancellationToken cancellationToken)
    {
        (page, pageSize) = NormalizePagination(page, pageSize);

        IQueryable<Listing> query = _dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.AgencyId == agencyId);

        if (status.HasValue)
        {
            query = query.Where(listing => listing.Status == status.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<Listing> listings = await ApplyListingIncludes(query)
            .OrderByDescending(listing => listing.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Listing>(
            listings,
            page,
            pageSize,
            totalCount);
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

    public async Task<Listing?> GetByIdForUpdateAsync(
    Guid id,
    CancellationToken cancellationToken)
    {
        return await _dbContext.Listings
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Listing> ApplyBasicFilters(
        IQueryable<Listing> query,
        GetListingsQuery filters)
    {
        if (filters.AgencyId.HasValue)
        {
            query = query.Where(listing =>
                listing.AgencyId == filters.AgencyId.Value);
        }

        if (filters.ListingType.HasValue)
        {
            query = query.Where(listing =>
                listing.ListingType == filters.ListingType.Value);
        }

        if (filters.PropertyType.HasValue)
        {
            query = query.Where(listing =>
                listing.PropertyType == filters.PropertyType.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Currency))
        {
            query = query.Where(listing =>
                listing.Currency == filters.Currency);
        }

        if (filters.MinPrice.HasValue)
        {
            query = query.Where(listing =>
                listing.Price >= filters.MinPrice.Value);
        }

        if (filters.MaxPrice.HasValue)
        {
            query = query.Where(listing =>
                listing.Price <= filters.MaxPrice.Value);
        }

        if (filters.MinAreaSquareMeters.HasValue)
        {
            query = query.Where(listing =>
                listing.AreaSquareMeters >=
                filters.MinAreaSquareMeters.Value);
        }

        if (filters.MaxAreaSquareMeters.HasValue)
        {
            query = query.Where(listing =>
                listing.AreaSquareMeters <=
                filters.MaxAreaSquareMeters.Value);
        }

        if (filters.MinRooms.HasValue)
        {
            query = query.Where(listing =>
                listing.Rooms.HasValue &&
                listing.Rooms.Value >= filters.MinRooms.Value);
        }

        if (filters.MaxRooms.HasValue)
        {
            query = query.Where(listing =>
                listing.Rooms.HasValue &&
                listing.Rooms.Value <= filters.MaxRooms.Value);
        }

        if (filters.HeatingType.HasValue)
        {
            query = query.Where(listing =>
                listing.HeatingType == filters.HeatingType.Value);
        }

        if (filters.FurnishingStatus.HasValue)
        {
            query = query.Where(listing =>
                listing.FurnishingStatus == filters.FurnishingStatus.Value);
        }

        if (filters.Condition.HasValue)
        {
            query = query.Where(listing =>
                listing.Condition == filters.Condition.Value);
        }

        if (filters.HasBasement.HasValue)
        {
            query = query.Where(listing =>
                listing.HasBasement == filters.HasBasement.Value);
        }

        return query;
    }

    private static IQueryable<Listing> ApplyPropertyDetailFilters(
        IQueryable<Listing> query,
        GetListingsQuery filters)
    {
        if (filters.HasElevator.HasValue)
        {
            query = query.Where(listing =>
                listing.ApartmentDetails != null &&
                listing.ApartmentDetails.HasElevator == filters.HasElevator.Value);
        }

        if (filters.ApartmentType.HasValue)
        {
            query = query.Where(listing =>
                listing.ApartmentDetails != null &&
                listing.ApartmentDetails.ApartmentType == filters.ApartmentType.Value);
        }

        if (filters.HouseType.HasValue)
        {
            query = query.Where(listing =>
                listing.HouseDetails != null &&
                listing.HouseDetails.HouseType == filters.HouseType.Value);
        }

        if (filters.MinYardAreaSquareMeters.HasValue)
        {
            query = query.Where(listing =>
                listing.HouseDetails != null &&
                listing.HouseDetails.YardAreaSquareMeters >= filters.MinYardAreaSquareMeters.Value);
        }

        if (filters.MaxYardAreaSquareMeters.HasValue)
        {
            query = query.Where(listing =>
                listing.HouseDetails != null &&
                listing.HouseDetails.YardAreaSquareMeters <= filters.MaxYardAreaSquareMeters.Value);
        }

        return query;
    }

    private static IQueryable<Listing> ApplyLocationFilters(
        IQueryable<Listing> query,
        GetListingsQuery filters)
    {
        if (!string.IsNullOrWhiteSpace(filters.City))
        {
            string city = filters.City.Trim();

            query = query.Where(listing =>
                listing.Translations.Any(translation =>
                    translation.City != null &&
                    EF.Functions.ILike(translation.City, $"%{city}%")));
        }

        if (!string.IsNullOrWhiteSpace(filters.Municipality))
        {
            string municipality = filters.Municipality.Trim();

            query = query.Where(listing =>
                listing.Translations.Any(translation =>
                    translation.Municipality != null &&
                    EF.Functions.ILike(translation.Municipality, $"%{municipality}%")));
        }

        if (!string.IsNullOrWhiteSpace(filters.Neighborhood))
        {
            string neighborhood = filters.Neighborhood.Trim();

            query = query.Where(listing =>
                listing.Translations.Any(translation =>
                    translation.Neighborhood != null &&
                    EF.Functions.ILike(translation.Neighborhood, $"%{neighborhood}%")));
        }

        return query;
    }

    private static IOrderedQueryable<Listing> ApplyOrdering(
        IQueryable<Listing> query,
        ListingSortOption sortOption)
    {
        return sortOption switch
        {
            ListingSortOption.Newest =>
                query
                    .OrderByDescending(listing => listing.CreatedAtUtc)
                    .ThenByDescending(listing => listing.Id),

            ListingSortOption.PriceAsc =>
                query
                    .OrderBy(listing => listing.Price)
                    .ThenByDescending(listing => listing.CreatedAtUtc)
                    .ThenByDescending(listing => listing.Id),

            ListingSortOption.PriceDesc =>
                query
                    .OrderByDescending(listing => listing.Price)
                    .ThenByDescending(listing => listing.CreatedAtUtc)
                    .ThenByDescending(listing => listing.Id),

            _ => throw new ArgumentOutOfRangeException(
                nameof(sortOption),
                sortOption,
                "Unsupported listing sort option.")
        };
    }

    private static IQueryable<Listing> ApplyListingIncludes(IQueryable<Listing> query)
    {
        return query
            .Include(listing => listing.Translations)
            .Include(listing => listing.Images)
            .Include(listing => listing.ApartmentDetails)
            .Include(listing => listing.HouseDetails)
            .AsSplitQuery();
    }

    private static (int Page, int PageSize) NormalizePagination(
        int page,
        int pageSize)
    {
        page = Math.Max(page, 1);

        if (pageSize < 1)
        {
            pageSize = DefaultPageSize;
        }

        pageSize = Math.Min(pageSize, MaxPageSize);

        return (page, pageSize);
    }
}