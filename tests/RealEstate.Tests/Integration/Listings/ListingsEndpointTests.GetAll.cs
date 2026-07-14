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
        items[0].GetProperty("id").GetGuid().Should().Be(listingId);

        json.GetProperty("page").GetInt32().Should().Be(1);
        json.GetProperty("pageSize").GetInt32().Should().Be(20);
        json.GetProperty("totalCount").GetInt32().Should().Be(1);
    }
}