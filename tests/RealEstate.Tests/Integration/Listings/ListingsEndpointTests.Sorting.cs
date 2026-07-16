using FluentAssertions;
using RealEstate.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task GetListings_DefaultNewestSort_OrdersByCreatedAtUtcThenIdDescending()
    {
        const string currency = "NWA";

        DateTime olderTimestamp =
            new(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        DateTime newerTimestamp =
            olderTimestamp.AddHours(1);

        Guid olderListingId =
            await CreateActiveSearchListingAsync(
                price: 100000,
                currency,
                olderTimestamp);

        Guid newerListingId1 =
            await CreateActiveSearchListingAsync(
                price: 110000,
                currency,
                newerTimestamp);

        Guid newerListingId2 =
            await CreateActiveSearchListingAsync(
                price: 120000,
                currency,
                newerTimestamp);

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings?currency={currency}&page=1&pageSize=20");

        (IReadOnlyList<Guid> actualIds, int totalCount) =
            await ReadSearchPageAsync(response);

        IReadOnlyList<Guid> expectedIds =
            new[] { newerListingId1, newerListingId2 }
                .OrderByDescending(
                    id => id.ToString("N"),
                    StringComparer.Ordinal)
                .Append(olderListingId)
                .ToList();

        actualIds.Should().Equal(expectedIds);
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetListings_PriceAsc_UsesAllLockedTieBreakers()
    {
        const string currency = "PAA";

        DateTime olderTimestamp =
            new(2030, 2, 1, 10, 0, 0, DateTimeKind.Utc);

        DateTime newerTimestamp =
            olderTimestamp.AddHours(1);

        Guid lowerOlderId =
            await CreateActiveSearchListingAsync(
                price: 100000,
                currency,
                olderTimestamp);

        Guid lowerNewerId1 =
            await CreateActiveSearchListingAsync(
                price: 100000,
                currency,
                newerTimestamp);

        Guid lowerNewerId2 =
            await CreateActiveSearchListingAsync(
                price: 100000,
                currency,
                newerTimestamp);

        Guid higherPriceId =
            await CreateActiveSearchListingAsync(
                price: 200000,
                currency,
                newerTimestamp.AddHours(1));

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings" +
            $"?sort=priceAsc" +
            $"&currency={currency}" +
            $"&page=1" +
            $"&pageSize=20");

        (IReadOnlyList<Guid> actualIds, int totalCount) =
            await ReadSearchPageAsync(response);

        List<Guid> expectedIds =
            new[] { lowerNewerId1, lowerNewerId2 }
                .OrderByDescending(
                    id => id.ToString("N"),
                    StringComparer.Ordinal)
                .Append(lowerOlderId)
                .Append(higherPriceId)
                .ToList();

        actualIds.Should().Equal(expectedIds);
        totalCount.Should().Be(4);
    }

    [Fact]
    public async Task GetListings_PriceDesc_UsesAllLockedTieBreakers()
    {
        const string currency = "PDA";

        DateTime olderTimestamp =
            new(2030, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        DateTime newerTimestamp =
            olderTimestamp.AddHours(1);

        Guid higherOlderId =
            await CreateActiveSearchListingAsync(
                price: 300000,
                currency,
                olderTimestamp);

        Guid higherNewerId1 =
            await CreateActiveSearchListingAsync(
                price: 300000,
                currency,
                newerTimestamp);

        Guid higherNewerId2 =
            await CreateActiveSearchListingAsync(
                price: 300000,
                currency,
                newerTimestamp);

        Guid lowerPriceId =
            await CreateActiveSearchListingAsync(
                price: 100000,
                currency,
                newerTimestamp.AddHours(1));

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings" +
            $"?sort=priceDesc" +
            $"&currency={currency}" +
            $"&page=1" +
            $"&pageSize=20");

        (IReadOnlyList<Guid> actualIds, int totalCount) =
            await ReadSearchPageAsync(response);

        List<Guid> expectedIds =
            new[] { higherNewerId1, higherNewerId2 }
                .OrderByDescending(
                    id => id.ToString("N"),
                    StringComparer.Ordinal)
                .Append(higherOlderId)
                .Append(lowerPriceId)
                .ToList();

        actualIds.Should().Equal(expectedIds);
        totalCount.Should().Be(4);
    }

    [Fact]
    public async Task GetListings_WithEqualSortValues_ReturnsStableAdjacentPages()
    {
        const string currency = "STB";
        const decimal price = 155555;

        DateTime timestamp =
            new(2030, 4, 1, 10, 0, 0, DateTimeKind.Utc);

        var listingIds = new List<Guid>();

        for (int index = 0; index < 5; index++)
        {
            Guid listingId =
                await CreateActiveSearchListingAsync(
                    price,
                    currency,
                    timestamp);

            listingIds.Add(listingId);
        }

        IReadOnlyList<Guid> expectedIds = listingIds
            .OrderByDescending(
                id => id.ToString("N"),
                StringComparer.Ordinal)
            .ToList();

        HttpResponseMessage firstRequest =
            await _httpClient.GetAsync(
                $"/api/listings" +
                $"?sort=priceAsc" +
                $"&currency={currency}" +
                $"&page=1" +
                $"&pageSize=2");

        HttpResponseMessage repeatedFirstRequest =
            await _httpClient.GetAsync(
                $"/api/listings" +
                $"?sort=priceAsc" +
                $"&currency={currency}" +
                $"&page=1" +
                $"&pageSize=2");

        HttpResponseMessage secondPageResponse =
            await _httpClient.GetAsync(
                $"/api/listings" +
                $"?sort=priceAsc" +
                $"&currency={currency}" +
                $"&page=2" +
                $"&pageSize=2");

        HttpResponseMessage thirdPageResponse =
            await _httpClient.GetAsync(
                $"/api/listings" +
                $"?sort=priceAsc" +
                $"&currency={currency}" +
                $"&page=3" +
                $"&pageSize=2");

        (IReadOnlyList<Guid> firstPageIds, int firstTotalCount) =
            await ReadSearchPageAsync(firstRequest);

        (IReadOnlyList<Guid> repeatedFirstPageIds, int repeatedTotalCount) =
            await ReadSearchPageAsync(repeatedFirstRequest);

        (IReadOnlyList<Guid> secondPageIds, int secondTotalCount) =
            await ReadSearchPageAsync(secondPageResponse);

        (IReadOnlyList<Guid> thirdPageIds, int thirdTotalCount) =
            await ReadSearchPageAsync(thirdPageResponse);

        firstPageIds.Should().Equal(expectedIds.Take(2));
        repeatedFirstPageIds.Should().Equal(firstPageIds);
        secondPageIds.Should().Equal(expectedIds.Skip(2).Take(2));
        thirdPageIds.Should().Equal(expectedIds.Skip(4).Take(2));

        firstTotalCount.Should().Be(5);
        repeatedTotalCount.Should().Be(5);
        secondTotalCount.Should().Be(5);
        thirdTotalCount.Should().Be(5);

        firstPageIds
            .Concat(secondPageIds)
            .Concat(thirdPageIds)
            .Should()
            .OnlyHaveUniqueItems()
            .And.Equal(expectedIds);
    }

    [Fact]
    public async Task GetListings_WithCurrencyOnly_ReturnsExactCurrencyMatches()
    {
        const string selectedCurrency = "CFA";
        const string excludedCurrency = "CFB";

        Guid selectedListingId =
            await CreateActiveSearchListingAsync(
                price: 123456,
                selectedCurrency,
                new DateTime(
                    2030,
                    5,
                    1,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc));

        await CreateActiveSearchListingAsync(
            price: 123456,
            excludedCurrency,
            new DateTime(
                2030,
                5,
                1,
                11,
                0,
                0,
                DateTimeKind.Utc));

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings?currency={selectedCurrency}");

        (IReadOnlyList<Guid> actualIds, int totalCount) =
            await ReadSearchPageAsync(response);

        actualIds.Should().Equal(selectedListingId);
        totalCount.Should().Be(1);
    }

    private async Task<Guid> CreateActiveSearchListingAsync(
        decimal price,
        string currency,
        DateTime createdAtUtc)
    {
        Guid listingId =
            await ListingTestHelpers.CreateListingAsync(
                _httpClient,
                price,
                currency);

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            listingId,
            ListingStatus.Active,
            createdAtUtc);

        return listingId;
    }

    private static async Task<(
        IReadOnlyList<Guid> Ids,
        int TotalCount)> ReadSearchPageAsync(
        HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        IReadOnlyList<Guid> ids = json
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();

        int totalCount =
            json.GetProperty("totalCount").GetInt32();

        return (ids, totalCount);
    }
}
