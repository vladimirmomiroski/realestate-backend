using FluentAssertions;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task GetMyListings_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.GetAsync("/api/listings/my");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyListings_WithAccessToken_ReturnsOnlyCurrentUsersListings()
    {
        AuthenticatedTestUser firstUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser secondUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid firstUserListingId;
        Guid secondUserListingId;

        try
        {
            _httpClient.AuthorizeAs(firstUser.AccessToken);

            var firstRequest = ListingTestHelpers.CreateValidListingRequest();

            HttpResponseMessage firstCreateResponse =
                await _httpClient.PostAsJsonAsync("/api/listings", firstRequest);

            firstCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement firstCreateJson =
                await firstCreateResponse.Content.ReadFromJsonAsync<JsonElement>();

            firstUserListingId = firstCreateJson.GetProperty("id").GetGuid();

            _httpClient.AuthorizeAs(secondUser.AccessToken);

            var secondRequest = ListingTestHelpers.CreateValidListingRequest();

            HttpResponseMessage secondCreateResponse =
                await _httpClient.PostAsJsonAsync("/api/listings", secondRequest);

            secondCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement secondCreateJson =
                await secondCreateResponse.Content.ReadFromJsonAsync<JsonElement>();

            secondUserListingId = secondCreateJson.GetProperty("id").GetGuid();

            _httpClient.AuthorizeAs(firstUser.AccessToken);

            HttpResponseMessage response =
                await _httpClient.GetAsync("/api/listings/my?lang=mk&page=1&pageSize=20");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            List<Guid> listingIds = json
                .GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid())
                .ToList();

            listingIds.Should().Contain(firstUserListingId);
            listingIds.Should().NotContain(secondUserListingId);

            json.GetProperty("totalCount").GetInt32().Should().Be(1);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}
