using FluentAssertions;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Tests.Integration.Listings;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task GetAgencyListings_PaginationContract_PreservesNormalizationZeroAndBeyondLastMetadata()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        HttpResponseMessage normalizedResponse =
            await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/listings?page=0&pageSize=0");

        JsonElement normalized =
            await ReadPaginationBodyAsync(normalizedResponse);

        AssertPaginationContract(
            normalized,
            expectedItemCount: 0,
            expectedPage: 1,
            expectedPageSize: 20,
            expectedTotalCount: 0,
            expectedTotalPages: 0,
            expectedHasNextPage: false,
            expectedHasPreviousPage: false);

        HttpResponseMessage cappedResponse =
            await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/listings?page=1&pageSize=101");

        JsonElement capped = await ReadPaginationBodyAsync(cappedResponse);

        AssertPaginationContract(
            capped,
            expectedItemCount: 0,
            expectedPage: 1,
            expectedPageSize: 100,
            expectedTotalCount: 0,
            expectedTotalPages: 0,
            expectedHasNextPage: false,
            expectedHasPreviousPage: false);

        HttpResponseMessage beyondLastResponse =
            await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/listings?page=2&pageSize=20");

        JsonElement beyondLast =
            await ReadPaginationBodyAsync(beyondLastResponse);

        AssertPaginationContract(
            beyondLast,
            expectedItemCount: 0,
            expectedPage: 2,
            expectedPageSize: 20,
            expectedTotalCount: 0,
            expectedTotalPages: 0,
            expectedHasNextPage: false,
            expectedHasPreviousPage: true);
    }

    [Fact]
    public async Task GetAgencyDashboardListings_PaginationContract_PreservesNormalizationCapAndEdgeMetadata()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        for (int index = 0; index < 3; index++)
        {
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 150000m + index);
        }

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage normalizedResponse =
                await _httpClient.GetAsync(
                    $"/api/agencies/{agencyId}/dashboard/listings" +
                    "?lang=en&page=0&pageSize=0");

            JsonElement normalized =
                await ReadPaginationBodyAsync(normalizedResponse);

            AssertPaginationContract(
                normalized,
                expectedItemCount: 3,
                expectedPage: 1,
                expectedPageSize: 20,
                expectedTotalCount: 3,
                expectedTotalPages: 1,
                expectedHasNextPage: false,
                expectedHasPreviousPage: false);

            HttpResponseMessage cappedResponse =
                await _httpClient.GetAsync(
                    $"/api/agencies/{agencyId}/dashboard/listings" +
                    "?lang=en&page=1&pageSize=101");

            JsonElement capped =
                await ReadPaginationBodyAsync(cappedResponse);

            AssertPaginationContract(
                capped,
                expectedItemCount: 3,
                expectedPage: 1,
                expectedPageSize: 100,
                expectedTotalCount: 3,
                expectedTotalPages: 1,
                expectedHasNextPage: false,
                expectedHasPreviousPage: false);

            HttpResponseMessage firstPageResponse =
                await _httpClient.GetAsync(
                    $"/api/agencies/{agencyId}/dashboard/listings" +
                    "?lang=en&page=1&pageSize=2");

            JsonElement firstPage =
                await ReadPaginationBodyAsync(firstPageResponse);

            AssertPaginationContract(
                firstPage,
                expectedItemCount: 2,
                expectedPage: 1,
                expectedPageSize: 2,
                expectedTotalCount: 3,
                expectedTotalPages: 2,
                expectedHasNextPage: true,
                expectedHasPreviousPage: false);

            HttpResponseMessage partialFinalResponse =
                await _httpClient.GetAsync(
                    $"/api/agencies/{agencyId}/dashboard/listings" +
                    "?lang=en&page=2&pageSize=2");

            JsonElement partialFinal =
                await ReadPaginationBodyAsync(partialFinalResponse);

            AssertPaginationContract(
                partialFinal,
                expectedItemCount: 1,
                expectedPage: 2,
                expectedPageSize: 2,
                expectedTotalCount: 3,
                expectedTotalPages: 2,
                expectedHasNextPage: false,
                expectedHasPreviousPage: true);

            HttpResponseMessage beyondLastResponse =
                await _httpClient.GetAsync(
                    $"/api/agencies/{agencyId}/dashboard/listings" +
                    "?lang=en&page=3&pageSize=2");

            JsonElement beyondLast =
                await ReadPaginationBodyAsync(beyondLastResponse);

            AssertPaginationContract(
                beyondLast,
                expectedItemCount: 0,
                expectedPage: 3,
                expectedPageSize: 2,
                expectedTotalCount: 3,
                expectedTotalPages: 2,
                expectedHasNextPage: false,
                expectedHasPreviousPage: true);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyDashboardListings_WithEqualCreatedAtUtc_IsStableAcrossRepeatedAndAdjacentPages()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);
        var listingIds = new List<Guid>();

        for (int index = 0; index < 5; index++)
        {
            listingIds.Add(
                await CreateAgencyListingAsAsync(
                    owner,
                    agencyId,
                    price: 160000m + index));
        }

        DateTime sharedTimestamp = new(
            2037,
            3,
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
            string route =
                $"/api/agencies/{agencyId}/dashboard/listings?lang=en";

            IReadOnlyList<Guid> firstPageIds =
                await ReadPaginationIdsAsync(
                    $"{route}&page=1&pageSize=2");

            IReadOnlyList<Guid> repeatedFirstPageIds =
                await ReadPaginationIdsAsync(
                    $"{route}&page=1&pageSize=2");

            IReadOnlyList<Guid> secondPageIds =
                await ReadPaginationIdsAsync(
                    $"{route}&page=2&pageSize=2");

            IReadOnlyList<Guid> thirdPageIds =
                await ReadPaginationIdsAsync(
                    $"{route}&page=3&pageSize=2");

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
