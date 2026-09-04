using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public sealed class ListingLandDetailsConfiguration
    : IEntityTypeConfiguration<ListingLandDetails>
{
    public void Configure(EntityTypeBuilder<ListingLandDetails> builder)
    {
        builder.ToTable("ListingLandDetails");

        builder.HasKey(details => details.ListingId);

        builder.Property(details => details.LandType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(LandType.Unknown);

        builder.HasOne(details => details.Listing)
            .WithOne(listing => listing.LandDetails)
            .HasForeignKey<ListingLandDetails>(details => details.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
