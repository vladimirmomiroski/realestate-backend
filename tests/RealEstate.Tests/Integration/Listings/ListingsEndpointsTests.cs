using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace RealEstate.Tests.Integration.Listings;

public sealed class ListingsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _httpClient;

    public ListingsEndpointTests(CustomWebApplicationFactory factory)
    {
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task CreateListing_WithValidRequest_ReturnsCreated()
    {
        var request = ListingTestHelpers.CreateValidListingRequest();

        var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("id").GetGuid().Should().NotBeEmpty();
        json.GetProperty("languageCode").GetString().Should().Be("en");
        json.GetProperty("title").GetString().Should().Be("Integration test apartment");
        json.GetProperty("balconyCount").GetInt32().Should().Be(2);
        json.GetProperty("parkingSpaces").GetInt32().Should().Be(1);
        json.GetProperty("hasBasement").GetBoolean().Should().BeTrue();
        json.GetProperty("isExchangePossible").GetBoolean().Should().BeFalse();
        json.GetProperty("heatingType").GetString().Should().Be("Central");
        json.GetProperty("furnishingStatus").GetString().Should().Be("Furnished");
        json.GetProperty("condition").GetString().Should().Be("Good");
        json.GetProperty("yearRenovated").GetInt32().Should().Be(2022);
        json.GetProperty("orientation").GetString().Should().Be("SouthEast");
        json.GetProperty("municipality").GetString().Should().Be("Centar");
        json.GetProperty("primaryImageUrl").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("images").ValueKind.Should().Be(JsonValueKind.Array);
        json.GetProperty("images").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task CreateListing_WithInvalidPrice_ReturnsBadRequest()
    {
        var request = ListingTestHelpers.CreateValidListingRequest(price: 0);

        var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadAsStringAsync();

        error.Should().Contain("Price must be greater than zero.");
    }

    [Fact]
    public async Task GetListings_ReturnsPagedListings()
    {
        await ListingTestHelpers.CreateListingAsync(_httpClient);

        var response = await _httpClient.GetAsync("/api/listings?lang=en&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        json.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);

        json.GetProperty("page").GetInt32().Should().Be(1);
        json.GetProperty("pageSize").GetInt32().Should().Be(20);
        json.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
    }

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
    public async Task GetListingById_WithExistingListing_ReturnsListingInRequestedLanguage()
    {
        var listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

        var response = await _httpClient.GetAsync($"/api/listings/{listingId}?lang=mk");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("id").GetGuid().Should().Be(listingId);
        json.GetProperty("languageCode").GetString().Should().Be("mk");
        json.GetProperty("title").GetString().Should().Be("Интеграциски тест стан");
        json.GetProperty("municipality").GetString().Should().Be("Центар");
        json.GetProperty("primaryImageUrl").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("images").ValueKind.Should().Be(JsonValueKind.Array);
        json.GetProperty("images").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetListingById_WithMissingListing_ReturnsNotFound()
    {
        var missingListingId = Guid.NewGuid();

        var response = await _httpClient.GetAsync($"/api/listings/{missingListingId}?lang=en");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}