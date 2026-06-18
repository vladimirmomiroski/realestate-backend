using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public sealed class ListingApartmentDetailsConfiguration : IEntityTypeConfiguration<ListingApartmentDetails>
{
    public void Configure(EntityTypeBuilder<ListingApartmentDetails> builder)
    {
        builder.ToTable("ListingApartmentDetails");

        builder.HasKey(details => details.ListingId);

        builder.Property(details => details.ApartmentType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ApartmentType.Unknown);

        builder.Property(details => details.Floor);

        builder.Property(details => details.TotalFloors);

        builder.Property(details => details.HasElevator);

        builder.HasOne(details => details.Listing)
            .WithOne(listing => listing.ApartmentDetails)
            .HasForeignKey<ListingApartmentDetails>(details => details.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
