using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
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
}
