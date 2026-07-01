using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingImagesEndpointTests
{
    [Fact]
    public async Task ReorderListingImages_WithValidOrder_ReturnsOrderedImages()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        var firstImage = await UploadImageAsync(listingId, owner, "first-image.png");
        var secondImage = await UploadImageAsync(listingId, owner, "second-image.png");
        var thirdImage = await UploadImageAsync(listingId, owner, "third-image.png");

        var firstImageId = firstImage.GetProperty("id").GetGuid();
        var secondImageId = secondImage.GetProperty("id").GetGuid();
        var thirdImageId = thirdImage.GetProperty("id").GetGuid();

        var request = new
        {
            imageIds = new[]
            {
            thirdImageId,
            firstImageId,
            secondImageId
        }
        };

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}/images/order",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetArrayLength().Should().Be(3);

            json[0].GetProperty("id").GetGuid().Should().Be(thirdImageId);
            json[0].GetProperty("sortOrder").GetInt32().Should().Be(0);

            json[1].GetProperty("id").GetGuid().Should().Be(firstImageId);
            json[1].GetProperty("sortOrder").GetInt32().Should().Be(1);

            json[2].GetProperty("id").GetGuid().Should().Be(secondImageId);
            json[2].GetProperty("sortOrder").GetInt32().Should().Be(2);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}
