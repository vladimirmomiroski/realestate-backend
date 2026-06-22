using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using System.Net.Http.Headers;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings

{
    public sealed class ListingImagesEndpointTests : IClassFixture<CustomWebApplicationFactory>
    {

        private readonly HttpClient _httpClient;

        public ListingImagesEndpointTests(CustomWebApplicationFactory factory)
        {
            _httpClient = factory.CreateClient();
        }

        [Fact]
        public async Task UploadListingImage_WithValidImage_ReturnsCreated()
        {
            (Guid listingId, AuthenticatedTestUser owner) =
                await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

            var json = await UploadImageAsync(listingId, owner);

            json.GetProperty("id").GetGuid().Should().NotBeEmpty();
            json.GetProperty("url").GetString().Should().NotBeNullOrWhiteSpace();
            json.GetProperty("contentType").GetString().Should().Be("image/png");
            json.GetProperty("sizeBytes").GetInt64().Should().Be(4);
            json.GetProperty("sortOrder").GetInt32().Should().Be(0);
            json.GetProperty("isPrimary").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task GetListingById_AfterImageUpload_ReturnsImageData()
        {
            (Guid listingId, AuthenticatedTestUser owner) =
                await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

            await UploadImageAsync(listingId, owner);

            var response = await _httpClient.GetAsync($"/api/listings/{listingId}?lang=en");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("primaryImageUrl").GetString().Should().NotBeNullOrWhiteSpace();

            var images = json.GetProperty("images");

            images.ValueKind.Should().Be(JsonValueKind.Array);
            images.GetArrayLength().Should().Be(1);

            images[0].GetProperty("isPrimary").GetBoolean().Should().BeTrue();
            images[0].GetProperty("sortOrder").GetInt32().Should().Be(0);
        }

        [Fact]
        public async Task UploadListingImage_WithInvalidFileType_ReturnsBadRequest()
        {
            (Guid listingId, AuthenticatedTestUser owner) =
                await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

            using var form = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent("not an image"u8.ToArray());
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

            form.Add(fileContent, "file", "notes.txt");

            _httpClient.AuthorizeAs(owner.AccessToken);

            try
            {
                var response = await _httpClient.PostAsync(
                    $"/api/listings/{listingId}/images",
                    form);

                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

                var error = await response.Content.ReadAsStringAsync();

                error.Should().Contain("Only JPG, JPEG, PNG, and WEBP images are allowed.");
            }
            finally
            {
                _httpClient.ClearAuthorization();
            }
        }

        [Fact]
        public async Task UploadListingImage_WithMissingListing_ReturnsNotFound()
        {
            Guid missingListingId = Guid.NewGuid();

            AuthenticatedTestUser user =
                await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

            using var form = new MultipartFormDataContent();

            var imageBytes = new byte[]
            {
                0x89, 0x50, 0x4E, 0x47
            };

            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            form.Add(fileContent, "file", "test-image.png");

            _httpClient.AuthorizeAs(user.AccessToken);

            try
            {
                var response = await _httpClient.PostAsync(
                    $"/api/listings/{missingListingId}/images",
                    form);

                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }
            finally
            {
                _httpClient.ClearAuthorization();
            }
        }

        [Fact]
        public async Task DeleteListingImage_WithExistingImage_ReturnsNoContent()
        {
            (Guid listingId, AuthenticatedTestUser owner) =
                await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

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

        private async Task<JsonElement> UploadImageAsync(
            Guid listingId,
            AuthenticatedTestUser owner,
             string fileName = "test-image.png",
            string contentType = "image/png")
        {
            _httpClient.AuthorizeAs(owner.AccessToken);

            try
            {
                using var form = new MultipartFormDataContent();

                var imageBytes = new byte[]
                {
                     0x89, 0x50, 0x4E, 0x47
                };

                var fileContent = new ByteArrayContent(imageBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                form.Add(fileContent, "file", fileName);

                var response = await _httpClient.PostAsync(
                    $"/api/listings/{listingId}/images",
                    form);

                response.StatusCode.Should().Be(HttpStatusCode.Created);

                return await response.Content.ReadFromJsonAsync<JsonElement>();
            }
            finally
            {
                _httpClient.ClearAuthorization();
            }
        }

        private static MultipartFormDataContent CreateImageUploadContent(
        string fileName = "test-image.jpg")
        {
            var content = new MultipartFormDataContent();

            byte[] imageBytes =
            [
                0xFF, 0xD8, 0xFF, 0xE0,
                0x00, 0x10, 0x4A, 0x46,
                0x49, 0x46, 0x00, 0x01
            ];

            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            content.Add(fileContent, "file", fileName);

            return content;
        }

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

        private async Task<Guid> UploadImageAsAsync(
            Guid listingId,
            AuthenticatedTestUser user)
        {
            _httpClient.AuthorizeAs(user.AccessToken);

            try
            {
                using MultipartFormDataContent content = CreateImageUploadContent();

                HttpResponseMessage response = await _httpClient.PostAsync(
                    $"/api/listings/{listingId}/images",
                    content);

                response.StatusCode.Should().Be(HttpStatusCode.Created);

                JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

                return json.GetProperty("id").GetGuid();
            }
            finally
            {
                _httpClient.ClearAuthorization();
            }
        }

    }

}
