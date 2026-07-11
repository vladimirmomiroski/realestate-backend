using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public sealed class AgencyConfiguration : IEntityTypeConfiguration<Agency>
{
    public void Configure(EntityTypeBuilder<Agency> builder)
    {
        builder.ToTable("Agencies");

        builder.HasKey(agency => agency.Id);

        builder.Property(agency => agency.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(agency => agency.Slug)
            .HasMaxLength(220)
            .IsRequired();

        builder.HasIndex(agency => agency.Slug)
            .IsUnique();

        builder.Property(agency => agency.Description)
            .HasMaxLength(2000);

        builder.Property(agency => agency.LogoUrl)
            .HasMaxLength(500);

        builder.Property(agency => agency.LogoStoredFileName)
            .HasMaxLength(255);

        builder.Property(agency => agency.LogoContentType)
            .HasMaxLength(100);

        builder.Property(agency => agency.LogoSizeBytes);

        builder.Property(agency => agency.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(agency => agency.Email)
            .HasMaxLength(320);

        builder.Property(agency => agency.WebsiteUrl)
            .HasMaxLength(500);

        builder.Property(agency => agency.AddressLine)
            .HasMaxLength(300);

        builder.Property(agency => agency.City)
            .HasMaxLength(100);

        builder.Property(agency => agency.Municipality)
            .HasMaxLength(100);

        builder.Property(agency => agency.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(agency => agency.CreatedAtUtc)
            .IsRequired();

        builder.Property(agency => agency.ModifiedAtUtc);
    }
}
