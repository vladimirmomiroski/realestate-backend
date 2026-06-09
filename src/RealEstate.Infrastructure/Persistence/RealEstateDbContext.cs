using Microsoft.EntityFrameworkCore;

namespace RealEstate.Infrastructure.Persistence;

public sealed class RealEstateDbContext(DbContextOptions<RealEstateDbContext> options)
    : DbContext(options)
{
}