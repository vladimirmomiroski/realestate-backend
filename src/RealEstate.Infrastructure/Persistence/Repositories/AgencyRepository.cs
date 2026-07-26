using Microsoft.EntityFrameworkCore;
using RealEstate.Application.Agencies.ReadModels;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;

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

    public async Task<Agency?> GetBySlugReadOnlyAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Agencies
            .AsNoTracking()
            .FirstOrDefaultAsync(agency => agency.Slug == slug, cancellationToken);
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

    public async Task<Agency?> GetByIdForUpdateAsync(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Agencies
            .FirstOrDefaultAsync(agency => agency.Id == agencyId, cancellationToken);
    }

    public async Task<Agency?> GetByIdWithMembersForUpdateAsync(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Agencies
            .Include(agency => agency.Members)
            .FirstOrDefaultAsync(agency => agency.Id == agencyId, cancellationToken);
    }

    public void AddMember(AgencyMember member)
    {
        _dbContext.Set<AgencyMember>().Add(member);
    }

    public async Task<IAgencyOwnerMutationScope?>
    BeginLastActiveOwnerMutationAsync(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        try
        {
            DbConnection connection =
                _dbContext.Database.GetDbConnection();

            await using DbCommand command =
                connection.CreateCommand();

            command.Transaction =
                transaction.GetDbTransaction();

            command.CommandText =
                """
            SELECT "Id"
            FROM "Agencies"
            WHERE "Id" = @agencyId
            FOR UPDATE;
            """;

            DbParameter agencyIdParameter =
                command.CreateParameter();

            agencyIdParameter.ParameterName = "@agencyId";
            agencyIdParameter.Value = agencyId;

            command.Parameters.Add(agencyIdParameter);

            object? lockedAgencyId =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            if (lockedAgencyId is null ||
                lockedAgencyId is DBNull)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                await transaction.DisposeAsync();

                return null;
            }

            return new AgencyOwnerMutationScope(
                transaction);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);
            }
            finally
            {
                await transaction.DisposeAsync();
            }

            throw;
        }
    }

    public async Task<AgencyMember?> GetMemberByIdForUpdateAsync(
    Guid agencyId,
    Guid memberId,
    CancellationToken cancellationToken)
    {
        return await _dbContext.Set<AgencyMember>()
            .FirstOrDefaultAsync(
                member =>
                    member.Id == memberId &&
                    member.AgencyId == agencyId,
                cancellationToken);
    }

    public async Task<AgencyDashboardSummaryReadModel?>
    GetDashboardSummaryReadOnlyAsync(
        Guid agencyId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Agencies
            .AsNoTracking()
            .Where(agency => agency.Id == agencyId)
            .Select(agency => new AgencyDashboardSummaryReadModel
            {
                AgencyId = agency.Id,
                AgencyName = agency.Name,
                AgencyStatus = agency.Status,

                TotalListings = _dbContext.Listings.Count(
                    listing =>
                        listing.AgencyId == agency.Id),

                DraftListings = _dbContext.Listings.Count(
                    listing =>
                        listing.AgencyId == agency.Id &&
                        listing.Status == ListingStatus.Draft),

                ActiveListings = _dbContext.Listings.Count(
                    listing =>
                        listing.AgencyId == agency.Id &&
                        listing.Status == ListingStatus.Active),

                ArchivedListings = _dbContext.Listings.Count(
                    listing =>
                        listing.AgencyId == agency.Id &&
                        listing.Status == ListingStatus.Archived),

                MembersCount = _dbContext
                    .Set<AgencyMember>()
                    .Count(member =>
                        member.AgencyId == agency.Id),

                ActiveMembersCount = _dbContext
                    .Set<AgencyMember>()
                    .Count(member =>
                        member.AgencyId == agency.Id &&
                        member.Status == AgencyMemberStatus.Active),

                PendingInvitationsCount = _dbContext
                    .Set<AgencyInvitation>()
                    .Count(invitation =>
                        invitation.AgencyId == agency.Id &&
                        invitation.Status ==
                            AgencyInvitationStatus.Pending &&
                        invitation.ExpiresAtUtc > utcNow)
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CountActiveOwnersAsync(
    Guid agencyId,
    CancellationToken cancellationToken)
    {
        return await _dbContext.Set<AgencyMember>()
            .CountAsync(
                member =>
                    member.AgencyId == agencyId &&
                    member.Role == AgencyMemberRole.Owner &&
                    member.Status == AgencyMemberStatus.Active,
                cancellationToken);
    }

    public async Task<AgencyMemberAccessReadModel?> GetMemberAccessReadOnlyAsync(
        Guid agencyId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Set<AgencyMember>()
            .AsNoTracking()
            .Where(member =>
                member.AgencyId == agencyId &&
                member.UserId == userId)
            .Select(member => new AgencyMemberAccessReadModel
            {
                Role = member.Role,
                Status = member.Status
            })
            .FirstOrDefaultAsync(cancellationToken);
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class AgencyOwnerMutationScope
    : IAgencyOwnerMutationScope
    {
        private readonly IDbContextTransaction _transaction;

        private bool _committed;
        private bool _disposed;

        public AgencyOwnerMutationScope(
            IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync(
            CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(AgencyOwnerMutationScope));
            }

            if (_committed)
            {
                return;
            }

            await _transaction.CommitAsync(
                cancellationToken);

            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                if (!_committed)
                {
                    await _transaction.RollbackAsync(
                        CancellationToken.None);
                }
            }
            finally
            {
                await _transaction.DisposeAsync();
            }
        }
    }
}
