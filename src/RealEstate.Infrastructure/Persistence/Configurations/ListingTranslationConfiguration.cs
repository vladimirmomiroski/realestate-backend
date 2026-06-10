using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public class ListingTranslationConfiguration : IEntityTypeConfiguration<ListingTranslation>
{
    public void Configure(EntityTypeBuilder<ListingTranslation> builder)
    {
        builder.ToTable("ListingTranslations");

        builder.HasKey(translation => translation.Id);

        builder.Property(translation => translation.LanguageCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(translation => translation.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(translation => translation.Description)
            .HasMaxLength(3000);

        builder.Property(translation => translation.AddressLine)
            .HasMaxLength(300);

        builder.Property(translation => translation.City)
            .HasMaxLength(100);

        builder.Property(translation => translation.Neighborhood)
            .HasMaxLength(100);

        builder.HasIndex(translation => new
        {
            translation.ListingId,
            translation.LanguageCode
        }).IsUnique();
    }
}