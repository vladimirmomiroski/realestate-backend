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
    public async Task GetListings_NonActiveStatuses_DoNotAffectItemsCountOrFirstPage()
    {
        const string currency = "VSB";

        (Guid activeListingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(
                _httpClient,
                currency: currency);

        Guid draftListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner,
                currency: currency);

        Guid archivedListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner,
                currency: currency);

        Guid reservedListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner,
                currency: currency);

        Guid soldListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner,
                currency: currency);

        Guid rentedListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner,
                currency: currency);

        DateTime activeCreatedAtUtc =
            new(2032, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            activeListingId,
            ListingStatus.Active,
            activeCreatedAtUtc);

        var nonActiveListings = new[]
        {
        (Id: draftListingId, Status: ListingStatus.Draft),
        (Id: archivedListingId, Status: ListingStatus.Archived),
        (Id: reservedListingId, Status: ListingStatus.Reserved),
        (Id: soldListingId, Status: ListingStatus.Sold),
        (Id: rentedListingId, Status: ListingStatus.Rented)
    };

        for (int index = 0; index < nonActiveListings.Length; index++)
        {
            (Guid listingId, ListingStatus status) =
                nonActiveListings[index];

            await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
                _factory,
                listingId,
                status,
                activeCreatedAtUtc.AddMinutes(index + 1));
        }

        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.GetAsync(
            "/api/listings" +
            $"?currency={currency}" +
            "&page=1" +
            "&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        JsonElement items = json.GetProperty("items");

        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("id").GetGuid()
            .Should().Be(activeListingId);

        ReadListingStatusFromJson(items[0])
            .Should().Be(ListingStatus.Active);

        json.GetProperty("totalCount").GetInt32().Should().Be(1);
        json.GetProperty("page").GetInt32().Should().Be(1);
        json.GetProperty("pageSize").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetListingById_ShouldReturnOk_WhenListingIsActive()
    {
        // Arrange
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetListingStatusAsync(listingId, ListingStatus.Active);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync($"/api/listings/{listingId}?lang=en");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        ListingStatus responseStatus = await ReadListingStatusAsync(response);
        responseStatus.Should().Be(ListingStatus.Active);
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Archived)]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Rented)]
    public async Task GetListingById_ShouldReturnNotFound_WhenListingIsNotActive(
        ListingStatus listingStatus)
    {
        // Arrange
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetListingStatusAsync(listingId, listingStatus);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync($"/api/listings/{listingId}?lang=en");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyListings_ShouldReturnAllStatusesForCurrentUser()
    {
        (Guid activeListingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(
                _httpClient);

        Guid draftListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner);

        Guid archivedListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner);

        Guid reservedListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner);

        Guid soldListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner);

        Guid rentedListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner);

        var expectedListings =
            new Dictionary<Guid, ListingStatus>
            {
                [activeListingId] = ListingStatus.Active,
                [draftListingId] = ListingStatus.Draft,
                [archivedListingId] = ListingStatus.Archived,
                [reservedListingId] = ListingStatus.Reserved,
                [soldListingId] = ListingStatus.Sold,
                [rentedListingId] = ListingStatus.Rented
            };

        foreach ((Guid listingId, ListingStatus status) in expectedListings)
        {
            await ListingTestHelpers.SetListingStatusAsync(
                _factory,
                listingId,
                status);
        }

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response =
                await _httpClient.GetAsync(
                    "/api/listings/my" +
                    "?lang=en" +
                    "&page=1" +
                    "&pageSize=100");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();

            JsonElement items = json.GetProperty("items");

            items.GetArrayLength().Should().Be(6);
            json.GetProperty("totalCount").GetInt32().Should().Be(6);

            Dictionary<Guid, ListingStatus> actualListings = items
                .EnumerateArray()
                .ToDictionary(
                    item => item.GetProperty("id").GetGuid(),
                    ReadListingStatusFromJson);

            actualListings.Should().BeEquivalentTo(expectedListings);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private static async Task<List<JsonElement>> ReadPagedListingItemsAsync(
        HttpResponseMessage response)
    {
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return json
            .GetProperty("items")
            .EnumerateArray()
            .ToList();
    }

    private static ListingStatus ReadListingStatusFromJson(JsonElement listingJson)
    {
        JsonElement statusElement = listingJson.GetProperty("status");

        if (statusElement.ValueKind == JsonValueKind.String)
        {
            return Enum.Parse<ListingStatus>(
                statusElement.GetString()!,
                ignoreCase: true);
        }

        return (ListingStatus)statusElement.GetInt32();
    }
}
