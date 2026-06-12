using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RealEstate.Tests.Integration;

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
        var request = CreateValidListingRequest();

        var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("id").GetGuid().Should().NotBeEmpty();
        json.GetProperty("languageCode").GetString().Should().Be("en");
        json.GetProperty("title").GetString().Should().Be("Integration test apartment");
    }

    [Fact]
    public async Task CreateListing_WithInvalidPrice_ReturnsBadRequest()
    {
        var request = CreateValidListingRequest(price: 0);

        var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadAsStringAsync();

        error.Should().Contain("Price must be greater than zero.");
    }

    [Fact]
    public async Task GetListings_ReturnsListings()
    {
        await CreateListingAsync();

        var response = await _httpClient.GetAsync("/api/listings?lang=en");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.ValueKind.Should().Be(JsonValueKind.Array);
        json.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetListingById_WithExistingListing_ReturnsListingInRequestedLanguage()
    {
        var listingId = await CreateListingAsync();

        var response = await _httpClient.GetAsync($"/api/listings/{listingId}?lang=mk");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("id").GetGuid().Should().Be(listingId);
        json.GetProperty("languageCode").GetString().Should().Be("mk");
        json.GetProperty("title").GetString().Should().Be("Интеграциски тест стан");
    }

    [Fact]
    public async Task GetListingById_WithMissingListing_ReturnsNotFound()
    {
        var missingListingId = Guid.NewGuid();

        var response = await _httpClient.GetAsync($"/api/listings/{missingListingId}?lang=en");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateListingAsync()
    {
        var request = CreateValidListingRequest();

        var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return json.GetProperty("id").GetGuid();
    }

    private static object CreateValidListingRequest(decimal price = 99000)
    {
        return new
        {
            listingType = "Sale",
            propertyType = "Apartment",
            price,
            currency = "EUR",
            areaSquareMeters = 58,
            rooms = 2,
            bathrooms = 1,
            floor = 2,
            totalFloors = 5,
            yearBuilt = 2015,
            latitude = 41.9981,
            longitude = 21.4254,
            translations = new[]
            {
                new
                {
                    languageCode = "en",
                    title = "Integration test apartment",
                    description = "Test listing created from integration tests.",
                    addressLine = "Center",
                    city = "Skopje",
                    neighborhood = "Center"
                },
                new
                {
                    languageCode = "mk",
                    title = "Интеграциски тест стан",
                    description = "Тест оглас креиран од integration tests.",
                    addressLine = "Центар",
                    city = "Скопје",
                    neighborhood = "Центар"
                }
            }
        };
    }
}