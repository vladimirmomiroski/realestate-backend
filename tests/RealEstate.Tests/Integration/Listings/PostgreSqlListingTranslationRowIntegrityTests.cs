using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RealEstate.Domain.Entities;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Configurations;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlListingTranslationRowIntegrityTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly string[] BoundaryWhitespaceCases =
    [
        " ",
        "\t",
        "\r\n",
        "\u00A0",
        "\u2003",
        "\u3000"
    ];

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public PostgreSqlListingTranslationRowIntegrityTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task LanguageCodeConstraint_RejectsNoncanonicalPersistedValues()
    {
        Guid translationId = await CreateTranslationIdAsync();

        string[] invalidValues =
        [
            "",
            "EN",
            "e",
            "en_uk",
            "en-u",
            .. BoundaryWhitespaceCases.Select(whitespace => whitespace),
            .. BoundaryWhitespaceCases.Select(
                whitespace => $"{whitespace}en{whitespace}")
        ];

        foreach (string value in invalidValues)
        {
            await AssertCheckRejectedAsync(
                translationId,
                "LanguageCode",
                value,
                ListingTranslationConfiguration.LanguageCodeConstraintName);
        }
    }

    [Fact]
    public async Task TitleConstraint_RejectsBlankOrUntrimmedPersistedValues()
    {
        Guid translationId = await CreateTranslationIdAsync();

        foreach (string whitespace in BoundaryWhitespaceCases)
        {
            await AssertCheckRejectedAsync(
                translationId,
                "Title",
                whitespace,
                ListingTranslationConfiguration.TitleConstraintName);
            await AssertCheckRejectedAsync(
                translationId,
                "Title",
                $"{whitespace}Title{whitespace}",
                ListingTranslationConfiguration.TitleConstraintName);
        }
    }

    [Fact]
    public async Task OptionalCoreTextConstraints_AcceptNullAndRejectBlankOrUntrimmedValues()
    {
        Guid translationId = await CreateTranslationIdAsync();

        await SetColumnAsync(translationId, "City", null);
        await SetColumnAsync(translationId, "Description", null);

        foreach (string whitespace in BoundaryWhitespaceCases)
        {
            await AssertCheckRejectedAsync(
                translationId,
                "City",
                whitespace,
                ListingTranslationConfiguration.CityConstraintName);
            await AssertCheckRejectedAsync(
                translationId,
                "City",
                $"{whitespace}Skopje{whitespace}",
                ListingTranslationConfiguration.CityConstraintName);
            await AssertCheckRejectedAsync(
                translationId,
                "Description",
                whitespace,
                ListingTranslationConfiguration.DescriptionConstraintName);
            await AssertCheckRejectedAsync(
                translationId,
                "Description",
                $"{whitespace}Description{whitespace}",
                ListingTranslationConfiguration.DescriptionConstraintName);
        }
    }

    [Theory]
    [InlineData("AddressLine")]
    [InlineData("Municipality")]
    [InlineData("Neighborhood")]
    public async Task OptionalLocalizedLocationConstraints_AcceptNullAndMeaningfulValues(
        string columnName)
    {
        Guid translationId = await CreateTranslationIdAsync();

        await SetColumnAsync(translationId, columnName, null);
        await SetColumnAsync(translationId, columnName, "Meaningful location");
        await SetColumnAsync(translationId, columnName, "Улица Македонија 1");
    }

    [Theory]
    [InlineData(
        "AddressLine",
        ListingTranslationConfiguration.AddressLineConstraintName)]
    [InlineData(
        "Municipality",
        ListingTranslationConfiguration.MunicipalityConstraintName)]
    [InlineData(
        "Neighborhood",
        ListingTranslationConfiguration.NeighborhoodConstraintName)]
    public async Task OptionalLocalizedLocationConstraints_RejectBlankOrUntrimmedValues(
        string columnName,
        string constraintName)
    {
        Guid translationId = await CreateTranslationIdAsync();

        await AssertCheckRejectedAsync(
            translationId,
            columnName,
            string.Empty,
            constraintName);

        foreach (string whitespace in BoundaryWhitespaceCases)
        {
            await AssertCheckRejectedAsync(
                translationId,
                columnName,
                whitespace,
                constraintName);
            await AssertCheckRejectedAsync(
                translationId,
                columnName,
                $"{whitespace}Location",
                constraintName);
            await AssertCheckRejectedAsync(
                translationId,
                columnName,
                $"Location{whitespace}",
                constraintName);
        }
    }

    [Fact]
    public async Task EfModel_DeclaresOptionalLocalizedLocationConstraintsAndNullableColumns()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
        var entityType = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(ListingTranslation));

        entityType.Should().NotBeNull();
        string[] expectedConstraints =
        [
            ListingTranslationConfiguration.AddressLineConstraintName,
            ListingTranslationConfiguration.MunicipalityConstraintName,
            ListingTranslationConfiguration.NeighborhoodConstraintName
        ];
        Dictionary<string, string> checks = entityType!.GetCheckConstraints()
            .Where(constraint => expectedConstraints.Contains(constraint.Name))
            .ToDictionary(
                constraint => constraint.Name!,
                constraint => constraint.Sql!,
                StringComparer.Ordinal);

        checks.Keys.Should().BeEquivalentTo(expectedConstraints);

        foreach (string propertyName in new[]
        {
            nameof(ListingTranslation.AddressLine),
            nameof(ListingTranslation.Municipality),
            nameof(ListingTranslation.Neighborhood)
        })
        {
            entityType.FindProperty(propertyName)!.IsNullable.Should().BeTrue();
        }

        checks.Values.Should().OnlyContain(sql =>
            sql.Contains("IS NULL", StringComparison.Ordinal) &&
            sql.Contains("<> ''", StringComparison.Ordinal) &&
            sql.Contains("btrim", StringComparison.Ordinal) &&
            sql.Contains("chr(160)", StringComparison.Ordinal) &&
            sql.Contains("chr(8195)", StringComparison.Ordinal) &&
            sql.Contains("chr(12288)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RowIntegrityConstraints_CoexistWithUniqueAndTrigramIndexes()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
        DbConnection connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync();

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT conname, pg_get_constraintdef(oid)
                FROM pg_constraint
                WHERE conrelid = '"ListingTranslations"'::regclass
                    AND conname LIKE 'CK_ListingTranslations_%'
                ORDER BY conname;
                """;

            var constraints = new Dictionary<string, string>(
                StringComparer.Ordinal);
            await using (DbDataReader reader =
                await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    constraints.Add(reader.GetString(0), reader.GetString(1));
                }
            }

            constraints.Keys.Should().BeEquivalentTo(
                ListingTranslationConfiguration.LanguageCodeConstraintName,
                ListingTranslationConfiguration.TitleConstraintName,
                ListingTranslationConfiguration.CityConstraintName,
                ListingTranslationConfiguration.DescriptionConstraintName,
                ListingTranslationConfiguration.AddressLineConstraintName,
                ListingTranslationConfiguration.MunicipalityConstraintName,
                ListingTranslationConfiguration.NeighborhoodConstraintName);
            constraints.Values.Should().OnlyContain(definition =>
                definition.Contains("btrim", StringComparison.Ordinal) &&
                definition.Contains("chr(160)", StringComparison.Ordinal) &&
                definition.Contains("chr(8195)", StringComparison.Ordinal) &&
                definition.Contains("chr(12288)", StringComparison.Ordinal));
            constraints[ListingTranslationConfiguration.LanguageCodeConstraintName]
                .Should()
                .ContainAll("lower", "[a-z]{2,3}");

            await using DbCommand indexCommand = connection.CreateCommand();
            indexCommand.CommandText =
                """
                SELECT indexname
                FROM pg_indexes
                WHERE tablename = 'ListingTranslations'
                    AND indexname IN (
                        'IX_ListingTranslations_ListingId_LanguageCode',
                        'IX_ListingTranslations_Q_Trigram')
                ORDER BY indexname;
                """;

            var indexes = new List<string>();
            await using DbDataReader indexReader =
                await indexCommand.ExecuteReaderAsync();
            while (await indexReader.ReadAsync())
            {
                indexes.Add(indexReader.GetString(0));
            }

            indexes.Should().BeEquivalentTo(
                "IX_ListingTranslations_ListingId_LanguageCode",
                "IX_ListingTranslations_Q_Trigram");
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task<Guid> CreateTranslationIdAsync()
    {
        Guid listingId =
            await ListingTestHelpers.CreateListingAsync(_httpClient);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        return await dbContext.Set<RealEstate.Domain.Entities.ListingTranslation>()
            .Where(translation =>
                translation.ListingId == listingId &&
                translation.LanguageCode == "en")
            .Select(translation => translation.Id)
            .SingleAsync();
    }

    private async Task AssertCheckRejectedAsync(
        Guid translationId,
        string columnName,
        string value,
        string expectedConstraint)
    {
        Func<Task> action = () =>
            SetColumnAsync(translationId, columnName, value);

        PostgresException exception =
            (await action.Should().ThrowAsync<PostgresException>()).Which;

        exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.Should().Be(expectedConstraint);
    }

    private async Task SetColumnAsync(
        Guid translationId,
        string columnName,
        string? value)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        switch (columnName)
        {
            case "LanguageCode":
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE "ListingTranslations"
                     SET "LanguageCode" = {value}
                     WHERE "Id" = {translationId}
                     """);
                break;
            case "Title":
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE "ListingTranslations"
                     SET "Title" = {value}
                     WHERE "Id" = {translationId}
                     """);
                break;
            case "City":
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE "ListingTranslations"
                     SET "City" = {value}
                     WHERE "Id" = {translationId}
                     """);
                break;
            case "Description":
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE "ListingTranslations"
                     SET "Description" = {value}
                     WHERE "Id" = {translationId}
                     """);
                break;
            case "AddressLine":
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE "ListingTranslations"
                     SET "AddressLine" = {value}
                     WHERE "Id" = {translationId}
                     """);
                break;
            case "Municipality":
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE "ListingTranslations"
                     SET "Municipality" = {value}
                     WHERE "Id" = {translationId}
                     """);
                break;
            case "Neighborhood":
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     UPDATE "ListingTranslations"
                     SET "Neighborhood" = {value}
                     WHERE "Id" = {translationId}
                     """);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(columnName),
                    columnName,
                    "Unsupported ListingTranslation test column.");
        }
    }
}
