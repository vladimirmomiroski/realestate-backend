using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RealEstate.Tests.Integration.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Listings;

public sealed class ListingsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public ListingsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task CreateListing_WithValidRequest_ReturnsCreated()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        try
        {
            _httpClient.AuthorizeAs(user.AccessToken);

            var request = ListingTestHelpers.CreateValidListingRequest();

            var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().NotBeEmpty();
            json.GetProperty("languageCode").GetString().Should().Be("en");
            json.GetProperty("title").GetString().Should().Be("Integration test apartment");

            json.GetProperty("pricePerSquareMeter").GetDecimal().Should().Be(1706.90m);
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

            var apartmentDetails = json.GetProperty("apartmentDetails");

            apartmentDetails.GetProperty("apartmentType").GetString().Should().Be("Standard");
            apartmentDetails.GetProperty("floor").GetInt32().Should().Be(4);
            apartmentDetails.GetProperty("totalFloors").GetInt32().Should().Be(8);
            apartmentDetails.GetProperty("hasElevator").GetBoolean().Should().BeTrue();

            json.GetProperty("houseDetails").ValueKind.Should().Be(JsonValueKind.Null);

            json.GetProperty("primaryImageUrl").ValueKind.Should().Be(JsonValueKind.Null);
            json.GetProperty("images").ValueKind.Should().Be(JsonValueKind.Array);
            json.GetProperty("images").GetArrayLength().Should().Be(0);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        var request = ListingTestHelpers.CreateValidListingRequest();

        var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateListing_WithAccessToken_StoresCreatedByUserId()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = ListingTestHelpers.CreateValidListingRequest();

            var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            Guid listingId = json.GetProperty("id").GetGuid();

            using IServiceScope scope = _factory.Services.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var listing = await dbContext.Listings.SingleAsync(
                listing => listing.Id == listingId);

            listing.CreatedByUserId.Should().Be(user.UserId);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }


    [Fact]
    public async Task CreateListing_WithDecimalPricePerSquareMeter_ReturnsRoundedValue()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        try
        {
            _httpClient.AuthorizeAs(user.AccessToken);

            var request = ListingTestHelpers.CreateValidListingRequest(price: 125000);

            var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("pricePerSquareMeter").GetDecimal().Should().Be(2155.17m);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }


    [Fact]
    public async Task CreateListing_WithValidHouseRequest_ReturnsCreated()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        try
        {
            _httpClient.AuthorizeAs(user.AccessToken);

            var request = ListingTestHelpers.CreateValidHouseListingRequest();

            var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("propertyType").GetString().Should().Be("House");
            json.GetProperty("apartmentDetails").ValueKind.Should().Be(JsonValueKind.Null);

            var houseDetails = json.GetProperty("houseDetails");

            houseDetails.GetProperty("houseType").GetString().Should().Be("Detached");
            houseDetails.GetProperty("numberOfFloors").GetInt32().Should().Be(2);
            houseDetails.GetProperty("yardAreaSquareMeters").GetDecimal().Should().Be(350);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_WithInvalidPrice_ReturnsBadRequest()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        try
        {
            _httpClient.AuthorizeAs(user.AccessToken);

            var request = ListingTestHelpers.CreateValidListingRequest(price: 0);

            var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var error = await response.Content.ReadAsStringAsync();

            error.Should().Contain("Price must be greater than zero.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
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

        json.GetProperty("apartmentDetails").ValueKind.Should().Be(JsonValueKind.Object);
        json.GetProperty("houseDetails").ValueKind.Should().Be(JsonValueKind.Null);

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

    [Fact]
    public async Task GetMyListings_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.GetAsync("/api/listings/my");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyListings_WithAccessToken_ReturnsOnlyCurrentUsersListings()
    {
        AuthenticatedTestUser firstUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser secondUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid firstUserListingId;
        Guid secondUserListingId;

        try
        {
            _httpClient.AuthorizeAs(firstUser.AccessToken);

            var firstRequest = ListingTestHelpers.CreateValidListingRequest();

            HttpResponseMessage firstCreateResponse =
                await _httpClient.PostAsJsonAsync("/api/listings", firstRequest);

            firstCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement firstCreateJson =
                await firstCreateResponse.Content.ReadFromJsonAsync<JsonElement>();

            firstUserListingId = firstCreateJson.GetProperty("id").GetGuid();

            _httpClient.AuthorizeAs(secondUser.AccessToken);

            var secondRequest = ListingTestHelpers.CreateValidListingRequest();

            HttpResponseMessage secondCreateResponse =
                await _httpClient.PostAsJsonAsync("/api/listings", secondRequest);

            secondCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement secondCreateJson =
                await secondCreateResponse.Content.ReadFromJsonAsync<JsonElement>();

            secondUserListingId = secondCreateJson.GetProperty("id").GetGuid();

            _httpClient.AuthorizeAs(firstUser.AccessToken);

            HttpResponseMessage response =
                await _httpClient.GetAsync("/api/listings/my?lang=mk&page=1&pageSize=20");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            List<Guid> listingIds = json
                .GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid())
                .ToList();

            listingIds.Should().Contain(firstUserListingId);
            listingIds.Should().NotContain(secondUserListingId);

            json.GetProperty("totalCount").GetInt32().Should().Be(1);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

}