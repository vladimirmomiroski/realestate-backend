using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Listings;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public class ListingTranslationConfiguration : IEntityTypeConfiguration<ListingTranslation>
{
    public const string LanguageCodeConstraintName =
        "CK_ListingTranslations_LanguageCode_Canonical";

    public const string TitleConstraintName =
        "CK_ListingTranslations_Title_TrimmedNonBlank";

    public const string CityConstraintName =
        "CK_ListingTranslations_City_TrimmedNonBlank";

    public const string DescriptionConstraintName =
        "CK_ListingTranslations_Description_TrimmedNonBlank";

    private const string PostgreSqlLanguageCodePattern =
        "^[a-z]{2,3}(-[a-z0-9]{2,8})*$";

    private static readonly string PostgreSqlBoundaryWhitespaceExpression =
        string.Join(
            " || ",
            ListingTranslationRules.BoundaryWhitespaceCharacters
                .Select(character => $"chr({(int)character})"));

    public void Configure(EntityTypeBuilder<ListingTranslation> builder)
    {
        builder.ToTable(
            "ListingTranslations",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    LanguageCodeConstraintName,
                    $"""
                    "LanguageCode" <> ''
                    AND "LanguageCode" = btrim("LanguageCode", {PostgreSqlBoundaryWhitespaceExpression})
                    AND "LanguageCode" = lower("LanguageCode")
                    AND "LanguageCode" ~ '{PostgreSqlLanguageCodePattern}'
                    """);

                tableBuilder.HasCheckConstraint(
                    TitleConstraintName,
                    $"""
                    "Title" <> ''
                    AND "Title" = btrim("Title", {PostgreSqlBoundaryWhitespaceExpression})
                    """);

                tableBuilder.HasCheckConstraint(
                    CityConstraintName,
                    $"""
                    "City" IS NULL
                    OR (
                        "City" <> ''
                        AND "City" = btrim("City", {PostgreSqlBoundaryWhitespaceExpression})
                    )
                    """);

                tableBuilder.HasCheckConstraint(
                    DescriptionConstraintName,
                    $"""
                    "Description" IS NULL
                    OR (
                        "Description" <> ''
                        AND "Description" = btrim("Description", {PostgreSqlBoundaryWhitespaceExpression})
                    )
                    """);
            });

        builder.HasKey(translation => translation.Id);

        builder.Property(translation => translation.LanguageCode)
            .HasMaxLength(ListingTranslationRules.LanguageCodeMaxLength)
            .IsRequired();

        builder.Property(translation => translation.Title)
            .HasMaxLength(ListingTranslationRules.TitleMaxLength)
            .IsRequired();

        builder.Property(translation => translation.Description)
            .HasMaxLength(ListingTranslationRules.DescriptionMaxLength);

        builder.Property(translation => translation.AddressLine)
            .HasMaxLength(ListingTranslationRules.AddressLineMaxLength);

        builder.Property(translation => translation.City)
            .HasMaxLength(ListingTranslationRules.LocationMaxLength);

        builder.Property(translation => translation.Municipality)
            .HasMaxLength(ListingTranslationRules.LocationMaxLength);

        builder.Property(translation => translation.Neighborhood)
            .HasMaxLength(ListingTranslationRules.LocationMaxLength);

        builder.HasIndex(translation => new
        {
            translation.ListingId,
            translation.LanguageCode
        }).IsUnique();

        builder.HasIndex(translation => new
        {
            translation.Title,
            translation.City,
            translation.Municipality,
            translation.Neighborhood
        })
            .HasDatabaseName("IX_ListingTranslations_Q_Trigram")
            .HasMethod("gin")
            .HasOperators(
                "gin_trgm_ops",
                "gin_trgm_ops",
                "gin_trgm_ops",
                "gin_trgm_ops");
    }
}
