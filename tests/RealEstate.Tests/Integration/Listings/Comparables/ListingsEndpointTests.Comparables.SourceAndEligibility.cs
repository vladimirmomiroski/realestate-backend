using FluentAssertions;
using RealEstate.Application.Listings.Queries.GetComparableListings;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Archived)]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Rented)]
    public async Task GetComparables_WithNonActiveSource_ReturnsNotFound(
    ListingStatus status)
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
            status);

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetComparables_WithActiveSourceAndBlankCity_ReturnsEmptyArray(
    string? city)
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        Guid sourceListingId =
            await ListingTestHelpers.CreateListingAsync(
                _httpClient,
                price: 100_000m,
                currency: currency,
                areaSquareMeters: 100m);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            sourceListingId,
            CreateComparableTranslation(
                "en",
                city));

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            sourceListingId,
            ListingStatus.Active);

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceListingId}/comparables?lang=en");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(100_000, 0)]
    [InlineData(100_000, -1)]
    public async Task GetComparables_WithActiveSourceAndNonPositivePriceOrArea_ReturnsEmptyArray(
    int price,
    int areaSquareMeters)
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        Guid sourceListingId =
            await ListingTestHelpers.CreateListingAsync(
                _httpClient,
                price: 100_000m,
                currency: currency,
                areaSquareMeters: 100m);

        await ListingTestHelpers.UpdateComparableFieldsAsync(
            _factory,
            sourceListingId,
            price: price,
            areaSquareMeters: areaSquareMeters);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            sourceListingId,
            ListingStatus.Active);

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceListingId}/comparables?lang=en");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, "   ")]
    public async Task GetComparables_ExcludesCandidateWithoutTranslationOrNonblankCity(
    bool removeTranslations,
    string? invalidCity)
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

        Guid controlCandidateId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        Guid invalidCandidateId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        if (removeTranslations)
        {
            await ListingTestHelpers.ReplaceListingTranslationsAsync(
                _factory,
                invalidCandidateId);
        }
        else
        {
            await ListingTestHelpers.ReplaceListingTranslationsAsync(
                _factory,
                invalidCandidateId,
                CreateComparableTranslation(
                    "en",
                    invalidCity));
        }

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(
            controlCandidateId);

        returnedIds.Should().NotContain(
            invalidCandidateId);
    }
}
