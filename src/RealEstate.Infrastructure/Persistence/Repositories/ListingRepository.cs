using Microsoft.EntityFrameworkCore;
using RealEstate.Application.Listings.Repositories;
using RealEstate.Domain.Entities;

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

    public async Task<IReadOnlyList<Listing>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Translations)
            .OrderByDescending(listing => listing.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Translations)
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);
    }
}