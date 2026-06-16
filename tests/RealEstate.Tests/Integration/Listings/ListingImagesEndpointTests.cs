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
    }
}
