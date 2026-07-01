using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingImagesEndpointTests
{
    [Fact]
    public async Task SetPrimaryListingImage_WithExistingImage_ReturnsOk()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        var firstImage = await UploadImageAsync(listingId, owner, "first-image.png");
        var secondImage = await UploadImageAsync(listingId, owner, "second-image.png");

        var firstImageId = firstImage.GetProperty("id").GetGuid();
        var secondImageId = secondImage.GetProperty("id").GetGuid();

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/images/{secondImageId}/primary",
                null);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().Be(secondImageId);
            json.GetProperty("isPrimary").GetBoolean().Should().BeTrue();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        var listingResponse = await _httpClient.GetAsync($"/api/listings/{listingId}?lang=en");

        listingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listingJson = await listingResponse.Content.ReadFromJsonAsync<JsonElement>();
        var images = listingJson.GetProperty("images");

        images.EnumerateArray()
            .Count(image => image.GetProperty("isPrimary").GetBoolean())
            .Should()
            .Be(1);

        var firstImageFromListing = images.EnumerateArray()
            .First(image => image.GetProperty("id").GetGuid() == firstImageId);

        var secondImageFromListing = images.EnumerateArray()
            .First(image => image.GetProperty("id").GetGuid() == secondImageId);

        firstImageFromListing.GetProperty("isPrimary").GetBoolean().Should().BeFalse();
        secondImageFromListing.GetProperty("isPrimary").GetBoolean().Should().BeTrue();

        listingJson.GetProperty("primaryImageUrl")
            .GetString()
            .Should()
            .Be(secondImageFromListing.GetProperty("url").GetString());
    }
}
