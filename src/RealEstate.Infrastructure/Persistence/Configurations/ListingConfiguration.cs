using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Domain.Listings;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public const string CoordinatePairConstraintName =
        "CK_Listings_Location_CoordinatePair";

    public const string LatitudeRangeConstraintName =
        "CK_Listings_Location_LatitudeRange";

    public const string LongitudeRangeConstraintName =
        "CK_Listings_Location_LongitudeRange";

    public const string LocationPrecisionConstraintName =
        "CK_Listings_Location_PrecisionDefined";

    public const string ProviderKeyConstraintName =
        "CK_Listings_Location_ProviderKeyTrimmedNonBlank";

    public const string ResultReferenceConstraintName =
        "CK_Listings_Location_ResultReferenceTrimmedNonBlank";

    public const string DisplayNameConstraintName =
        "CK_Listings_Location_DisplayNameTrimmedNonBlank";

    public const string SnapshotStateConstraintName =
        "CK_Listings_Location_SnapshotState";

    private static readonly string PostgreSqlBoundaryWhitespaceExpression =
        string.Join(
            " || ",
            ListingTranslationRules.BoundaryWhitespaceCharacters
                .Select(character => $"chr({(int)character})"));

    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable(
            "Listings",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    CoordinatePairConstraintName,
                    """
                    (
                        "Latitude" IS NULL
                        AND "Longitude" IS NULL
                    )
                    OR (
                        "Latitude" IS NOT NULL
                        AND "Longitude" IS NOT NULL
                    )
                    """);

                tableBuilder.HasCheckConstraint(
                    LatitudeRangeConstraintName,
                    "\"Latitude\" IS NULL OR \"Latitude\" BETWEEN -90 AND 90");

                tableBuilder.HasCheckConstraint(
                    LongitudeRangeConstraintName,
                    "\"Longitude\" IS NULL OR \"Longitude\" BETWEEN -180 AND 180");

                tableBuilder.HasCheckConstraint(
                    LocationPrecisionConstraintName,
                    """
                    "LocationPrecision" IS NULL
                    OR "LocationPrecision" IN (
                        'ExactAddress',
                        'Street',
                        'Neighborhood',
                        'Municipality',
                        'City',
                        'Approximate')
                    """);

                tableBuilder.HasCheckConstraint(
                    ProviderKeyConstraintName,
                    $"""
                    "GeocodingProviderKey" IS NULL
                    OR (
                        "GeocodingProviderKey" <> ''
                        AND "GeocodingProviderKey" = btrim("GeocodingProviderKey", {PostgreSqlBoundaryWhitespaceExpression})
                    )
                    """);

                tableBuilder.HasCheckConstraint(
                    ResultReferenceConstraintName,
                    $"""
                    "GeocodingResultReference" IS NULL
                    OR (
                        "GeocodingResultReference" <> ''
                        AND "GeocodingResultReference" = btrim("GeocodingResultReference", {PostgreSqlBoundaryWhitespaceExpression})
                    )
                    """);

                tableBuilder.HasCheckConstraint(
                    DisplayNameConstraintName,
                    $"""
                    "GeocodedDisplayName" IS NULL
                    OR (
                        "GeocodedDisplayName" <> ''
                        AND "GeocodedDisplayName" = btrim("GeocodedDisplayName", {PostgreSqlBoundaryWhitespaceExpression})
                    )
                    """);

                tableBuilder.HasCheckConstraint(
                    SnapshotStateConstraintName,
                    """
                    (
                        "Latitude" IS NULL
                        AND "Longitude" IS NULL
                        AND "LocationPrecision" IS NULL
                        AND "GeocodingProviderKey" IS NULL
                        AND "GeocodingResultReference" IS NULL
                        AND "GeocodedDisplayName" IS NULL
                        AND "LocationConfirmedAtUtc" IS NULL
                    )
                    OR (
                        "Latitude" IS NOT NULL
                        AND "Longitude" IS NOT NULL
                        AND (
                            (
                                "LocationPrecision" IS NULL
                                AND "GeocodingProviderKey" IS NULL
                                AND "GeocodingResultReference" IS NULL
                                AND "GeocodedDisplayName" IS NULL
                                AND "LocationConfirmedAtUtc" IS NULL
                            )
                            OR (
                                "LocationPrecision" IS NOT NULL
                                AND "GeocodingProviderKey" IS NOT NULL
                                AND "GeocodingResultReference" IS NOT NULL
                                AND "LocationConfirmedAtUtc" IS NOT NULL
                            )
                        )
                    )
                    """);
            });

        builder.HasKey(listing => listing.Id);

        builder.Property(listing => listing.CreatedByUserId);

        builder.HasIndex(listing => listing.CreatedByUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(listing => listing.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(listing => listing.AgencyId);

        builder.HasOne(listing => listing.Agency)
            .WithMany()
            .HasForeignKey(listing => listing.AgencyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(listing => listing.AgencyId);

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

        builder.Property(listing => listing.LocationPrecision)
            .HasConversion<string>()
            .HasMaxLength(ListingLocationRules.LocationPrecisionMaxLength);

        builder.Property(listing => listing.GeocodingProviderKey)
            .HasMaxLength(ListingLocationRules.GeocodingProviderKeyMaxLength);

        builder.Property(listing => listing.GeocodingResultReference)
            .HasMaxLength(ListingLocationRules.GeocodingResultReferenceMaxLength);

        builder.Property(listing => listing.GeocodedDisplayName)
            .HasMaxLength(ListingLocationRules.GeocodedDisplayNameMaxLength);

        builder.Property(listing => listing.LocationConfirmedAtUtc);

        builder.Property(listing => listing.CreatedAtUtc)
            .IsRequired();

        builder.Property(listing => listing.ModifiedAtUtc);

        builder.HasMany(listing => listing.Translations)
            .WithOne(translation => translation.Listing)
            .HasForeignKey(translation => translation.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
