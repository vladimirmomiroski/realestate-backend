using FluentAssertions;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task GetListings_WithPriceFilter_ReturnsMatchingListings()
    {
        await ListingTestHelpers.CreateListingAsync(_httpClient);

        var response = await _httpClient.GetAsync(
            "/api/listings?lang=en&minPrice=90000&maxPrice=100000&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);

        var firstListing = json.GetProperty("items")[0];

        firstListing.GetProperty("primaryImageUrl").ValueKind.Should().Be(JsonValueKind.Null);
        firstListing.GetProperty("images").ValueKind.Should().Be(JsonValueKind.Array);
        firstListing.GetProperty("images").GetArrayLength().Should().Be(0);
        firstListing.GetProperty("price").GetDecimal().Should().BeInRange(90000, 100000);
    }

    [Fact]
    public async Task GetListings_WithMunicipalityFilter_ReturnsMatchingListings()
    {
        await ListingTestHelpers.CreateListingAsync(_httpClient);

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
        await ListingTestHelpers.CreateListingAsync(_httpClient);

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
