using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public class ListingImageConfiguration : IEntityTypeConfiguration<ListingImage>
{
    public void Configure(EntityTypeBuilder<ListingImage> builder)
    {
        builder.ToTable("ListingImages");

        builder.HasKey(image => image.Id);

        builder.Property(image => image.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(image => image.StoredFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(image => image.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(image => image.SizeBytes)
            .IsRequired();

        builder.Property(image => image.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(image => image.SortOrder)
            .IsRequired();

        builder.Property(image => image.IsPrimary)
            .IsRequired();

        builder.Property(image => image.CreatedAtUtc)
            .IsRequired();

        builder.Property(image => image.ModifiedAtUtc);

        builder.HasOne(image => image.Listing)
            .WithMany(listing => listing.Images)
            .HasForeignKey(image => image.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(image => new
        {
            image.ListingId,
            image.SortOrder
        });

        builder.HasIndex(image => image.ListingId)
            .IsUnique()
            .HasFilter("\"IsPrimary\" = true");
    }
}