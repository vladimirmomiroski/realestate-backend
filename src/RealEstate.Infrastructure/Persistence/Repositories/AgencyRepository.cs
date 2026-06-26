using Microsoft.EntityFrameworkCore;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Infrastructure.Persistence.Repositories;

public sealed class AgencyRepository : IAgencyRepository
{
    private readonly RealEstateDbContext _dbContext;

    public AgencyRepository(RealEstateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateAsync(
    Agency agency,
    CancellationToken cancellationToken)
    {
        _dbContext.Agencies.Add(agency);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Agencies
            .AnyAsync(agency => agency.Slug == slug, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Agencies
            .AnyAsync(agency => agency.Id == agencyId, cancellationToken);
    }

    public async Task<bool> IsActiveMemberAsync(
        Guid agencyId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Set<AgencyMember>()
            .AnyAsync(
                member =>
                    member.AgencyId == agencyId &&
                    member.UserId == userId &&
                    member.Status == AgencyMemberStatus.Active,
                cancellationToken);
    }
}