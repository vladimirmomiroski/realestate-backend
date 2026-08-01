using FluentAssertions;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task GetListings_PaginationContract_PreservesNormalizationCapAndEdgeMetadata()
    {
        const string currency = "KPA";

        (Guid firstListingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(
                _httpClient,
                currency: currency);

        Guid secondListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner,
                currency: currency);

        DateTime timestamp = new(
            2037,
            1,
            1,
            10,
            0,
            0,
            DateTimeKind.Utc);

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            firstListingId,
            ListingStatus.Active,
            timestamp);

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            secondListingId,
            ListingStatus.Active,
            timestamp.AddMinutes(1));

        HttpResponseMessage normalizedResponse =
            await _httpClient.GetAsync(
                $"/api/listings?currency={currency}&page=0&pageSize=0");

        JsonElement normalized =
            await ReadPaginationBodyAsync(normalizedResponse);

        AssertPaginationContract(
            normalized,
            expectedItemCount: 2,
            expectedPage: 1,
            expectedPageSize: 20,
            expectedTotalCount: 2,
            expectedTotalPages: 1,
            expectedHasNextPage: false,
            expectedHasPreviousPage: false);

        HttpResponseMessage cappedResponse =
            await _httpClient.GetAsync(
                $"/api/listings?currency={currency}&page=1&pageSize=101");

        JsonElement capped = await ReadPaginationBodyAsync(cappedResponse);

        AssertPaginationContract(
            capped,
            expectedItemCount: 2,
            expectedPage: 1,
            expectedPageSize: 100,
            expectedTotalCount: 2,
            expectedTotalPages: 1,
            expectedHasNextPage: false,
            expectedHasPreviousPage: false);

        HttpResponseMessage beyondLastResponse =
            await _httpClient.GetAsync(
                $"/api/listings?currency={currency}&page=2&pageSize=2");

        JsonElement beyondLast =
            await ReadPaginationBodyAsync(beyondLastResponse);

        AssertPaginationContract(
            beyondLast,
            expectedItemCount: 0,
            expectedPage: 2,
            expectedPageSize: 2,
            expectedTotalCount: 2,
            expectedTotalPages: 1,
            expectedHasNextPage: false,
            expectedHasPreviousPage: true);

        HttpResponseMessage zeroResponse =
            await _httpClient.GetAsync(
                "/api/listings?currency=KZP&page=1&pageSize=20");

        JsonElement zero = await ReadPaginationBodyAsync(zeroResponse);

        AssertPaginationContract(
            zero,
            expectedItemCount: 0,
            expectedPage: 1,
            expectedPageSize: 20,
            expectedTotalCount: 0,
            expectedTotalPages: 0,
            expectedHasNextPage: false,
            expectedHasPreviousPage: false);
    }

    [Fact]
    public async Task GetMyListings_PaginationContract_PreservesNormalizationAndBeyondLastMetadata()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(
                _httpClient);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage normalizedResponse =
                await _httpClient.GetAsync(
                    "/api/listings/my?lang=en&page=-1&pageSize=0");

            JsonElement normalized =
                await ReadPaginationBodyAsync(normalizedResponse);

            AssertPaginationContract(
                normalized,
                expectedItemCount: 1,
                expectedPage: 1,
                expectedPageSize: 20,
                expectedTotalCount: 1,
                expectedTotalPages: 1,
                expectedHasNextPage: false,
                expectedHasPreviousPage: false);

            normalized.GetProperty("items")[0]
                .GetProperty("id")
                .GetGuid()
                .Should().Be(listingId);

            HttpResponseMessage cappedResponse =
                await _httpClient.GetAsync(
                    "/api/listings/my?lang=en&page=1&pageSize=101");

            JsonElement capped =
                await ReadPaginationBodyAsync(cappedResponse);

            AssertPaginationContract(
                capped,
                expectedItemCount: 1,
                expectedPage: 1,
                expectedPageSize: 100,
                expectedTotalCount: 1,
                expectedTotalPages: 1,
                expectedHasNextPage: false,
                expectedHasPreviousPage: false);

            HttpResponseMessage beyondLastResponse =
                await _httpClient.GetAsync(
                    "/api/listings/my?lang=en&page=2&pageSize=1");

            JsonElement beyondLast =
                await ReadPaginationBodyAsync(beyondLastResponse);

            AssertPaginationContract(
                beyondLast,
                expectedItemCount: 0,
                expectedPage: 2,
                expectedPageSize: 1,
                expectedTotalCount: 1,
                expectedTotalPages: 1,
                expectedHasNextPage: false,
                expectedHasPreviousPage: true);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetMyListings_WithEqualCreatedAtUtc_IsStableAcrossRepeatedAndAdjacentPages()
    {
        (Guid firstListingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(
                _httpClient);

        var listingIds = new List<Guid> { firstListingId };

        for (int index = 0; index < 4; index++)
        {
            listingIds.Add(
                await ListingTestHelpers.CreateListingAsAsync(
                    _httpClient,
                    owner));
        }

        DateTime sharedTimestamp = new(
            2037,
            2,
            1,
            10,
            0,
            0,
            DateTimeKind.Utc);

        foreach (Guid listingId in listingIds)
        {
            await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
                _factory,
                listingId,
                ListingStatus.Draft,
                sharedTimestamp);
        }

        IReadOnlyList<Guid> expectedIds = listingIds
            .OrderByDescending(
                id => id.ToString("N"),
                StringComparer.Ordinal)
            .ToList();

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            IReadOnlyList<Guid> firstPageIds =
                await ReadPaginationIdsAsync(
                    "/api/listings/my?lang=en&page=1&pageSize=2");

            IReadOnlyList<Guid> repeatedFirstPageIds =
                await ReadPaginationIdsAsync(
                    "/api/listings/my?lang=en&page=1&pageSize=2");

            IReadOnlyList<Guid> secondPageIds =
                await ReadPaginationIdsAsync(
                    "/api/listings/my?lang=en&page=2&pageSize=2");

            IReadOnlyList<Guid> thirdPageIds =
                await ReadPaginationIdsAsync(
                    "/api/listings/my?lang=en&page=3&pageSize=2");

            firstPageIds.Should().Equal(expectedIds.Take(2));
            repeatedFirstPageIds.Should().Equal(firstPageIds);
            secondPageIds.Should().Equal(expectedIds.Skip(2).Take(2));
            thirdPageIds.Should().Equal(expectedIds.Skip(4).Take(2));

            firstPageIds
                .Concat(secondPageIds)
                .Concat(thirdPageIds)
                .Should()
                .OnlyHaveUniqueItems()
                .And.Equal(expectedIds);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<IReadOnlyList<Guid>> ReadPaginationIdsAsync(
        string requestUri)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(requestUri);
        JsonElement body = await ReadPaginationBodyAsync(response);

        return body.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();
    }

    private static async Task<JsonElement> ReadPaginationBodyAsync(
        HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static void AssertPaginationContract(
        JsonElement body,
        int expectedItemCount,
        int expectedPage,
        int expectedPageSize,
        int expectedTotalCount,
        int expectedTotalPages,
        bool expectedHasNextPage,
        bool expectedHasPreviousPage)
    {
        body.EnumerateObject()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                "items",
                "page",
                "pageSize",
                "totalCount",
                "totalPages",
                "hasNextPage",
                "hasPreviousPage");

        body.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        body.GetProperty("items").GetArrayLength().Should().Be(expectedItemCount);
        body.GetProperty("page").GetInt32().Should().Be(expectedPage);
        body.GetProperty("pageSize").GetInt32().Should().Be(expectedPageSize);
        body.GetProperty("totalCount").GetInt32().Should().Be(expectedTotalCount);
        body.GetProperty("totalPages").GetInt32().Should().Be(expectedTotalPages);
        body.GetProperty("hasNextPage").GetBoolean().Should().Be(expectedHasNextPage);
        body.GetProperty("hasPreviousPage").GetBoolean()
            .Should().Be(expectedHasPreviousPage);
    }
}
