using FluentAssertions;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingImagesEndpointTests
{
    [Fact]
    public async Task DeleteListingImage_WithExistingImage_ReturnsNoContent()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);

        var image = await UploadImageAsync(listingId, owner);

        var imageId = image.GetProperty("id").GetGuid();

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var response = await _httpClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{imageId}");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        var listingResponse = await _httpClient.GetAsync($"/api/listings/{listingId}?lang=en");

        listingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listingJson = await listingResponse.Content.ReadFromJsonAsync<JsonElement>();

        listingJson.GetProperty("primaryImageUrl").ValueKind.Should().Be(JsonValueKind.Null);
        listingJson.GetProperty("images").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task DeleteListingImage_WhenPrimaryDeleted_MakesNextImagePrimary()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);

        var firstImage = await UploadImageAsync(listingId, owner, "first-image.png");
        var secondImage = await UploadImageAsync(listingId, owner, "second-image.png");

        var firstImageId = firstImage.GetProperty("id").GetGuid();
        var secondImageId = secondImage.GetProperty("id").GetGuid();

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var deleteResponse = await _httpClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{firstImageId}");

            deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        var listingResponse = await _httpClient.GetAsync($"/api/listings/{listingId}?lang=en");

        listingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listingJson = await listingResponse.Content.ReadFromJsonAsync<JsonElement>();
        var images = listingJson.GetProperty("images");

        images.GetArrayLength().Should().Be(1);
        images[0].GetProperty("id").GetGuid().Should().Be(secondImageId);
        images[0].GetProperty("isPrimary").GetBoolean().Should().BeTrue();

        listingJson.GetProperty("primaryImageUrl")
            .GetString()
            .Should()
            .Be(images[0].GetProperty("url").GetString());
    }
}
