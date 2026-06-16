using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using System.Net.Http.Headers;

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
            var listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

            using var form = new MultipartFormDataContent();

            var imageBytes = new byte[]
            {
        0x89, 0x50, 0x4E, 0x47
            };

            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            form.Add(fileContent, "file", "test-image.png");

            var response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                form);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().NotBeEmpty();
            json.GetProperty("url").GetString().Should().NotBeNullOrWhiteSpace();
            json.GetProperty("contentType").GetString().Should().Be("image/png");
            json.GetProperty("sizeBytes").GetInt64().Should().Be(imageBytes.Length);
            json.GetProperty("sortOrder").GetInt32().Should().Be(0);
            json.GetProperty("isPrimary").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task GetListingById_AfterImageUpload_ReturnsImageData()
        {
            var listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

            using var form = new MultipartFormDataContent();

            var imageBytes = new byte[]
            {
        0x89, 0x50, 0x4E, 0x47
            };

            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            form.Add(fileContent, "file", "test-image.png");

            var uploadResponse = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                form);

            uploadResponse.EnsureSuccessStatusCode();

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
            var listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

            using var form = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent("not an image"u8.ToArray());
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

            form.Add(fileContent, "file", "notes.txt");

            var response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                form);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var error = await response.Content.ReadAsStringAsync();

            error.Should().Contain("Only JPG, JPEG, PNG, and WEBP images are allowed.");
        }

        [Fact]
        public async Task UploadListingImage_WithMissingListing_ReturnsNotFound()
        {
            var missingListingId = Guid.NewGuid();

            using var form = new MultipartFormDataContent();

            var imageBytes = new byte[]
            {
        0x89, 0x50, 0x4E, 0x47
            };

            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            form.Add(fileContent, "file", "test-image.png");

            var response = await _httpClient.PostAsync(
                $"/api/listings/{missingListingId}/images",
                form);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task DeleteListingImage_WithExistingImage_ReturnsNoContent()
        {
            var listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
            var image = await UploadImageAsync(listingId);

            var imageId = image.GetProperty("id").GetGuid();

            var response = await _httpClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{imageId}");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var listingResponse = await _httpClient.GetAsync($"/api/listings/{listingId}?lang=en");

            listingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var listingJson = await listingResponse.Content.ReadFromJsonAsync<JsonElement>();

            listingJson.GetProperty("primaryImageUrl").ValueKind.Should().Be(JsonValueKind.Null);
            listingJson.GetProperty("images").GetArrayLength().Should().Be(0);
        }

        [Fact]
        public async Task DeleteListingImage_WhenPrimaryDeleted_MakesNextImagePrimary()
        {
            var listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

            var firstImage = await UploadImageAsync(listingId, "first-image.png");
            var secondImage = await UploadImageAsync(listingId, "second-image.png");

            var firstImageId = firstImage.GetProperty("id").GetGuid();
            var secondImageId = secondImage.GetProperty("id").GetGuid();

            var deleteResponse = await _httpClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{firstImageId}");

            deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

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
            var listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

            var firstImage = await UploadImageAsync(listingId, "first-image.png");
            var secondImage = await UploadImageAsync(listingId, "second-image.png");

            var firstImageId = firstImage.GetProperty("id").GetGuid();
            var secondImageId = secondImage.GetProperty("id").GetGuid();

            var response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/images/{secondImageId}/primary",
                null);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().Be(secondImageId);
            json.GetProperty("isPrimary").GetBoolean().Should().BeTrue();

            var listingResponse = await _httpClient.GetAsync($"/api/listings/{listingId}?lang=en");

            listingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var listingJson = await listingResponse.Content.ReadFromJsonAsync<JsonElement>();
            var images = listingJson.GetProperty("images");

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
            var listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

            var firstImage = await UploadImageAsync(listingId, "first-image.png");
            var secondImage = await UploadImageAsync(listingId, "second-image.png");
            var thirdImage = await UploadImageAsync(listingId, "third-image.png");

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

        private async Task<JsonElement> UploadImageAsync(
        Guid listingId,
        string fileName = "test-image.png",
        string contentType = "image/png")
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

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
    }


}
