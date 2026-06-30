using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
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
}
