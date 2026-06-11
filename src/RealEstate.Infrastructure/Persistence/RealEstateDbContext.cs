using Microsoft.EntityFrameworkCore;
using RealEstate.Domain.Entities;

namespace RealEstate.Infrastructure.Persistence;


public sealed class RealEstateDbContext(DbContextOptions<RealEstateDbContext> options)
    : DbContext(options)
{

    public DbSet<Listing> Listings => Set<Listing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RealEstateDbContext).Assembly);
    }
}