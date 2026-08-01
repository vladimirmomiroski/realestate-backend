using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Domain.Entities;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingImagesEndpointTests
{
    private const int MaximumListingImageBytes = 5 * 1024 * 1024;

    [Fact]
    public async Task UploadImage_WithValidFile_PreservesCreatedContract()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using MultipartFormDataContent content = CreateImageUploadContent();
            using HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                content);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            ListingImageResponse? body =
                await response.Content.ReadFromJsonAsync<ListingImageResponse>();
            body.Should().NotBeNull();
            response.Headers.Location.Should().Be(
                new Uri(
                    $"/api/listings/{listingId}/images/{body!.Id}",
                    UriKind.Relative));
            body.Url.Should().StartWith($"/uploads/listings/{listingId}/");
            body.SortOrder.Should().Be(0);
            body.IsPrimary.Should().BeTrue();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UploadImage_WithIndependentlyAllowedExtensionAndMime_RemainsAccepted()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(fileContent, "file", "independent-allowlists.jpg");

            using HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                content);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            ListingImageResponse? body =
                await response.Content.ReadFromJsonAsync<ListingImageResponse>();
            body.Should().NotBeNull();
            body!.ContentType.Should().Be("image/png");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("missing", ErrorCodes.ValidationFileRequired)]
    [InlineData("empty", ErrorCodes.ValidationFileEmpty)]
    [InlineData("oversized", ErrorCodes.ValidationFileTooLarge)]
    [InlineData("extension", ErrorCodes.ValidationFileTypeNotSupported)]
    [InlineData("mime", ErrorCodes.ValidationFileTypeNotSupported)]
    public async Task UploadImage_WithInvalidFile_ReturnsCanonicalValidation(
        string scenario,
        string expectedCode)
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using MultipartFormDataContent content =
                CreateInvalidUploadContent(scenario);

            using HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images?ignored=secret",
                content);

            JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                expectedCode,
                $"/api/listings/{listingId}/images",
                validationKey: "file");
            body.GetProperty("title").GetString().Should().Be("Validation failed");
            body.GetProperty("detail").GetString().Should()
                .Be("One or more validation errors occurred.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UploadImage_WithUnsupportedRequestMediaType_ReturnsCanonical415()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using var content = new StringContent("not-multipart");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                content);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.UnsupportedMediaType,
                ErrorCodes.RequestMediaTypeNotSupported,
                $"/api/listings/{listingId}/images");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("upload")]
    [InlineData("delete")]
    [InlineData("primary")]
    [InlineData("reorder")]
    public async Task ImageMutation_WithNonGuidSubject_ReturnsInvalidPrincipal(
        string operation)
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        Guid imageId = await UploadImageAsAsync(listingId, owner);
        string token = AuthTestHelpers.CreateSignedToken(
            _factory,
            "not-a-guid",
            DateTime.UtcNow.AddMinutes(5));

        _httpClient.AuthorizeAs(token);

        try
        {
            using HttpResponseMessage response = await SendImageOperationAsync(
                operation,
                listingId,
                imageId);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Unauthorized,
                ErrorCodes.AuthenticationInvalidPrincipal,
                GetImageOperationPath(operation, listingId, imageId),
                bearerChallenge: true);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("upload")]
    [InlineData("delete")]
    [InlineData("primary")]
    [InlineData("reorder")]
    public async Task ImageMutation_WithMissingListing_ReturnsCanonicalNotFound(
        string operation)
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid missingListingId = Guid.NewGuid();
        Guid imageId = Guid.NewGuid();
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            using HttpResponseMessage response = await SendImageOperationAsync(
                operation,
                missingListingId,
                imageId);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                ErrorCodes.ResourceNotFound,
                GetImageOperationPath(operation, missingListingId, imageId));
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task ReorderImages_WithEmptyIds_ReturnsCanonicalValidation()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}/images/order",
                new { imageIds = Array.Empty<Guid>() });

            JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed,
                $"/api/listings/{listingId}/images/order",
                validationKey: "imageIds");
            body.GetProperty("errors").GetProperty("imageIds")[0]
                .GetString().Should().Be("Image ids are required.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task ReorderImages_WithDuplicateIds_ReturnsCanonicalValidation()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        Guid imageId = await UploadImageAsAsync(listingId, owner);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}/images/order",
                new { imageIds = new[] { imageId, imageId } });

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed,
                $"/api/listings/{listingId}/images/order",
                validationKey: "imageIds");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("primary")]
    public async Task ImageMutation_WithImageFromAnotherListing_ReturnsNotFound(
        string operation)
    {
        (Guid targetListingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        Guid otherListingId = await ListingTestHelpers.CreateListingAsAsync(
            _httpClient,
            owner);
        Guid otherImageId = await UploadImageAsAsync(otherListingId, owner);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using HttpResponseMessage response = operation == "delete"
                ? await _httpClient.DeleteAsync(
                    $"/api/listings/{targetListingId}/images/{otherImageId}")
                : await _httpClient.PutAsync(
                    $"/api/listings/{targetListingId}/images/{otherImageId}/primary",
                    new StringContent(string.Empty));

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                ErrorCodes.ResourceNotFound,
                operation == "delete"
                    ? $"/api/listings/{targetListingId}/images/{otherImageId}"
                    : $"/api/listings/{targetListingId}/images/{otherImageId}/primary");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UploadImage_AtCapacity_ReturnsCanonicalConflict()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        dbContext.Set<ListingImage>().AddRange(
            Enumerable.Range(0, 20).Select(index => new ListingImage
            {
                Id = Guid.NewGuid(),
                ListingId = listingId,
                OriginalFileName = $"capacity-{index}.jpg",
                StoredFileName = $"capacity-{index}.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 4,
                Url = $"/uploads/listings/{listingId}/capacity-{index}.jpg",
                SortOrder = index,
                IsPrimary = index == 0
            }));
        await dbContext.SaveChangesAsync();

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using MultipartFormDataContent content = CreateImageUploadContent();
            using HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                content);

            JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictResourceCapacity,
                $"/api/listings/{listingId}/images");
            body.GetProperty("title").GetString().Should().Be("Conflict");
            body.GetProperty("detail").GetString().Should()
                .Be("The resource has reached its allowed capacity.");

            (await dbContext.Set<ListingImage>()
                .CountAsync(image => image.ListingId == listingId))
                .Should().Be(20);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DeleteImage_PreservesSurvivingSortOrdersWithoutCompaction()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        Guid firstId = await UploadImageAsAsync(listingId, owner);
        Guid secondId = await UploadImageAsAsync(listingId, owner);
        Guid thirdId = await UploadImageAsAsync(listingId, owner);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{secondId}");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
        List<ListingImage> survivors = await dbContext.Set<ListingImage>()
            .AsNoTracking()
            .Where(image => image.ListingId == listingId)
            .OrderBy(image => image.SortOrder)
            .ToListAsync();

        survivors.Select(image => image.Id).Should().Equal(firstId, thirdId);
        survivors.Select(image => image.SortOrder).Should().Equal(0, 2);
        survivors.Single(image => image.Id == firstId).IsPrimary.Should().BeTrue();
    }

    private static MultipartFormDataContent CreateInvalidUploadContent(
        string scenario)
    {
        var content = new MultipartFormDataContent();

        if (scenario == "missing")
        {
            content.Add(new StringContent("image requested"), "metadata");
            return content;
        }

        byte[] bytes = scenario switch
        {
            "empty" => [],
            "oversized" => new byte[MaximumListingImageBytes + 1],
            _ => [0xFF, 0xD8, 0xFF, 0xE0]
        };
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            scenario == "mime" ? "text/plain" : "image/jpeg");
        string fileName = scenario == "extension" ? "image.txt" : "image.jpg";
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private async Task<HttpResponseMessage> SendImageOperationAsync(
        string operation,
        Guid listingId,
        Guid imageId)
    {
        if (operation == "upload")
        {
            using MultipartFormDataContent content = CreateImageUploadContent();
            return await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                content);
        }

        return operation switch
        {
            "delete" => await _httpClient.DeleteAsync(
                $"/api/listings/{listingId}/images/{imageId}"),
            "primary" => await _httpClient.PutAsync(
                $"/api/listings/{listingId}/images/{imageId}/primary",
                new StringContent(string.Empty)),
            "reorder" => await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}/images/order",
                new { imageIds = new[] { imageId } }),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    private static string GetImageOperationPath(
        string operation,
        Guid listingId,
        Guid imageId)
    {
        return operation switch
        {
            "upload" => $"/api/listings/{listingId}/images",
            "delete" => $"/api/listings/{listingId}/images/{imageId}",
            "primary" => $"/api/listings/{listingId}/images/{imageId}/primary",
            "reorder" => $"/api/listings/{listingId}/images/order",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }
}
