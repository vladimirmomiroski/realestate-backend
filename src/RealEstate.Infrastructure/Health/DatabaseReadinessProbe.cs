using Microsoft.EntityFrameworkCore;
using RealEstate.Application.Common.Health;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Infrastructure.Health;

public sealed class DatabaseReadinessProbe : IDatabaseReadinessProbe
{
    private readonly RealEstateDbContext _dbContext;

    public DatabaseReadinessProbe(
        RealEstateDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> CanConnectAsync(
        CancellationToken cancellationToken)
    {
        return _dbContext.Database.CanConnectAsync(
            cancellationToken);
    }
}
