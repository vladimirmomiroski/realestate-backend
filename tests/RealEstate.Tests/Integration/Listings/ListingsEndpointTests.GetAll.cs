using FluentAssertions;
using RealEstate.Domain.Enums;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task GetListings_ReturnsPagedListings()
    {
        const decimal uniquePrice = 987654.32m;

        Guid listingId = await ListingTestHelpers.CreateListingAsync(
            _httpClient,
            uniquePrice);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);

        string price = uniquePrice.ToString(CultureInfo.InvariantCulture);

        var response = await _httpClient.GetAsync(
            $"/api/listings" +
            $"?lang=en" +
            $"&minPrice={price}" +
            $"&maxPrice={price}" +
            $"&currency=EUR" +
            $"&page=1" +
            $"&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        JsonElement items = json.GetProperty("items");

        items.ValueKind.Should().Be(JsonValueKind.Array);
        items.GetArrayLength().Should().Be(1);
        JsonElement item = items[0];
        item.GetProperty("id").GetGuid().Should().Be(listingId);
        item.GetProperty("languageCode").GetString().Should().Be("en");
        item.GetProperty("title").GetString().Should()
            .Be("Integration test apartment");
        item.GetProperty("city").GetString().Should().Be("Skopje");
        item.GetProperty("municipality").GetString().Should().Be("Centar");
        item.GetProperty("addressLine").GetString().Should().Be("Center");
        item.GetProperty("description").GetString().Should()
            .Be("Test listing created from integration tests.");
        item.GetProperty("latitude").GetDecimal().Should().Be(41.9981m);
        item.GetProperty("longitude").GetDecimal().Should().Be(21.4254m);
        item.GetProperty("locationPrecision").GetString().Should()
            .Be("ExactAddress");

        json.GetProperty("page").GetInt32().Should().Be(1);
        json.GetProperty("pageSize").GetInt32().Should().Be(20);
        json.GetProperty("totalCount").GetInt32().Should().Be(1);
    }
}
