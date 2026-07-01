using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RealEstate.Tests.Integration.Auth;


namespace RealEstate.Tests.Integration.Listings;

    public sealed partial class ListingImagesEndpointTests
{
    [Fact]
    public async Task UploadImage_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        using MultipartFormDataContent content = CreateImageUploadContent();

        HttpResponseMessage response = await _httpClient.PostAsync(
            $"/api/listings/{Guid.NewGuid()}/images",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteImage_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.DeleteAsync(
            $"/api/listings/{Guid.NewGuid()}/images/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetPrimaryImage_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.PutAsync(
            $"/api/listings/{Guid.NewGuid()}/images/{Guid.NewGuid()}/primary",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReorderImages_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        var request = new
        {
            imageIds = Array.Empty<Guid>()
        };

        HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
            $"/api/listings/{Guid.NewGuid()}/images/order",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UploadImage_WithDifferentUser_ReturnsForbidden()
    {
        (Guid listingId, _) = await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        AuthenticatedTestUser differentUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(differentUser.AccessToken);

        try
        {
            using MultipartFormDataContent content = CreateImageUploadContent();

            HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                content);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DeleteImage_WithDifferentUser_ReturnsForbidden()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        Guid imageId = await UploadImageAsAsync(listingId, owner);

        AuthenticatedTestUser differentUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(differentUser.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{imageId}");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task SetPrimaryImage_WithDifferentUser_ReturnsForbidden()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        Guid imageId = await UploadImageAsAsync(listingId, owner);

        AuthenticatedTestUser differentUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(differentUser.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/images/{imageId}/primary",
                new StringContent(string.Empty));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task ReorderImages_WithDifferentUser_ReturnsForbidden()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        Guid imageId = await UploadImageAsAsync(listingId, owner);

        AuthenticatedTestUser differentUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(differentUser.AccessToken);

        try
        {
            var request = new
            {
                imageIds = new[] { imageId }
            };

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}/images/order",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UploadImage_WithDifferentActiveAgencyMember_ReturnsForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agencyMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agencyMember.UserId);

        Guid listingId = await CreateAgencyListingAsAsync(owner, agencyId);

        _httpClient.AuthorizeAs(agencyMember.AccessToken);

        try
        {
            using MultipartFormDataContent content = CreateImageUploadContent();

            HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                content);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DeleteImage_WithDifferentActiveAgencyMember_ReturnsForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agencyMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agencyMember.UserId);

        Guid listingId = await CreateAgencyListingAsAsync(owner, agencyId);

        Guid imageId = await UploadImageAsAsync(listingId, owner);

        _httpClient.AuthorizeAs(agencyMember.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{imageId}");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}

