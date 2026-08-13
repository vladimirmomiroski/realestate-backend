using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    private const decimal PrivateLocationLatitude = 41.998123m;
    private const decimal PrivateLocationLongitude = 21.425456m;

    private static readonly DateTime PrivateLocationConfirmedAtUtc =
        new(2026, 8, 13, 12, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(PrivateLocationState.Unresolved)]
    [InlineData(PrivateLocationState.LegacyUnverified)]
    [InlineData(PrivateLocationState.Confirmed)]
    public async Task GetMyListings_ReturnsTruthfulPrivateLocationState(
        PrivateLocationState state)
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await EstablishPrivateLocationStateAsync(listingId, state);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                "/api/listings/my?lang=en&page=1&pageSize=20");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement page =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            JsonElement listing = page.GetProperty("items")
                .EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == listingId);

            AssertPrivateLocationState(listing, state);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(PrivateLocationState.Unresolved)]
    [InlineData(PrivateLocationState.LegacyUnverified)]
    [InlineData(PrivateLocationState.Confirmed)]
    public async Task GetListingManagement_ReturnsTruthfulAuthoringLocationState(
        PrivateLocationState state)
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await EstablishPrivateLocationStateAsync(listingId, state);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement listing =
                await response.Content.ReadFromJsonAsync<JsonElement>();

            AssertPrivateLocationState(listing, state);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task EstablishPrivateLocationStateAsync(
        Guid listingId,
        PrivateLocationState state)
    {
        if (state == PrivateLocationState.Unresolved)
        {
            return;
        }

        if (state == PrivateLocationState.LegacyUnverified)
        {
            await ListingTestHelpers.SetLegacyCoordinatesAsync(
                _factory,
                listingId,
                PrivateLocationLatitude,
                PrivateLocationLongitude);
            return;
        }

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Listing listing = await dbContext.Listings
            .SingleAsync(current => current.Id == listingId);

        listing.ConfirmLocation(
            PrivateLocationLatitude,
            PrivateLocationLongitude,
            LocationPrecision.Street,
            "private-contract-provider",
            "Opaque:Private/Result:CaseSensitive",
            "Confirmed private display name",
            PrivateLocationConfirmedAtUtc);

        await dbContext.SaveChangesAsync();
    }

    private static void AssertPrivateLocationState(
        JsonElement listing,
        PrivateLocationState state)
    {
        listing.TryGetProperty("geocodingProviderKey", out _)
            .Should().BeFalse();
        listing.TryGetProperty("geocodingResultReference", out _)
            .Should().BeFalse();

        if (state == PrivateLocationState.Unresolved)
        {
            AssertNullLocationState(listing);
            return;
        }

        listing.GetProperty("latitude").GetDecimal()
            .Should().Be(PrivateLocationLatitude);
        listing.GetProperty("longitude").GetDecimal()
            .Should().Be(PrivateLocationLongitude);

        if (state == PrivateLocationState.LegacyUnverified)
        {
            listing.GetProperty("locationPrecision").ValueKind
                .Should().Be(JsonValueKind.Null);
            listing.GetProperty("geocodedDisplayName").ValueKind
                .Should().Be(JsonValueKind.Null);
            listing.GetProperty("locationConfirmedAtUtc").ValueKind
                .Should().Be(JsonValueKind.Null);
            return;
        }

        listing.GetProperty("locationPrecision").GetString()
            .Should().Be(nameof(LocationPrecision.Street));
        listing.GetProperty("geocodedDisplayName").GetString()
            .Should().Be("Confirmed private display name");
        listing.GetProperty("locationConfirmedAtUtc").GetDateTime()
            .Should().Be(PrivateLocationConfirmedAtUtc);
    }

    private static void AssertNullLocationState(JsonElement listing)
    {
        foreach (string propertyName in new[]
        {
            "latitude",
            "longitude",
            "locationPrecision",
            "geocodedDisplayName",
            "locationConfirmedAtUtc"
        })
        {
            listing.GetProperty(propertyName).ValueKind
                .Should().Be(JsonValueKind.Null);
        }
    }

    public enum PrivateLocationState
    {
        Unresolved,
        LegacyUnverified,
        Confirmed
    }
}
