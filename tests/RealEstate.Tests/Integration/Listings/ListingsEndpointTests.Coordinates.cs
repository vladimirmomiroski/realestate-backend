using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Domain.Entities;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task CreateListing_WithUnknownLocationMembers_PersistsUnresolvedDraft()
    {
        JsonObject request = JsonSerializer.SerializeToNode(
            ListingTestHelpers.CreateValidListingRequest(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        request["latitude"] = 41.998123m;
        request["longitude"] = 21.425456m;
        request["locationPrecision"] = "ExactAddress";
        request["geocodingProviderKey"] = "caller-provider";

        HttpResponseMessage response =
            await PostListingAsNewUserAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement created =
            await response.Content.ReadFromJsonAsync<JsonElement>();
        Guid listingId = created.GetProperty("id").GetGuid();

        created.GetProperty("latitude").ValueKind
            .Should().Be(JsonValueKind.Null);
        created.GetProperty("longitude").ValueKind
            .Should().Be(JsonValueKind.Null);

        (decimal? latitude, decimal? longitude) =
            await ReadPersistedCoordinatesAsync(listingId);
        latitude.Should().BeNull();
        longitude.Should().BeNull();
    }

    [Fact]
    public async Task UpdateListing_WithCoordinateJsonMembers_CannotReplacePersistedCoordinates()
    {
        const decimal originalLatitude = 41.998123m;
        const decimal originalLongitude = 21.425456m;
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetPersistedCoordinatesAsync(
            listingId,
            originalLatitude,
            originalLongitude);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            JsonObject payload = CreateWritablePayload(
                await GetManagementJsonAsync(listingId));
            payload["latitude"] = -12.345678m;
            payload["longitude"] = 98.765432m;

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                payload);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement body =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("latitude").GetDecimal()
                .Should().Be(originalLatitude);
            body.GetProperty("longitude").GetDecimal()
                .Should().Be(originalLongitude);

            (decimal? latitude, decimal? longitude) =
                await ReadPersistedCoordinatesAsync(listingId);
            latitude.Should().Be(originalLatitude);
            longitude.Should().Be(originalLongitude);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenCoordinatesAreOmitted_PreservesPersistedCoordinates()
    {
        const decimal originalLatitude = 41.998123m;
        const decimal originalLongitude = 21.425456m;
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetPersistedCoordinatesAsync(
            listingId,
            originalLatitude,
            originalLongitude);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            JsonObject payload = CreateWritablePayload(
                await GetManagementJsonAsync(listingId));
            payload.ContainsKey("latitude").Should().BeFalse();
            payload.ContainsKey("longitude").Should().BeFalse();
            payload["price"] = 321_000m;

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                payload);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement body =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("price").GetDecimal().Should().Be(321_000m);

            (decimal? latitude, decimal? longitude) =
                await ReadPersistedCoordinatesAsync(listingId);
            latitude.Should().Be(originalLatitude);
            longitude.Should().Be(originalLongitude);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("E_R")]
    [InlineData("EÜR")]
    public async Task CreateListing_WithInvalidCurrency_ReturnsBadRequest(
        string currency)
    {
        object request =
            ListingTestHelpers.CreateValidListingRequest(
                currency: currency);

        HttpResponseMessage response =
            await PostListingAsNewUserAsync(request);

        string responseBody =
            await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest);

        responseBody.Should().Contain(
            CreateListingValidator.InvalidCurrencyError);
    }

    [Fact]
    public async Task CreateListing_WithTrimmedMixedCaseCurrency_ReturnsNormalizedCurrency()
    {
        object request =
            ListingTestHelpers.CreateValidListingRequest(
                currency: " eUr ");

        HttpResponseMessage response =
            await PostListingAsNewUserAsync(request);

        response.StatusCode.Should().Be(
            HttpStatusCode.Created);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.GetProperty("currency").GetString()
            .Should().Be("EUR");
    }

    private async Task<HttpResponseMessage> PostListingAsNewUserAsync(
        object request)
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            return await _httpClient.PostAsJsonAsync(
                "/api/listings",
                request);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task SetPersistedCoordinatesAsync(
        Guid listingId,
        decimal latitude,
        decimal longitude)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Listing listing = await dbContext.Listings.SingleAsync(
            current => current.Id == listingId);

        listing.Latitude = latitude;
        listing.Longitude = longitude;

        await dbContext.SaveChangesAsync();
    }

    private async Task<(decimal? Latitude, decimal? Longitude)>
        ReadPersistedCoordinatesAsync(Guid listingId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Listing listing = await dbContext.Listings
            .AsNoTracking()
            .SingleAsync(current => current.Id == listingId);

        return (listing.Latitude, listing.Longitude);
    }
}
