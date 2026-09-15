using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public sealed class ListingCommercialDetailsConfiguration
    : IEntityTypeConfiguration<ListingCommercialDetails>
{
    public void Configure(EntityTypeBuilder<ListingCommercialDetails> builder)
    {
        builder.ToTable("ListingCommercialDetails");

        builder.HasKey(details => details.ListingId);

        builder.Property(details => details.CommercialType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(CommercialType.Unknown);

        builder.HasOne(details => details.Listing)
            .WithOne(listing => listing.CommercialDetails)
            .HasForeignKey<ListingCommercialDetails>(details => details.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
