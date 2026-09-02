using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RealEstate.Domain.Entities;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Persistence.Configurations;

namespace RealEstate.Tests.Integration.Listings;

public sealed class PostgreSqlListingLocationIntegrityTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTime ConfirmationTime =
        new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string, int> MetadataScalarLimits =>
        new()
        {
            { nameof(Listing.GeocodingProviderKey), 64 },
            { nameof(Listing.GeocodingResultReference), 512 },
            { nameof(Listing.GeocodedDisplayName), 500 }
        };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public PostgreSqlListingLocationIntegrityTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task LocationState_AcceptsUnresolvedSnapshot()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

        await UpdateSnapshotAsync(
            listingId,
            latitude: null,
            longitude: null,
            precision: null,
            providerKey: null,
            resultReference: null,
            displayName: null,
            confirmedAtUtc: null);
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(41.9981, 21.4254)]
    public async Task LocationState_AcceptsBoundedLegacyCoordinatePair(
        decimal latitude,
        decimal longitude)
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

        await UpdateSnapshotAsync(
            listingId,
            latitude,
            longitude,
            precision: null,
            providerKey: null,
            resultReference: null,
            displayName: null,
            confirmedAtUtc: null);
    }

    [Theory]
    [InlineData("ExactAddress")]
    [InlineData("Street")]
    [InlineData("Neighborhood")]
    [InlineData("Municipality")]
    [InlineData("City")]
    [InlineData("Approximate")]
    public async Task LocationState_AcceptsCompleteConfirmedSnapshot(
        string precision)
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

        await UpdateSnapshotAsync(
            listingId,
            -90m,
            180m,
            precision,
            "provider-Key",
            "Opaque:Result/ABC-123",
            "Скопје, Македонија",
            ConfirmationTime);

        await UpdateSnapshotAsync(
            listingId,
            90m,
            -180m,
            precision,
            "provider-Key",
            "Opaque:Result/ABC-123",
            displayName: null,
            ConfirmationTime);
    }

    [Theory]
    [MemberData(nameof(MetadataScalarLimits))]
    public async Task MetadataLength_MatchesPostgreSqlSupplementaryUnicodeScalarBoundary(
        string propertyName,
        int maximumScalarCount)
    {
        string acceptedValue =
            CreateSupplementaryScalarValue(maximumScalarCount);
        Guid acceptedListingId =
            await ListingTestHelpers.CreateListingAsync(_httpClient);

        await UpdateSnapshotAsync(
            acceptedListingId,
            ConfirmedWithMetadataValue(propertyName, acceptedValue));

        acceptedValue.Length.Should().Be(maximumScalarCount * 2);
        acceptedValue.EnumerateRunes().Count().Should().Be(maximumScalarCount);
        (await ReadMetadataValueAsync(acceptedListingId, propertyName))
            .Should().Be(acceptedValue);

        string rejectedValue =
            CreateSupplementaryScalarValue(maximumScalarCount + 1);
        Guid rejectedListingId =
            await ListingTestHelpers.CreateListingAsync(_httpClient);
        Func<Task> action = () => UpdateSnapshotAsync(
            rejectedListingId,
            ConfirmedWithMetadataValue(propertyName, rejectedValue));

        PostgresException exception =
            (await action.Should().ThrowAsync<PostgresException>()).Which;
        exception.SqlState.Should().Be(
            PostgresErrorCodes.StringDataRightTruncation);
    }

    public static TheoryData<decimal?, decimal?> SingletonCoordinateCases =>
        new()
        {
            { 41m, null },
            { null, 21m }
        };

    [Theory]
    [MemberData(nameof(SingletonCoordinateCases))]
    public async Task LocationState_RejectsSingletonCoordinate(
        decimal? latitude,
        decimal? longitude)
    {
        await AssertRejectedAsync(
            new SnapshotInput(latitude, longitude, null, null, null, null, null),
            ListingConfiguration.CoordinatePairConstraintName);
    }

    [Theory]
    [InlineData(-90.000001)]
    [InlineData(90.000001)]
    public async Task LocationState_RejectsLatitudeOutsideBounds(decimal latitude)
    {
        await AssertRejectedAsync(
            Legacy(latitude, 21m),
            ListingConfiguration.LatitudeRangeConstraintName);
    }

    [Theory]
    [InlineData(-180.000001)]
    [InlineData(180.000001)]
    public async Task LocationState_RejectsLongitudeOutsideBounds(decimal longitude)
    {
        await AssertRejectedAsync(
            Legacy(41m, longitude),
            ListingConfiguration.LongitudeRangeConstraintName);
    }

    [Fact]
    public async Task LocationState_RejectsUnknownPrecision()
    {
        await AssertRejectedAsync(
            Confirmed(precision: "BuildingRoof"),
            ListingConfiguration.LocationPrecisionConstraintName);
    }

    public static TheoryData<SnapshotInput> PartialSnapshotCases => new()
    {
        new(41m, 21m, "City", null, null, null, null),
        new(41m, 21m, null, "provider", null, null, null),
        new(41m, 21m, null, null, "result", null, null),
        new(41m, 21m, null, null, null, null, ConfirmationTime),
        new(41m, 21m, null, null, null, "display", null),
        new(null, null, "City", "provider", "result", null, ConfirmationTime),
        new(41m, 21m, null, "provider", "result", null, ConfirmationTime),
        new(41m, 21m, "City", null, "result", null, ConfirmationTime),
        new(41m, 21m, "City", "provider", null, null, ConfirmationTime),
        new(41m, 21m, "City", "provider", "result", null, null)
    };

    [Theory]
    [MemberData(nameof(PartialSnapshotCases))]
    public async Task LocationState_RejectsPartialOrMisplacedMetadata(
        SnapshotInput input)
    {
        await AssertRejectedAsync(
            input,
            ListingConfiguration.SnapshotStateConstraintName);
    }

    [Theory]
    [InlineData("provider", "", "result", null,
        ListingConfiguration.ProviderKeyConstraintName)]
    [InlineData("provider", "\u00A0provider", "result", null,
        ListingConfiguration.ProviderKeyConstraintName)]
    [InlineData("provider", "provider\u3000", "result", null,
        ListingConfiguration.ProviderKeyConstraintName)]
    [InlineData("result", "provider", "", null,
        ListingConfiguration.ResultReferenceConstraintName)]
    [InlineData("result", "provider", "\u2003result", null,
        ListingConfiguration.ResultReferenceConstraintName)]
    [InlineData("result", "provider", "result\u00A0", null,
        ListingConfiguration.ResultReferenceConstraintName)]
    [InlineData("display", "provider", "result", "",
        ListingConfiguration.DisplayNameConstraintName)]
    [InlineData("display", "provider", "result", "\u3000display",
        ListingConfiguration.DisplayNameConstraintName)]
    [InlineData("display", "provider", "result", "display\u202F",
        ListingConfiguration.DisplayNameConstraintName)]
    public async Task LocationState_RejectsBlankOrBoundaryUntrimmedMetadata(
        string field,
        string providerKey,
        string resultReference,
        string? displayName,
        string expectedConstraint)
    {
        field.Should().NotBeNullOrWhiteSpace();
        await AssertRejectedAsync(
            Confirmed(
                providerKey: providerKey,
                resultReference: resultReference,
                displayName: displayName),
            expectedConstraint);
    }

    private async Task AssertRejectedAsync(
        SnapshotInput input,
        string expectedConstraint)
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        Func<Task> action = () => UpdateSnapshotAsync(listingId, input);

        PostgresException exception =
            (await action.Should().ThrowAsync<PostgresException>()).Which;

        exception.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.Should().Be(expectedConstraint);
    }

    private Task UpdateSnapshotAsync(Guid listingId, SnapshotInput input)
    {
        return UpdateSnapshotAsync(
            listingId,
            input.Latitude,
            input.Longitude,
            input.Precision,
            input.ProviderKey,
            input.ResultReference,
            input.DisplayName,
            input.ConfirmedAtUtc);
    }

    private async Task UpdateSnapshotAsync(
        Guid listingId,
        decimal? latitude,
        decimal? longitude,
        string? precision,
        string? providerKey,
        string? resultReference,
        string? displayName,
        DateTime? confirmedAtUtc)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Listings"
             SET "Latitude" = {latitude},
                 "Longitude" = {longitude},
                 "LocationPrecision" = {precision},
                 "GeocodingProviderKey" = {providerKey},
                 "GeocodingResultReference" = {resultReference},
                 "GeocodedDisplayName" = {displayName},
                 "LocationConfirmedAtUtc" = {confirmedAtUtc}
             WHERE "Id" = {listingId}
             """);
    }

    private static SnapshotInput Legacy(decimal latitude, decimal longitude)
    {
        return new SnapshotInput(
            latitude,
            longitude,
            null,
            null,
            null,
            null,
            null);
    }

    private static SnapshotInput Confirmed(
        string precision = "ExactAddress",
        string providerKey = "provider",
        string resultReference = "Opaque/Result:ABC",
        string? displayName = null)
    {
        return new SnapshotInput(
            41m,
            21m,
            precision,
            providerKey,
            resultReference,
            displayName,
            ConfirmationTime);
    }

    private static SnapshotInput ConfirmedWithMetadataValue(
        string propertyName,
        string value)
    {
        return Confirmed(
            providerKey: propertyName == nameof(Listing.GeocodingProviderKey)
                ? value
                : "provider",
            resultReference:
                propertyName == nameof(Listing.GeocodingResultReference)
                    ? value
                    : "result",
            displayName: propertyName == nameof(Listing.GeocodedDisplayName)
                ? value
                : null);
    }

    private async Task<string?> ReadMetadataValueAsync(
        Guid listingId,
        string propertyName)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Listing listing = await dbContext.Listings
            .AsNoTracking()
            .SingleAsync(current => current.Id == listingId);

        return propertyName switch
        {
            nameof(Listing.GeocodingProviderKey) =>
                listing.GeocodingProviderKey,
            nameof(Listing.GeocodingResultReference) =>
                listing.GeocodingResultReference,
            nameof(Listing.GeocodedDisplayName) =>
                listing.GeocodedDisplayName,
            _ => throw new ArgumentOutOfRangeException(
                nameof(propertyName),
                propertyName,
                "Unknown location metadata property.")
        };
    }

    private static string CreateSupplementaryScalarValue(int scalarCount)
    {
        return string.Concat(Enumerable.Repeat("\U0001F642", scalarCount));
    }

    public sealed record SnapshotInput(
        decimal? Latitude,
        decimal? Longitude,
        string? Precision,
        string? ProviderKey,
        string? ResultReference,
        string? DisplayName,
        DateTime? ConfirmedAtUtc);
}
