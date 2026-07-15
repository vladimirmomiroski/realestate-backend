using FluentAssertions;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RealEstate.Domain.Enums;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task GetListings_WithPriceFilter_ReturnsOnlyMatchingCurrency()
    {
        const decimal matchingPrice = 934567.89m;
        const string matchingCurrency = "PFX";
        const string otherCurrency = "PFY";

        Guid matchingListingId =
            await ListingTestHelpers.CreateListingAsync(
                _httpClient,
                matchingPrice,
                matchingCurrency);

        Guid otherCurrencyListingId =
            await ListingTestHelpers.CreateListingAsync(
                _httpClient,
                matchingPrice,
                otherCurrency);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            matchingListingId,
            ListingStatus.Active);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            otherCurrencyListingId,
            ListingStatus.Active);

        var response = await _httpClient.GetAsync(
            "/api/listings" +
            "?lang=en" +
            $"&minPrice={matchingPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&maxPrice={matchingPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            "&currency=pfx" +
            "&page=1" +
            "&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        JsonElement items = json.GetProperty("items");

        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("id").GetGuid()
            .Should().Be(matchingListingId);

        items[0].GetProperty("currency").GetString()
            .Should().Be(matchingCurrency);

        items[0].GetProperty("price").GetDecimal()
            .Should().Be(matchingPrice);

        json.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetListings_WithMunicipalityFilter_ReturnsMatchingListings()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);

        var response = await _httpClient.GetAsync(
            "/api/listings?lang=en&municipality=Centar&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);

        var firstListing = json.GetProperty("items")[0];

        firstListing.GetProperty("municipality").GetString().Should().Be("Centar");
    }

    [Fact]
    public async Task GetListings_WithApartmentFilters_ReturnsMatchingListings()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);

        var response = await _httpClient.GetAsync(
            "/api/listings?lang=en&heatingType=Central&furnishingStatus=Furnished&condition=Good&hasBasement=true&hasElevator=true&apartmentType=Standard&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);

        var firstListing = json.GetProperty("items")[0];

        firstListing.GetProperty("heatingType").GetString().Should().Be("Central");
        firstListing.GetProperty("furnishingStatus").GetString().Should().Be("Furnished");
        firstListing.GetProperty("condition").GetString().Should().Be("Good");
        firstListing.GetProperty("hasBasement").GetBoolean().Should().BeTrue();

        var apartmentDetails = firstListing.GetProperty("apartmentDetails");

        apartmentDetails.GetProperty("apartmentType").GetString().Should().Be("Standard");
        apartmentDetails.GetProperty("hasElevator").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetListings_WithHouseFilters_ReturnsMatchingListings()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        try
        {
            _httpClient.AuthorizeAs(user.AccessToken);

            var request = ListingTestHelpers.CreateValidHouseListingRequest();

            var createResponse = await _httpClient.PostAsJsonAsync("/api/listings", request);

            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement createJson =
            await createResponse.Content.ReadFromJsonAsync<JsonElement>();

            Guid listingId = createJson.GetProperty("id").GetGuid();

            await ListingTestHelpers.SetListingStatusAsync(
                _factory,
                listingId,
                ListingStatus.Active);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        var response = await _httpClient.GetAsync(
            "/api/listings?lang=en&houseType=Detached&minYardAreaSquareMeters=300&maxYardAreaSquareMeters=400&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);

        var firstListing = json.GetProperty("items")[0];

        firstListing.GetProperty("propertyType").GetString().Should().Be("House");

        var houseDetails = firstListing.GetProperty("houseDetails");

        houseDetails.GetProperty("houseType").GetString().Should().Be("Detached");
        houseDetails.GetProperty("yardAreaSquareMeters").GetDecimal().Should().Be(350);
    }
}
