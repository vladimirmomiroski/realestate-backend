using FluentAssertions;
using RealEstate.Tests.Integration.Auth;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
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
                "en",
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
                "en",
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
            languageCode: "en",
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

        const string lowerCollatedLanguage = "de";

        const string higherCollatedLanguage = "sq";

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
}
