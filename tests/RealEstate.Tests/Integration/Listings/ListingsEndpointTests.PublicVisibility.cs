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
    public async Task GetListings_ShouldOnlyReturnActiveListings()
    {
        // Arrange
        (Guid activeListingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        (Guid draftListingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        (Guid archivedListingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetListingStatusAsync(activeListingId, ListingStatus.Active);
        await SetListingStatusAsync(draftListingId, ListingStatus.Draft);
        await SetListingStatusAsync(archivedListingId, ListingStatus.Archived);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync("/api/listings?lang=en&page=1&pageSize=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        List<JsonElement> items = await ReadPagedListingItemsAsync(response);

        List<Guid> listingIds = items
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();

        listingIds.Should().Contain(activeListingId);
        listingIds.Should().NotContain(draftListingId);
        listingIds.Should().NotContain(archivedListingId);

        List<ListingStatus> statuses = items
            .Select(ReadListingStatusFromJson)
            .ToList();

        statuses.Should().OnlyContain(status => status == ListingStatus.Active);
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
    public async Task GetMyListings_ShouldStillReturnDraftActiveAndArchivedListings()
    {
        // Arrange
        (Guid activeListingId, var owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        Guid draftListingId =
            await ListingTestHelpers.CreateListingAsAsync(_httpClient, owner);

        Guid archivedListingId =
            await ListingTestHelpers.CreateListingAsAsync(_httpClient, owner);

        await SetListingStatusAsync(activeListingId, ListingStatus.Active);
        await SetListingStatusAsync(draftListingId, ListingStatus.Draft);
        await SetListingStatusAsync(archivedListingId, ListingStatus.Archived);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.GetAsync("/api/listings/my?lang=en&page=1&pageSize=100");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            List<JsonElement> items = await ReadPagedListingItemsAsync(response);

            List<Guid> listingIds = items
                .Select(item => item.GetProperty("id").GetGuid())
                .ToList();

            listingIds.Should().Contain(activeListingId);
            listingIds.Should().Contain(draftListingId);
            listingIds.Should().Contain(archivedListingId);
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
