using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("Listings");

        builder.HasKey(listing => listing.Id);

        builder.Property(listing => listing.CreatedByUserId);

        builder.HasIndex(listing => listing.CreatedByUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(listing => listing.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

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

        builder.Property(listing => listing.Bathrooms)
            .HasPrecision(4, 1);

        builder.Property(listing => listing.BalconyCount);

        builder.Property(listing => listing.ParkingSpaces);

        builder.Property(listing => listing.HasBasement);

        builder.Property(listing => listing.IsExchangePossible);

        builder.Property(listing => listing.HeatingType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(HeatingType.Unknown);

        builder.Property(listing => listing.FurnishingStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(FurnishingStatus.Unknown);

        builder.Property(listing => listing.Condition)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(PropertyCondition.Unknown);

        builder.Property(listing => listing.YearRenovated);

        builder.Property(listing => listing.Orientation)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(Orientation.Unknown);

        builder.Property(listing => listing.YearBuilt);

        builder.Property(listing => listing.Latitude)
            .HasPrecision(9, 6);

        builder.Property(listing => listing.Longitude)
            .HasPrecision(9, 6);

        builder.Property(listing => listing.CreatedAtUtc)
            .IsRequired();

        builder.Property(listing => listing.ModifiedAtUtc);

        builder.HasMany(listing => listing.Translations)
            .WithOne(translation => translation.Listing)
            .HasForeignKey(translation => translation.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}