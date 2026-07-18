using FluentAssertions;
using RealEstate.Application.Listings.Queries.GetComparableListings;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task GetComparables_WithLimitOutsideAllowedRange_ReturnsBadRequest(
        int limit)
    {
        // Arrange
        Guid sourceListingId = Guid.NewGuid();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceListingId}/comparables?limit={limit}");

        string responseBody =
            await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest);

        responseBody.Should().Contain(
            GetComparableListingsValidator.InvalidLimitError);
    }

    [Fact]
    public async Task GetComparables_WithMissingSource_ReturnsNotFound()
    {
        // Arrange
        Guid sourceListingId = Guid.NewGuid();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceListingId}/comparables");

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetComparables_WithNonActiveSource_ReturnsNotFound()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        Guid sourceListingId =
            await ListingTestHelpers.CreateListingAsync(
                _httpClient,
                currency: currency);

        // Listing remains Draft.

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceListingId}/comparables?lang=en");

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetComparables_WithActiveSourceAndNoCandidates_ReturnsEmptyArray()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        Guid sourceListingId =
            await ListingTestHelpers.CreateListingAsync(
                _httpClient,
                currency: currency);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            sourceListingId,
            ListingStatus.Active);

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceListingId}/comparables?lang=en");

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.OK);

        json.ValueKind.Should().Be(
            JsonValueKind.Array);

        json.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetComparables_WithActiveSourceWithoutTranslation_ReturnsEmptyArray()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        Guid sourceListingId =
            await ListingTestHelpers.CreateListingAsync(
                _httpClient,
                currency: currency);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            sourceListingId,
            ListingStatus.Active);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            sourceListingId);

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceListingId}/comparables?lang=en");

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.OK);

        json.ValueKind.Should().Be(
            JsonValueKind.Array);

        json.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetComparables_WithEligibleCandidate_ReturnsCandidateAndExcludesSource()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        Guid sourceListingId =
            await ListingTestHelpers.CreateListingAsync(
                _httpClient,
                price: 100_000m,
                currency: currency,
                areaSquareMeters: 50m);

        Guid candidateListingId =
            await ListingTestHelpers.CreateListingAsync(
                _httpClient,
                price: 110_000m,
                currency: currency,
                areaSquareMeters: 55m);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            sourceListingId,
            ListingStatus.Active);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            candidateListingId,
            ListingStatus.Active);

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceListingId}/comparables?lang=en");

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Guid[] returnedIds =
            json.EnumerateArray()
                .Select(item =>
                    item.GetProperty("id").GetGuid())
                .ToArray();

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.OK);

        returnedIds.Should().Equal(
            candidateListingId);

        returnedIds.Should().NotContain(
            sourceListingId);

        json[0]
            .GetProperty("languageCode")
            .GetString()
            .Should()
            .Be("en");
    }

    [Fact]
    public async Task GetComparables_AppliesAllMandatoryCandidateEligibility()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        string differentCurrency =
            currency == "ZZZ"
                ? "YYY"
                : "ZZZ";

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        async Task<Guid> CreateAsync(
            string listingCurrency)
        {
            return await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner,
                price: 100_000m,
                currency: listingCurrency,
                areaSquareMeters: 50m);
        }

        Guid sourceId =
            await CreateAsync(currency);

        Guid eligibleId =
            await CreateAsync(currency);

        Guid nonActiveId =
            await CreateAsync(currency);

        Guid wrongListingTypeId =
            await CreateAsync(currency);

        Guid wrongPropertyTypeId =
            await CreateAsync(currency);

        Guid wrongCurrencyId =
            await CreateAsync(differentCurrency);

        Guid zeroPriceId =
            await CreateAsync(currency);

        Guid zeroAreaId =
            await CreateAsync(currency);

        Guid differentLanguageId =
            await CreateAsync(currency);

        Guid differentCityId =
            await CreateAsync(currency);

        await ListingTestHelpers.UpdateComparableFieldsAsync(
            _factory,
            wrongListingTypeId,
            listingType: ListingType.Rent);

        await ListingTestHelpers.UpdateComparableFieldsAsync(
            _factory,
            wrongPropertyTypeId,
            propertyType: PropertyType.House);

        await ListingTestHelpers.UpdateComparableFieldsAsync(
            _factory,
            zeroPriceId,
            price: 0m);

        await ListingTestHelpers.UpdateComparableFieldsAsync(
            _factory,
            zeroAreaId,
            areaSquareMeters: 0m);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            eligibleId,
            CreateComparableTranslation(
                "en",
                "sKoPjE"));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            differentLanguageId,
            CreateComparableTranslation(
                "de",
                "Skopje"));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            differentCityId,
            CreateComparableTranslation(
                "en",
                "Skopje Center"));

        Guid[] activeListings =
        [
            sourceId,
        eligibleId,
        wrongListingTypeId,
        wrongPropertyTypeId,
        wrongCurrencyId,
        zeroPriceId,
        zeroAreaId,
        differentLanguageId,
        differentCityId
        ];

        foreach (Guid listingId in activeListings)
        {
            await ListingTestHelpers.SetListingStatusAsync(
                _factory,
                listingId,
                ListingStatus.Active);
        }

        // nonActiveId intentionally remains Draft.

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Guid[] returnedIds =
            json.EnumerateArray()
                .Select(item =>
                    item.GetProperty("id").GetGuid())
                .ToArray();

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.OK);

        returnedIds.Should().Equal(
            eligibleId);

        returnedIds.Should().NotContain(
            sourceId);

        returnedIds.Should().NotContain(
            nonActiveId);

        returnedIds.Should().NotContain(
            wrongListingTypeId);

        returnedIds.Should().NotContain(
            wrongPropertyTypeId);

        returnedIds.Should().NotContain(
            wrongCurrencyId);

        returnedIds.Should().NotContain(
            zeroPriceId);

        returnedIds.Should().NotContain(
            zeroAreaId);

        returnedIds.Should().NotContain(
            differentLanguageId);

        returnedIds.Should().NotContain(
            differentCityId);
    }

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

    [Fact]
    public async Task GetComparables_UsesDefaultAndExplicitLimit()
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

        for (int index = 0; index < 8; index++)
        {
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m + index,
                areaSquareMeters: 100m);
        }

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage fullResponse =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        HttpResponseMessage defaultResponse =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en");

        HttpResponseMessage explicitResponse =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=3");

        Guid[] fullIds =
            await ReadComparableIdsAsync(fullResponse);

        Guid[] defaultIds =
            await ReadComparableIdsAsync(defaultResponse);

        Guid[] explicitIds =
            await ReadComparableIdsAsync(explicitResponse);

        // Assert
        fullIds.Should().HaveCount(8);

        defaultIds.Should().Equal(
            fullIds.Take(6));

        explicitIds.Should().Equal(
            fullIds.Take(3));
    }

    private static ListingTranslation CreateComparableTranslation(
    string languageCode,
    string city,
    string municipality = "Centar",
    string neighborhood = "Center")
    {
        return new ListingTranslation
        {
            Id = Guid.NewGuid(),
            LanguageCode = languageCode,
            Title = $"Comparable {languageCode}",
            Description = "Comparable test description",
            AddressLine = "Comparable address",
            City = city,
            Municipality = municipality,
            Neighborhood = neighborhood
        };
    }

    private static string CreateUniqueCurrency()
    {
        byte[] bytes =
            Guid.NewGuid().ToByteArray();

        return new string(
            bytes
                .Take(3)
                .Select(value =>
                    (char)('A' + value % 26))
                .ToArray());
    }

    private async Task<Guid> CreateActiveComparableAsync(
    AuthenticatedTestUser owner,
    string currency,
    decimal price,
    decimal areaSquareMeters,
    string city = "Skopje",
    string municipality = "Centar",
    string neighborhood = "Center",
    DateTime? createdAtUtc = null)
    {
        Guid listingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner,
                price: price,
                currency: currency,
                areaSquareMeters: areaSquareMeters);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId,
            CreateComparableTranslation(
                "en",
                city,
                municipality,
                neighborhood));

        if (createdAtUtc.HasValue)
        {
            await ListingTestHelpers
                .SetListingStatusAndCreatedAtUtcAsync(
                    _factory,
                    listingId,
                    ListingStatus.Active,
                    createdAtUtc.Value);
        }
        else
        {
            await ListingTestHelpers.SetListingStatusAsync(
                _factory,
                listingId,
                ListingStatus.Active);
        }

        return listingId;
    }

    private static async Task<Guid[]> ReadComparableIdsAsync(
        HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(
            HttpStatusCode.OK);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.ValueKind.Should().Be(
            JsonValueKind.Array);

        return json.EnumerateArray()
            .Select(item =>
                item.GetProperty("id").GetGuid())
            .ToArray();
    }
}
