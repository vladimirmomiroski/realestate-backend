using Microsoft.EntityFrameworkCore;
using RealEstate.Domain.Entities;

namespace RealEstate.Infrastructure.Persistence;


public sealed class RealEstateDbContext(DbContextOptions<RealEstateDbContext> options)
    : DbContext(options)
{

    public DbSet<Listing> Listings { get; set; }

    public DbSet<ListingTranslation> ListingTranslations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RealEstateDbContext).Assembly);
    }
}