using FluentAssertions;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Tests.Integration.Listings;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task GetAgencyListings_WithMissingAgency_ReturnsNotFound()
    {
        Guid missingAgencyId = Guid.NewGuid();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{missingAgencyId}/listings?lang=en&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string error = await response.Content.ReadAsStringAsync();

        error.Should().Contain("Agency was not found.");
    }

    [Fact]
    public async Task GetAgencyListings_WithExistingAgencyAndNoListings_ReturnsEmptyPagedResult()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(user);

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{agencyId}/listings?lang=en&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        json.GetProperty("items").GetArrayLength().Should().Be(0);
        json.GetProperty("page").GetInt32().Should().Be(1);
        json.GetProperty("pageSize").GetInt32().Should().Be(20);
        json.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetAgencyListings_ReturnsOnlyListingsForAgency()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid firstAgencyId = await CreateAgencyAsAsync(owner);
        Guid secondAgencyId = await CreateAgencyAsAsync(owner);

        Guid firstAgencyListingId = await CreateAgencyListingAsAsync(
            owner,
            firstAgencyId,
            price: 99000);

        Guid secondAgencyListingId = await CreateAgencyListingAsAsync(
            owner,
            secondAgencyId,
            price: 125000);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            firstAgencyListingId,
            ListingStatus.Active);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            secondAgencyListingId,
            ListingStatus.Active);

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{firstAgencyId}/listings?lang=en&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("totalCount").GetInt32().Should().Be(1);

        JsonElement items = json.GetProperty("items");

        items.GetArrayLength().Should().Be(1);

        Guid returnedListingId = items[0].GetProperty("id").GetGuid();

        returnedListingId.Should().Be(firstAgencyListingId);
        returnedListingId.Should().NotBe(secondAgencyListingId);

        items[0].GetProperty("agencyId").GetGuid().Should().Be(firstAgencyId);
    }

    [Theory]
    [InlineData("sort=1")]
    [InlineData("sort=oldest")]
    [InlineData("currency=EU")]
    [InlineData("currency=E1R")]
    [InlineData("sort=priceAsc")]
    [InlineData("sort=priceDesc")]
    public async Task GetAgencyListings_WithInvalidInputAndMissingAgency_ReturnsBadRequest(
    string queryString)
    {
        Guid missingAgencyId = Guid.NewGuid();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{missingAgencyId}/listings?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAgencyListings_DefaultNewestSort_IsDeterministic()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        DateTime olderTimestamp =
            new(2031, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        DateTime newerTimestamp =
            olderTimestamp.AddHours(1);

        Guid olderListingId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 100000m,
            currency: "AGN");

        Guid newerListingId1 = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 110000m,
            currency: "AGN");

        Guid newerListingId2 = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 120000m,
            currency: "AGN");

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            olderListingId,
            ListingStatus.Active,
            olderTimestamp);

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            newerListingId1,
            ListingStatus.Active,
            newerTimestamp);

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            newerListingId2,
            ListingStatus.Active,
            newerTimestamp);

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{agencyId}/listings" +
            "?lang=en" +
            "&currency=agn" +
            "&page=1" +
            "&pageSize=20");

        (IReadOnlyList<Guid> actualIds, int totalCount) =
            await ReadAgencyListingsPageAsync(response);

        IReadOnlyList<Guid> equalTimestampIds =
            new[] { newerListingId1, newerListingId2 }
                .OrderByDescending(
                    id => id.ToString("N"),
                    StringComparer.Ordinal)
                .ToList();

        IReadOnlyList<Guid> expectedIds = equalTimestampIds
            .Append(olderListingId)
            .ToList();

        actualIds.Should().Equal(expectedIds);
        totalCount.Should().Be(3);
    }

    [Theory]
    [InlineData("priceAsc", true)]
    [InlineData("priceDesc", false)]
    public async Task GetAgencyListings_WithPriceSort_InheritsSharedPublicOrdering(
    string sort,
    bool ascending)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        Guid lowestPriceId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 100000m,
            currency: "AGP");

        Guid middlePriceId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 200000m,
            currency: "AGP");

        Guid highestPriceId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 300000m,
            currency: "AGP");

        DateTime timestamp =
            new(2031, 2, 1, 10, 0, 0, DateTimeKind.Utc);

        foreach (Guid listingId in new[]
                 {
                 lowestPriceId,
                 middlePriceId,
                 highestPriceId
             })
        {
            await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
                _factory,
                listingId,
                ListingStatus.Active,
                timestamp);
        }

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{agencyId}/listings" +
            $"?sort={sort}" +
            "&currency=agp" +
            "&page=1" +
            "&pageSize=20");

        (IReadOnlyList<Guid> actualIds, int totalCount) =
            await ReadAgencyListingsPageAsync(response);

        IReadOnlyList<Guid> expectedIds = ascending
            ? new[] { lowestPriceId, middlePriceId, highestPriceId }
            : new[] { highestPriceId, middlePriceId, lowestPriceId };

        actualIds.Should().Equal(expectedIds);
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetAgencyListings_WithCurrency_ReturnsOnlyExactCurrencyMatches()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        Guid matchingListingId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 150000m,
            currency: "AGC");

        Guid excludedListingId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 150000m,
            currency: "AGD");

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            matchingListingId,
            ListingStatus.Active);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            excludedListingId,
            ListingStatus.Active);

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{agencyId}/listings" +
            "?currency=%20agc%20" +
            "&page=1" +
            "&pageSize=20");

        (IReadOnlyList<Guid> actualIds, int totalCount) =
            await ReadAgencyListingsPageAsync(response);

        actualIds.Should().Equal(new[] { matchingListingId });
        actualIds.Should().NotContain(excludedListingId);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAgencyListings_NonActiveStatuses_DoNotAffectItemsCountOrFirstPage()
    {
        const string currency = "APV";

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        Guid activeListingId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 100000m,
            currency: currency);

        Guid draftListingId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 110000m,
            currency: currency);

        Guid archivedListingId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 120000m,
            currency: currency);

        Guid reservedListingId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 130000m,
            currency: currency);

        Guid soldListingId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 140000m,
            currency: currency);

        Guid rentedListingId = await CreateAgencyListingAsAsync(
            owner,
            agencyId,
            price: 150000m,
            currency: currency);

        DateTime activeCreatedAtUtc =
            new(2032, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            activeListingId,
            ListingStatus.Active,
            activeCreatedAtUtc);

        var nonActiveListings = new[]
        {
        (Id: draftListingId, Status: ListingStatus.Draft),
        (Id: archivedListingId, Status: ListingStatus.Archived),
        (Id: reservedListingId, Status: ListingStatus.Reserved),
        (Id: soldListingId, Status: ListingStatus.Sold),
        (Id: rentedListingId, Status: ListingStatus.Rented)
    };

        for (int index = 0; index < nonActiveListings.Length; index++)
        {
            (Guid listingId, ListingStatus status) =
                nonActiveListings[index];

            await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
                _factory,
                listingId,
                status,
                activeCreatedAtUtc.AddMinutes(index + 1));
        }

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{agencyId}/listings" +
            $"?currency={currency}" +
            "&page=1" +
            "&pageSize=1");

        (IReadOnlyList<Guid> actualIds, int totalCount) =
            await ReadAgencyListingsPageAsync(response);

        actualIds.Should().Equal(new[] { activeListingId });
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAgencyListings_WhenRequestedLanguageIsMissing_UsesDeterministicFallback()
    {
        // Arrange
        const string currency = "AGT";

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId =
            await CreateAgencyAsAsync(owner);

        Guid listingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 100000m,
                currency: currency);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId,
            CreateCustomListingTranslation(
                "\U00010000",
                "Supplementary Title",
                city: "Supplementary City"),
            CreateCustomListingTranslation(
                "\uE000",
                "Private Use Title",
                city: "Private Use City"));

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/listings" +
                "?lang=de" +
                $"&currency={currency}" +
                "&page=1" +
                "&pageSize=20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("totalCount").GetInt32().Should().Be(1);

        JsonElement items = json.GetProperty("items");

        items.GetArrayLength().Should().Be(1);

        JsonElement item = items[0];

        item.GetProperty("id").GetGuid().Should().Be(listingId);
        item.GetProperty("agencyId").GetGuid().Should().Be(agencyId);
        item.GetProperty("languageCode").GetString()
            .Should().Be("\uE000");
        item.GetProperty("title").GetString()
            .Should().Be("Private Use Title");
        item.GetProperty("city").GetString()
            .Should().Be("Private Use City");
    }

    [Fact]
    public async Task GetListings_WithAgencyIdAndLocationFilter_UsesSharedEffectiveTranslationSemantics()
    {
        // Arrange
        const string currency = "AGL";
        const string city = "Agency Scoped City";

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid selectedAgencyId =
            await CreateAgencyAsAsync(owner);

        Guid excludedAgencyId =
            await CreateAgencyAsAsync(owner);

        Guid selectedListingId =
            await CreateAgencyListingAsAsync(
                owner,
                selectedAgencyId,
                price: 100000m,
                currency: currency);

        Guid excludedListingId =
            await CreateAgencyListingAsAsync(
                owner,
                excludedAgencyId,
                price: 110000m,
                currency: currency);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            selectedListingId,
            CreateCustomListingTranslation(
                "en",
                "Selected Agency Listing",
                city: city),
            CreateCustomListingTranslation(
                "mk",
                "Избран агенциски оглас",
                city: "Друг Град"));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            excludedListingId,
            CreateCustomListingTranslation(
                "en",
                "Excluded Agency Listing",
                city: city),
            CreateCustomListingTranslation(
                "mk",
                "Исклучен агенциски оглас",
                city: "Друг Град"));

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            selectedListingId,
            ListingStatus.Active);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            excludedListingId,
            ListingStatus.Active);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                "/api/listings" +
                $"?agencyId={selectedAgencyId}" +
                "&lang=en" +
                $"&currency={currency}" +
                $"&city={Uri.EscapeDataString(city)}" +
                "&page=1" +
                "&pageSize=20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("totalCount").GetInt32().Should().Be(1);

        JsonElement items = json.GetProperty("items");

        items.GetArrayLength().Should().Be(1);

        JsonElement item = items[0];

        item.GetProperty("id").GetGuid()
            .Should().Be(selectedListingId);

        item.GetProperty("id").GetGuid()
            .Should().NotBe(excludedListingId);

        item.GetProperty("agencyId").GetGuid()
            .Should().Be(selectedAgencyId);

        item.GetProperty("languageCode").GetString()
            .Should().Be("en");

        item.GetProperty("city").GetString()
            .Should().Be(city);
    }

    private static async Task<(
    IReadOnlyList<Guid> Ids,
    int TotalCount)> ReadAgencyListingsPageAsync(
    HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        IReadOnlyList<Guid> ids = json
            .GetProperty("items")
            .EnumerateArray()
            .Select(item =>
                item.GetProperty("id").GetGuid())
            .ToList();

        int totalCount =
            json.GetProperty("totalCount").GetInt32();

        return (ids, totalCount);
    }
}
