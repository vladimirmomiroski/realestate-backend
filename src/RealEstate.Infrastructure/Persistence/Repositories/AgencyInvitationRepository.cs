using Microsoft.EntityFrameworkCore;
using RealEstate.Application.Agencies.Repositories;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Infrastructure.Persistence.Repositories;

public sealed class AgencyInvitationRepository : IAgencyInvitationRepository
{
    private readonly RealEstateDbContext _dbContext;

    public AgencyInvitationRepository(RealEstateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateAsync(
        AgencyInvitation invitation,
        CancellationToken cancellationToken)
    {
        _dbContext.AgencyInvitations.Add(invitation);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AgencyInvitation?> GetByTokenForUpdateAsync(
        string token,
        CancellationToken cancellationToken)
    {
        return await _dbContext.AgencyInvitations
            .FirstOrDefaultAsync(
                invitation => invitation.Token == token,
                cancellationToken);
    }

    public async Task<AgencyInvitation?> GetByIdForUpdateAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.AgencyInvitations
            .FirstOrDefaultAsync(
                invitation => invitation.Id == invitationId,
                cancellationToken);
    }

    public async Task<bool> ExistsPendingForAgencyEmailAsync(
        Guid agencyId,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        return await _dbContext.AgencyInvitations
            .AnyAsync(
                invitation =>
                    invitation.AgencyId == agencyId &&
                    invitation.NormalizedEmail == normalizedEmail &&
                    invitation.Status == AgencyInvitationStatus.Pending,
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
