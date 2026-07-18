using FluentAssertions;
using RealEstate.Application.Listings.Queries.GetComparableListings;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RealEstate.Tests.Integration.Auth;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Agencies;
using System.Text.Json.Nodes;

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

    [Fact]
    public async Task GetComparables_RequestedLanguageSelection_IsCaseInsensitiveAndUsesSameResponseTranslation()
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

        Guid candidateId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            sourceId,
            CreateComparableTranslation(
                "mk",
                "Source Macedonian Decoy",
                title: "Source Macedonian decoy"),
            CreateComparableTranslation(
                "EN",
                "Requested City",
                municipality: "Requested Municipality",
                neighborhood: "Requested Neighborhood",
                title: "Source requested translation"));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            candidateId,
            CreateComparableTranslation(
                "mk",
                "Candidate Macedonian Decoy",
                title: "Candidate Macedonian decoy"),
            CreateComparableTranslation(
                "EN",
                "Requested City",
                municipality: "Requested Municipality",
                neighborhood: "Requested Neighborhood",
                title: "Candidate requested translation"));

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=%20eN%20");

        JsonElement item =
            await ReadSingleComparableAsync(
                response,
                candidateId);

        // Assert
        AssertSelectedTranslation(
            item,
            languageCode: "EN",
            title: "Candidate requested translation",
            city: "Requested City",
            municipality: "Requested Municipality",
            neighborhood: "Requested Neighborhood");
    }

    [Fact]
    public async Task GetComparables_WhenRequestedLanguageIsMissing_UsesMacedonianForEligibilityAndResponse()
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

        Guid candidateId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            sourceId,
            CreateComparableTranslation(
                "en",
                "Source English Decoy",
                title: "Source English decoy"),
            CreateComparableTranslation(
                "mk",
                "Fallback City",
                municipality: "Fallback Municipality",
                neighborhood: "Fallback Neighborhood",
                title: "Source Macedonian translation"));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            candidateId,
            CreateComparableTranslation(
                "en",
                "Candidate English Decoy",
                title: "Candidate English decoy"),
            CreateComparableTranslation(
                "mk",
                "Fallback City",
                municipality: "Fallback Municipality",
                neighborhood: "Fallback Neighborhood",
                title: "Candidate Macedonian translation"));

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=de");

        JsonElement item =
            await ReadSingleComparableAsync(
                response,
                candidateId);

        // Assert
        AssertSelectedTranslation(
            item,
            languageCode: "mk",
            title: "Candidate Macedonian translation",
            city: "Fallback City",
            municipality: "Fallback Municipality",
            neighborhood: "Fallback Neighborhood");
    }

    [Fact]
    public async Task GetComparables_WhenRequestedAndMacedonianAreMissing_UsesPostgreSqlCOrderedTranslation()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        const string lowerCollatedLanguage =
            "\uE000";

        const string higherCollatedLanguage =
            "\U00010000";

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid sourceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        Guid candidateId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            sourceId,
            CreateComparableTranslation(
                higherCollatedLanguage,
                "Source Higher Decoy",
                title: "Source higher-collated decoy"),
            CreateComparableTranslation(
                lowerCollatedLanguage,
                "C Ordered City",
                municipality: "C Ordered Municipality",
                neighborhood: "C Ordered Neighborhood",
                title: "Source C-selected translation"));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            candidateId,
            CreateComparableTranslation(
                higherCollatedLanguage,
                "Candidate Higher Decoy",
                title: "Candidate higher-collated decoy"),
            CreateComparableTranslation(
                lowerCollatedLanguage,
                "C Ordered City",
                municipality: "C Ordered Municipality",
                neighborhood: "C Ordered Neighborhood",
                title: "Candidate C-selected translation"));

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=de");

        JsonElement item =
            await ReadSingleComparableAsync(
                response,
                candidateId);

        // Assert
        AssertSelectedTranslation(
            item,
            languageCode: lowerCollatedLanguage,
            title: "Candidate C-selected translation",
            city: "C Ordered City",
            municipality: "C Ordered Municipality",
            neighborhood: "C Ordered Neighborhood");
    }

    [Fact]
    public async Task GetComparables_WhenRequestedPriorityTies_UsesPostgreSqlCOrderingForEligibilityAndResponse()
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

        Guid candidateId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        // Both "en" and "EN" match the requested language
        // case-insensitively. PostgreSQL COLLATE "C" must
        // choose "EN" before "en".
        //
        // Lowercase is deliberately inserted first so
        // insertion order cannot make the test pass.

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            sourceId,
            CreateComparableTranslation(
                "en",
                "Source Lowercase Decoy",
                title: "Source lowercase decoy"),
            CreateComparableTranslation(
                "EN",
                "Case Ordered City",
                municipality: "Case Ordered Municipality",
                neighborhood: "Case Ordered Neighborhood",
                title: "Source uppercase selected"));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            candidateId,
            CreateComparableTranslation(
                "en",
                "Candidate Lowercase Decoy",
                title: "Candidate lowercase decoy"),
            CreateComparableTranslation(
                "EN",
                "Case Ordered City",
                municipality: "Case Ordered Municipality",
                neighborhood: "Case Ordered Neighborhood",
                title: "Candidate uppercase selected"));

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en");

        JsonElement item =
            await ReadSingleComparableAsync(
                response,
                candidateId);

        // Assert
        AssertSelectedTranslation(
            item,
            languageCode: "EN",
            title: "Candidate uppercase selected",
            city: "Case Ordered City",
            municipality: "Case Ordered Municipality",
            neighborhood: "Case Ordered Neighborhood");
    }

    [Theory]
    [InlineData(
    "Percent%City",
    "PercentWildcardCity")]
    [InlineData(
    "Under_City",
    "UnderXCity")]
    [InlineData(
    "Slash\\City",
    "SlashCity")]
    public async Task GetComparables_CityWildcardCharactersAreLiteral(
    string literalCity,
    string wildcardShapedDecoyCity)
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
                city: literalCity);

        Guid exactCandidateId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m,
                city: literalCity);

        Guid decoyCandidateId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m,
                city: wildcardShapedDecoyCity);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(
            exactCandidateId);

        returnedIds.Should().NotContain(
            decoyCandidateId);
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

    [Fact]
    public async Task GetComparables_AcceptsBoundaryLimitsAndCapsResults()
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

        DateTime baseTimestamp =
            new(
                2026,
                2,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        var candidateIdsInCreationOrder =
            new List<Guid>();

        for (int index = 0; index < 13; index++)
        {
            Guid candidateId =
                await CreateActiveComparableAsync(
                    owner,
                    currency,
                    price: 100_000m,
                    areaSquareMeters: 100m,
                    createdAtUtc:
                        baseTimestamp.AddMinutes(index));

            candidateIdsInCreationOrder.Add(
                candidateId);
        }

        Guid[] expectedOrder =
            candidateIdsInCreationOrder
                .AsEnumerable()
                .Reverse()
                .ToArray();

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage limitOneResponse =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=1");

        HttpResponseMessage limitTwelveResponse =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        Guid[] limitOneIds =
            await ReadComparableIdsAsync(
                limitOneResponse);

        Guid[] limitTwelveIds =
            await ReadComparableIdsAsync(
                limitTwelveResponse);

        // Assert
        limitOneIds.Should().Equal(
            expectedOrder.Take(1));

        limitTwelveIds.Should().Equal(
            expectedOrder.Take(12));

        limitOneIds.Should().HaveCount(1);
        limitTwelveIds.Should().HaveCount(12);

        limitTwelveIds.Should().NotContain(
            expectedOrder[12]);
    }

    [Fact]
    public async Task GetComparables_IgnoresCoordinatesRoomsAndApartmentDetails()
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
                3,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        DateTime newerTimestamp =
            olderTimestamp.AddDays(1);

        object sourceRequest =
            ListingTestHelpers.CreateValidListingRequest(
                price: 100_000m,
                currency: currency,
                areaSquareMeters: 100m,
                rooms: null,
                latitude: null,
                longitude: null);

        Guid sourceId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                sourceRequest,
                title: "Ignored fields source",
                createdAtUtc: olderTimestamp);

        object nullFieldsCandidateRequest =
            ListingTestHelpers.CreateValidListingRequest(
                price: 100_000m,
                currency: currency,
                areaSquareMeters: 100m,
                rooms: null,
                latitude: null,
                longitude: null);

        Guid nullFieldsCandidateId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                nullFieldsCandidateRequest,
                title: "Null ignored fields candidate",
                createdAtUtc: olderTimestamp);

        JsonObject divergentCandidateRequest =
            JsonSerializer.SerializeToNode(
                ListingTestHelpers.CreateValidListingRequest(
                    price: 100_000m,
                    currency: currency,
                    areaSquareMeters: 100m,
                    rooms: 7m,
                    latitude: -45.123456m,
                    longitude: 120.654321m))!
                .AsObject();

        JsonObject divergentApartmentDetails =
            divergentCandidateRequest[
                "apartmentDetails"]!
                .AsObject();

        divergentApartmentDetails["floor"] = 19;
        divergentApartmentDetails["totalFloors"] = 20;
        divergentApartmentDetails["hasElevator"] = false;

        Guid divergentFieldsCandidateId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                divergentCandidateRequest,
                title: "Divergent ignored fields candidate",
                createdAtUtc: newerTimestamp);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(
            divergentFieldsCandidateId,
            nullFieldsCandidateId);
    }

    [Fact]
    public async Task GetComparables_PersonalAndAgencyCandidatesCompeteTogetherAndPreserveResponseShape()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId =
            await CreateComparableAgencyWithOwnerAsync(
                owner.UserId);

        DateTime olderTimestamp =
            new(
                2026,
                4,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        DateTime newerTimestamp =
            olderTimestamp.AddDays(1);

        object sourceRequest =
            ListingTestHelpers.CreateValidListingRequest(
                price: 100_000m,
                currency: currency,
                areaSquareMeters: 100m,
                latitude: 41.998123m,
                longitude: 21.425456m);

        Guid sourceId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                sourceRequest,
                title: "Comparable source",
                createdAtUtc: olderTimestamp);

        object personalRequest =
            ListingTestHelpers.CreateValidListingRequest(
                price: 100_000m,
                currency: currency,
                areaSquareMeters: 100m,
                latitude: 41.900001m,
                longitude: 21.400001m);

        Guid personalCandidateId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                personalRequest,
                title: "Personal comparable",
                createdAtUtc: olderTimestamp);

        object agencyRequest =
            ListingTestHelpers.CreateValidListingRequest(
                price: 100_000m,
                agencyId: agencyId,
                currency: currency,
                areaSquareMeters: 100m,
                latitude: 42.100001m,
                longitude: 22.100001m);

        Guid agencyCandidateId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                agencyRequest,
                title: "Agency comparable",
                createdAtUtc: newerTimestamp);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.OK);

        json.ValueKind.Should().Be(
            JsonValueKind.Array);

        json.GetArrayLength().Should().Be(2);

        Guid[] returnedIds =
            json.EnumerateArray()
                .Select(item =>
                    item.GetProperty("id").GetGuid())
                .ToArray();

        returnedIds.Should().Equal(
            agencyCandidateId,
            personalCandidateId);

        JsonElement agencyItem =
            json.EnumerateArray()
                .Single(item =>
                    item.GetProperty("id").GetGuid() ==
                    agencyCandidateId);

        JsonElement personalItem =
            json.EnumerateArray()
                .Single(item =>
                    item.GetProperty("id").GetGuid() ==
                    personalCandidateId);

        AssertSelectedTranslation(
            agencyItem,
            languageCode: "en",
            title: "Agency comparable",
            city: "Skopje",
            municipality: "Centar",
            neighborhood: "Center");

        AssertSelectedTranslation(
            personalItem,
            languageCode: "en",
            title: "Personal comparable",
            city: "Skopje",
            municipality: "Centar",
            neighborhood: "Center");

        agencyItem.GetProperty("agencyId")
            .GetGuid()
            .Should()
            .Be(agencyId);

        personalItem.GetProperty("agencyId")
            .ValueKind.Should()
            .Be(JsonValueKind.Null);

        agencyItem.GetProperty("latitude")
            .GetDecimal()
            .Should()
            .Be(42.100001m);

        agencyItem.GetProperty("longitude")
            .GetDecimal()
            .Should()
            .Be(22.100001m);

        personalItem.GetProperty("latitude")
            .GetDecimal()
            .Should()
            .Be(41.900001m);

        personalItem.GetProperty("longitude")
            .GetDecimal()
            .Should()
            .Be(21.400001m);

        foreach (JsonElement item in json.EnumerateArray())
        {
            item.GetProperty("apartmentDetails")
                .ValueKind.Should()
                .Be(JsonValueKind.Object);

            item.GetProperty("houseDetails")
                .ValueKind.Should()
                .Be(JsonValueKind.Null);

            item.GetProperty("images")
                .ValueKind.Should()
                .Be(JsonValueKind.Array);

            item.GetProperty("images")
                .GetArrayLength()
                .Should()
                .Be(0);

            item.GetProperty("primaryImageUrl")
                .ValueKind.Should()
                .Be(JsonValueKind.Null);

            item.TryGetProperty(
                    "score",
                    out _)
                .Should()
                .BeFalse();

            item.TryGetProperty(
                    "rank",
                    out _)
                .Should()
                .BeFalse();

            item.TryGetProperty(
                    "ranking",
                    out _)
                .Should()
                .BeFalse();

            item.TryGetProperty(
                    "comparableScore",
                    out _)
                .Should()
                .BeFalse();
        }
    }

    private async Task<Guid> CreateActiveComparableFromRequestAsync(
    AuthenticatedTestUser owner,
    object request,
    string title,
    DateTime createdAtUtc)
    {
        Guid listingId;

        _httpClient.AuthorizeAs(
            owner.AccessToken);

        try
        {
            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync(
                    "/api/listings",
                    request);

            response.StatusCode.Should().Be(
                HttpStatusCode.Created);

            JsonElement json =
                await response.Content
                    .ReadFromJsonAsync<JsonElement>();

            listingId =
                json.GetProperty("id").GetGuid();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId,
            CreateComparableTranslation(
                "en",
                "Skopje",
                municipality: "Centar",
                neighborhood: "Center",
                title: title));

        await ListingTestHelpers
            .SetListingStatusAndCreatedAtUtcAsync(
                _factory,
                listingId,
                ListingStatus.Active,
                createdAtUtc);

        return listingId;
    }

    private async Task<Guid> CreateComparableAgencyWithOwnerAsync(
        Guid ownerUserId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        Agency agency =
            AgencyTestHelpers.CreateAgency();

        agency.AddMember(
            ownerUserId,
            AgencyMemberRole.Owner);

        dbContext.Agencies.Add(
            agency);

        await dbContext.SaveChangesAsync();

        return agency.Id;
    }

    private static ListingTranslation CreateComparableTranslation(
    string languageCode,
    string? city,
    string? municipality = "Centar",
    string? neighborhood = "Center",
    string? title = null,
    Guid? id = null)
    {
        return new ListingTranslation
        {
            Id = id ?? Guid.NewGuid(),
            LanguageCode = languageCode,
            Title = title ?? $"Comparable {languageCode}",
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

    private static async Task<JsonElement> ReadSingleComparableAsync(
    HttpResponseMessage response,
    Guid expectedListingId)
    {
        response.StatusCode.Should().Be(
            HttpStatusCode.OK);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.ValueKind.Should().Be(
            JsonValueKind.Array);

        json.GetArrayLength().Should().Be(1);

        JsonElement item = json[0];

        item.GetProperty("id")
            .GetGuid()
            .Should()
            .Be(expectedListingId);

        return item;
    }

    private static void AssertSelectedTranslation(
        JsonElement item,
        string languageCode,
        string title,
        string city,
        string municipality,
        string neighborhood)
    {
        item.GetProperty("languageCode")
            .GetString()
            .Should()
            .Be(languageCode);

        item.GetProperty("title")
            .GetString()
            .Should()
            .Be(title);

        item.GetProperty("city")
            .GetString()
            .Should()
            .Be(city);

        item.GetProperty("municipality")
            .GetString()
            .Should()
            .Be(municipality);

        item.GetProperty("neighborhood")
            .GetString()
            .Should()
            .Be(neighborhood);
    }
}
