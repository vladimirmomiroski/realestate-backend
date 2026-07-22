using FluentAssertions;
using RealEstate.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task GetListings_WithAreaRange_IncludesExactBounds()
    {
        const string currency = "QAA";

        await CreateActiveAreaRoomListingAsync(
            price: 90000m,
            currency,
            areaSquareMeters: 49m,
            rooms: 2m);

        Guid lowerBoundId =
            await CreateActiveAreaRoomListingAsync(
                price: 100000m,
                currency,
                areaSquareMeters: 50m,
                rooms: 2m);

        Guid middleId =
            await CreateActiveAreaRoomListingAsync(
                price: 110000m,
                currency,
                areaSquareMeters: 75m,
                rooms: 2m);

        Guid upperBoundId =
            await CreateActiveAreaRoomListingAsync(
                price: 120000m,
                currency,
                areaSquareMeters: 100m,
                rooms: 2m);

        await CreateActiveAreaRoomListingAsync(
            price: 130000m,
            currency,
            areaSquareMeters: 101m,
            rooms: 2m);

        HttpResponseMessage response = await _httpClient.GetAsync(
            "/api/listings" +
            "?currency=QAA" +
            "&sort=priceAsc" +
            "&minAreaSquareMeters=50" +
            "&maxAreaSquareMeters=100" +
            "&page=1" +
            "&pageSize=20");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadAreaRoomPageAsync(response);

        ids.Should().Equal(
            new[]
            {
                lowerBoundId,
                middleId,
                upperBoundId
            });

        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetListings_WithRoomRange_IncludesBoundsAndExcludesNullRooms()
    {
        const string currency = "QAR";

        await CreateActiveAreaRoomListingAsync(
            price: 90000m,
            currency,
            areaSquareMeters: 70m,
            rooms: null);

        await CreateActiveAreaRoomListingAsync(
            price: 95000m,
            currency,
            areaSquareMeters: 70m,
            rooms: 1.5m);

        Guid lowerBoundId =
            await CreateActiveAreaRoomListingAsync(
                price: 100000m,
                currency,
                areaSquareMeters: 70m,
                rooms: 2m);

        Guid middleId =
            await CreateActiveAreaRoomListingAsync(
                price: 110000m,
                currency,
                areaSquareMeters: 70m,
                rooms: 2.5m);

        Guid upperBoundId =
            await CreateActiveAreaRoomListingAsync(
                price: 120000m,
                currency,
                areaSquareMeters: 70m,
                rooms: 3m);

        await CreateActiveAreaRoomListingAsync(
            price: 130000m,
            currency,
            areaSquareMeters: 70m,
            rooms: 3.5m);

        HttpResponseMessage response = await _httpClient.GetAsync(
            "/api/listings" +
            "?currency=QAR" +
            "&sort=priceAsc" +
            "&minRooms=2" +
            "&maxRooms=3" +
            "&page=1" +
            "&pageSize=20");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadAreaRoomPageAsync(response);

        ids.Should().Equal(
            new[]
            {
                lowerBoundId,
                middleId,
                upperBoundId
            });

        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetListings_WithZeroRoomRange_ReturnsZeroButNotNullRooms()
    {
        const string currency = "QRZ";

        await CreateActiveAreaRoomListingAsync(
            price: 90000m,
            currency,
            areaSquareMeters: 60m,
            rooms: null);

        Guid zeroRoomsId =
            await CreateActiveAreaRoomListingAsync(
                price: 100000m,
                currency,
                areaSquareMeters: 60m,
                rooms: 0m);

        await CreateActiveAreaRoomListingAsync(
            price: 110000m,
            currency,
            areaSquareMeters: 60m,
            rooms: 1m);

        HttpResponseMessage response = await _httpClient.GetAsync(
            "/api/listings" +
            "?currency=QRZ" +
            "&minRooms=0" +
            "&maxRooms=0" +
            "&page=1" +
            "&pageSize=20");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadAreaRoomPageAsync(response);

        ids.Should().Equal(new[] { zeroRoomsId });
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_WithAreaAndRoomFilters_AppliesBeforeCountAndPagination()
    {
        const string currency = "QCM";

        Guid firstMatchingId =
            await CreateActiveAreaRoomListingAsync(
                price: 100000m,
                currency,
                areaSquareMeters: 60m,
                rooms: 2m);

        Guid secondMatchingId =
            await CreateActiveAreaRoomListingAsync(
                price: 200000m,
                currency,
                areaSquareMeters: 80m,
                rooms: 3m);

        await CreateActiveAreaRoomListingAsync(
            price: 150000m,
            currency,
            areaSquareMeters: 59m,
            rooms: 2m);

        await CreateActiveAreaRoomListingAsync(
            price: 160000m,
            currency,
            areaSquareMeters: 70m,
            rooms: 4m);

        HttpResponseMessage response = await _httpClient.GetAsync(
            "/api/listings" +
            "?currency=QCM" +
            "&sort=priceAsc" +
            "&minAreaSquareMeters=60" +
            "&maxAreaSquareMeters=80" +
            "&minRooms=2" +
            "&maxRooms=3" +
            "&page=2" +
            "&pageSize=1");

        (IReadOnlyList<Guid> ids, int totalCount) =
            await ReadAreaRoomPageAsync(response);

        ids.Should().Equal(new[] { secondMatchingId });
        ids.Should().NotContain(firstMatchingId);
        totalCount.Should().Be(2);
    }

    private async Task<Guid> CreateActiveAreaRoomListingAsync(
        decimal price,
        string currency,
        decimal areaSquareMeters,
        decimal? rooms)
    {
        Guid listingId =
            await ListingTestHelpers.CreateListingAsync(
                _httpClient,
                price,
                currency,
                areaSquareMeters,
                rooms);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);

        return listingId;
    }

    private static async Task<(
        IReadOnlyList<Guid> Ids,
        int TotalCount)> ReadAreaRoomPageAsync(
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
