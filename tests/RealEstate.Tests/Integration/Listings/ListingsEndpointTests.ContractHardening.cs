using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Common;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task RoomsContract_CreateAndUpdateRejectNegativesWithoutPersistingThem()
    {
        var (listingId, owner) = await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        _httpClient.AuthorizeAs(owner.AccessToken);
        try
        {
            var create = await _httpClient.PostAsJsonAsync("/api/listings",
                ListingTestHelpers.CreateValidListingRequest(rooms: -1m));
            await ApiFailureAssertions.AssertProblemAsync(create, HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed, "/api/listings", validationKey: "rooms");

            var payload = CreateValidUpdatePayload();
            payload["rooms"] = -0.1m;
            var update = await _httpClient.PutAsJsonAsync($"/api/listings/{listingId}", payload);
            await ApiFailureAssertions.AssertProblemAsync(update, HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed, $"/api/listings/{listingId}", validationKey: "rooms");

            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
            var owned = await db.Listings.AsNoTracking()
                .Where(listing => listing.CreatedByUserId == owner.UserId).ToListAsync();
            owned.Should().ContainSingle().Which.Rooms.Should().Be(2m);
        }
        finally { _httpClient.ClearAuthorization(); }
    }

    [Fact]
    public async Task RoomsContract_PreexistingNegativeDraftCannotBePublished()
    {
        var (listingId, owner) = await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await PrepareListingForPublishAsync(listingId);
        // Reproduce historical authoring data only inside the disposable test database.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
            var listing = await db.Listings.SingleAsync(item => item.Id == listingId);
            listing.Rooms = -1m;
            await db.SaveChangesAsync();
        }

        _httpClient.AuthorizeAs(owner.AccessToken);
        try
        {
            var response = await _httpClient.PutAsync($"/api/listings/{listingId}/publish", null);
            await ApiFailureAssertions.AssertProblemAsync(response, HttpStatusCode.Conflict,
                ErrorCodes.ConflictListingNotReady, $"/api/listings/{listingId}/publish");
            (await GetListingStatusAsync(listingId)).Should().Be(ListingStatus.Draft);
        }
        finally { _httpClient.ClearAuthorization(); }
    }

    [Fact]
    public async Task GetListings_OffsetContract_PreservesFilteredOrderAndExtremePageMetadata()
    {
        const string currency = "KRB";
        var (firstId, owner) = await ListingTestHelpers.CreateListingWithOwnerAsync(
            _httpClient, currency: currency);
        var ids = new List<Guid> { firstId };
        for (int index = 1; index < 5; index++)
            ids.Add(await ListingTestHelpers.CreateListingAsAsync(_httpClient, owner, currency: currency));

        // This same-currency draft must remain excluded from the public count.
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, owner, currency: currency);
        var timestamp = new DateTime(2038, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int index = 0; index < ids.Count; index++)
            await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
                _factory, ids[index], ListingStatus.Active, timestamp.AddMinutes(index));

        var expectedIds = ids.AsEnumerable().Reverse().ToArray();
        for (int page = 1; page <= 4; page++)
        {
            var expectedPageIds = expectedIds.Skip((page - 1) * 2).Take(2).ToArray();
            JsonElement body = await ReadPaginationBodyAsync(await _httpClient.GetAsync(
                $"/api/listings?lang=en&currency={currency}&listingType=Sale&page={page}&pageSize=2"));
            AssertPaginationContract(body, expectedPageIds.Length, page, 2, 5, 3, page < 3, page > 1);
            body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
                .Should().Equal(expectedPageIds);
        }

        foreach (int page in new[] { 214748366, int.MaxValue })
        {
            JsonElement body = await ReadPaginationBodyAsync(await _httpClient.GetAsync(
                $"/api/listings?lang=en&currency={currency}&page={page}&pageSize=20"));
            AssertPaginationContract(body, 0, page, 20, 5, 1, false, true);
        }

        foreach (int page in new[] { 1, 2, 214748366, int.MaxValue })
        {
            JsonElement empty = await ReadPaginationBodyAsync(await _httpClient.GetAsync(
                $"/api/listings?lang=en&currency={currency}&listingType=Rent&page={page}&pageSize=20"));
            AssertPaginationContract(empty, 0, page, 20, 0, 0, false, page > 1);
        }
    }
}
