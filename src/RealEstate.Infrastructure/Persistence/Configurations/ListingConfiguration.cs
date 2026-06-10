using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;
using System.Reflection;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("Listings");

        builder.HasKey(listing => listing.Id);

        builder.Property(listing => listing.ListingType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(listing => listing.PropertyType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(listing => listing.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(listing => listing.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(listing => listing.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(listing => listing.AreaSquareMeters)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(listing => listing.Rooms)
            .HasPrecision(4, 1);

        builder.Property(listing => listing.Latitude)
            .HasPrecision(9, 6);

        builder.Property(listing => listing.Longitude)
            .HasPrecision(9, 6);

        builder.Property(listing => listing.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(listing => listing.Translations)
            .WithOne(translation => translation.Listing)
            .HasForeignKey(translation => translation.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}