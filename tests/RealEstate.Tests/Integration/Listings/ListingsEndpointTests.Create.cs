using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task CreateListing_WithValidRequest_ReturnsCreated()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        try
        {
            _httpClient.AuthorizeAs(user.AccessToken);

            var request = ListingTestHelpers.CreateValidListingRequest();

            var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().NotBeEmpty();
            json.GetProperty("status").GetString().Should().Be(nameof(ListingStatus.Draft));
            json.GetProperty("languageCode").GetString().Should().Be("en");
            json.GetProperty("title").GetString().Should().Be("Integration test apartment");

            json.GetProperty("pricePerSquareMeter").GetDecimal().Should().Be(1706.90m);
            json.GetProperty("balconyCount").GetInt32().Should().Be(2);
            json.GetProperty("parkingSpaces").GetInt32().Should().Be(1);
            json.GetProperty("hasBasement").GetBoolean().Should().BeTrue();
            json.GetProperty("isExchangePossible").GetBoolean().Should().BeFalse();
            json.GetProperty("heatingType").GetString().Should().Be("Central");
            json.GetProperty("furnishingStatus").GetString().Should().Be("Furnished");
            json.GetProperty("condition").GetString().Should().Be("Good");
            json.GetProperty("yearRenovated").GetInt32().Should().Be(2022);
            json.GetProperty("orientation").GetString().Should().Be("SouthEast");
            json.GetProperty("municipality").GetString().Should().Be("Centar");

            var apartmentDetails = json.GetProperty("apartmentDetails");

            apartmentDetails.GetProperty("apartmentType").GetString().Should().Be("Standard");
            apartmentDetails.GetProperty("floor").GetInt32().Should().Be(4);
            apartmentDetails.GetProperty("totalFloors").GetInt32().Should().Be(8);
            apartmentDetails.GetProperty("hasElevator").GetBoolean().Should().BeTrue();

            json.GetProperty("houseDetails").ValueKind.Should().Be(JsonValueKind.Null);

            json.GetProperty("primaryImageUrl").ValueKind.Should().Be(JsonValueKind.Null);
            json.GetProperty("images").ValueKind.Should().Be(JsonValueKind.Array);
            json.GetProperty("images").GetArrayLength().Should().Be(0);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        var request = ListingTestHelpers.CreateValidListingRequest();

        var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateListing_WhenCurrentUserIsDisabled_ReturnsForbidden()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(user.UserId, UserStatus.Disabled);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = ListingTestHelpers.CreateValidListingRequest();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/listings",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_WithAccessToken_StoresCreatedByUserId()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = ListingTestHelpers.CreateValidListingRequest();

            var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            Guid listingId = json.GetProperty("id").GetGuid();

            using IServiceScope scope = _factory.Services.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var listing = await dbContext.Listings.SingleAsync(
                listing => listing.Id == listingId);

            listing.CreatedByUserId.Should().Be(user.UserId);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }


    [Fact]
    public async Task CreateListing_WithDecimalPricePerSquareMeter_ReturnsRoundedValue()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        try
        {
            _httpClient.AuthorizeAs(user.AccessToken);

            var request = ListingTestHelpers.CreateValidListingRequest(price: 125000);

            var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("pricePerSquareMeter").GetDecimal().Should().Be(2155.17m);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }


    [Fact]
    public async Task CreateListing_WithValidHouseRequest_ReturnsCreated()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        try
        {
            _httpClient.AuthorizeAs(user.AccessToken);

            var request = ListingTestHelpers.CreateValidHouseListingRequest();

            var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("propertyType").GetString().Should().Be("House");
            json.GetProperty("apartmentDetails").ValueKind.Should().Be(JsonValueKind.Null);

            var houseDetails = json.GetProperty("houseDetails");

            houseDetails.GetProperty("houseType").GetString().Should().Be("Detached");
            houseDetails.GetProperty("numberOfFloors").GetInt32().Should().Be(2);
            houseDetails.GetProperty("yardAreaSquareMeters").GetDecimal().Should().Be(350);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_WithInvalidPrice_ReturnsBadRequest()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        try
        {
            _httpClient.AuthorizeAs(user.AccessToken);

            var request = ListingTestHelpers.CreateValidListingRequest(price: 0);

            var response = await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var error = await response.Content.ReadAsStringAsync();

            error.Should().Contain("Price must be greater than zero.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_WhenUserHasReachedTemporaryLimit_ReturnsBadRequest()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await ListingTestHelpers.CreateListingAsAsync(_httpClient, user);
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, user);
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, user);
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, user);
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, user);
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, user);
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, user);
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, user);
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, user);
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, user);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = ListingTestHelpers.CreateValidListingRequest();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/listings",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            string error = await response.Content.ReadAsStringAsync();

            error.Should().Contain("Free listing limit reached");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_LimitIsPerUser_NotGlobal()
    {
        AuthenticatedTestUser firstUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser secondUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await ListingTestHelpers.CreateListingAsAsync(_httpClient, firstUser);
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, firstUser);
        await ListingTestHelpers.CreateListingAsAsync(_httpClient, firstUser);

        _httpClient.AuthorizeAs(secondUser.AccessToken);

        try
        {
            var request = ListingTestHelpers.CreateValidListingRequest();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/listings",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}
