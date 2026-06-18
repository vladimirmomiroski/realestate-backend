using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public sealed class ListingHouseDetailsConfiguration : IEntityTypeConfiguration<ListingHouseDetails>
{
    public void Configure(EntityTypeBuilder<ListingHouseDetails> builder)
    {
        builder.ToTable("ListingHouseDetails");

        builder.HasKey(details => details.ListingId);

        builder.Property(details => details.HouseType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(HouseType.Unknown);

        builder.Property(details => details.NumberOfFloors);

        builder.Property(details => details.YardAreaSquareMeters)
            .HasPrecision(10, 2);

        builder.HasOne(details => details.Listing)
            .WithOne(listing => listing.HouseDetails)
            .HasForeignKey<ListingHouseDetails>(details => details.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
