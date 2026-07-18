using FluentAssertions;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task GetComparables_OrdersByLocationTier()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid sourceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        Guid tierTwoId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m,
                municipality: "Karpos",
                neighborhood: "Vlae");

        Guid tierOneId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m,
                municipality: "Centar",
                neighborhood: "Debar Maalo");

        Guid tierZeroId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m,
                municipality: "Centar",
                neighborhood: "Center");

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(
            tierZeroId,
            tierOneId,
            tierTwoId);
    }

    [Fact]
    public async Task GetComparables_OrdersByRelativeAreaDifference()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid sourceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        Guid fartherAreaId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 80_000m,
                areaSquareMeters: 80m);

        Guid closerAreaId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 95_000m,
                areaSquareMeters: 95m);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(
            closerAreaId,
            fartherAreaId);
    }

    [Fact]
    public async Task GetComparables_OrdersByUnroundedPricePerSquareMeterDifference()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid sourceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        Guid largerPsmDifferenceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 99_000.40m,
                areaSquareMeters: 99m);

        Guid smallerPsmDifferenceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 101_000.10m,
                areaSquareMeters: 101m);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(
            smallerPsmDifferenceId,
            largerPsmDifferenceId);
    }

    [Fact]
    public async Task GetComparables_OrdersByRelativePriceDifference()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid sourceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        Guid smallerPriceDifferenceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 81_000m,
                areaSquareMeters: 90m);

        Guid largerPriceDifferenceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 121_000m,
                areaSquareMeters: 110m);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(
            smallerPriceDifferenceId,
            largerPriceDifferenceId);
    }

    [Fact]
    public async Task GetComparables_UsesCreatedAtThenUuidDescendingTieBreakers()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        DateTime olderTimestamp =
            new(
                2026,
                1,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        DateTime newerTimestamp =
            olderTimestamp.AddDays(1);

        Guid sourceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        Guid olderId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m,
                createdAtUtc: olderTimestamp);

        Guid newerFirstId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m,
                createdAtUtc: newerTimestamp);

        Guid newerSecondId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m,
                createdAtUtc: newerTimestamp);

        Guid[] equalTimestampIds =
        [
            newerFirstId,
        newerSecondId
        ];

        Guid[] expectedNewerOrder =
            equalTimestampIds
                .OrderByDescending(
                    id => id.ToString("D"),
                    StringComparer.Ordinal)
                .ToArray();

        Guid[] expectedIds =
            expectedNewerOrder
                .Append(olderId)
                .ToArray();

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(expectedIds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetComparables_BlankMunicipalityDoesNotEarnTierZeroOrOne(
    bool blankOnSource)
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        string sourceMunicipality =
            blankOnSource
                ? "   "
                : "Centar";

        Guid sourceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m,
                municipality: sourceMunicipality,
                neighborhood: "Center");

        Guid blankMunicipalityCandidateId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 120_000m,
                areaSquareMeters: 120m,
                municipality: "   ",
                neighborhood: "Center");

        Guid sameCorrectTierControlId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 101_000m,
                areaSquareMeters: 101m,
                municipality: "Karpos",
                neighborhood: "Vlae");

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(
            sameCorrectTierControlId,
            blankMunicipalityCandidateId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetComparables_BlankNeighborhoodDoesNotEarnTierZero(
    bool blankOnSource)
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        string sourceNeighborhood =
            blankOnSource
                ? "   "
                : "Center";

        Guid sourceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m,
                municipality: "Centar",
                neighborhood: sourceNeighborhood);

        Guid blankNeighborhoodCandidateId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 120_000m,
                areaSquareMeters: 120m,
                municipality: "Centar",
                neighborhood: "   ");

        Guid sameCorrectTierControlId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 101_000m,
                areaSquareMeters: 101m,
                municipality: "Centar",
                neighborhood: "Debar Maalo");

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(
            sameCorrectTierControlId,
            blankNeighborhoodCandidateId);
    }

    [Fact]
    public async Task GetComparables_MatchingNeighborhoodWithoutMatchingMunicipalityDoesNotEarnTierZero()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid sourceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m,
                municipality: "Centar",
                neighborhood: "Center");

        Guid matchingNeighborhoodOnlyCandidateId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 120_000m,
                areaSquareMeters: 120m,
                municipality: "Karpos",
                neighborhood: "Center");

        Guid tierTwoControlId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 101_000m,
                areaSquareMeters: 101m,
                municipality: "Aerodrom",
                neighborhood: "Jane Sandanski");

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(
            tierTwoControlId,
            matchingNeighborhoodOnlyCandidateId);
    }
}
