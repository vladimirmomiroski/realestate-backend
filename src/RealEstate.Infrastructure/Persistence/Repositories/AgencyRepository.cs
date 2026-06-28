using Microsoft.EntityFrameworkCore;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Application.Agencies.ReadModels;

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

    public async Task<Agency?> GetByIdReadOnlyAsync(
    Guid agencyId,
    CancellationToken cancellationToken)
    {
        return await _dbContext.Agencies
            .AsNoTracking()
            .FirstOrDefaultAsync(agency => agency.Id == agencyId, cancellationToken);
    }

    public async Task<IReadOnlyList<UserAgencyMembershipReadModel>> GetByUserIdReadOnlyAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await (
            from member in _dbContext.Set<AgencyMember>().AsNoTracking()
            join agency in _dbContext.Agencies.AsNoTracking()
                on member.AgencyId equals agency.Id
            where member.UserId == userId
            orderby agency.Name
            select new UserAgencyMembershipReadModel
            {
                AgencyId = agency.Id,
                Name = agency.Name,
                Slug = agency.Slug,
                LogoUrl = agency.LogoUrl,
                City = agency.City,
                Municipality = agency.Municipality,
                AgencyStatus = agency.Status,
                MemberRole = member.Role,
                MemberStatus = member.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgencyMemberReadModel>> GetMembersByAgencyIdReadOnlyAsync(
    Guid agencyId,
    CancellationToken cancellationToken)
    {
        return await (
            from member in _dbContext.Set<AgencyMember>().AsNoTracking()
            join user in _dbContext.Users.AsNoTracking()
                on member.UserId equals user.Id
            where member.AgencyId == agencyId
            orderby member.CreatedAtUtc
            select new AgencyMemberReadModel
            {
                MemberId = member.Id,
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserStatus = user.Status,
                MemberRole = member.Role,
                MemberStatus = member.Status,
                JoinedAtUtc = member.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
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