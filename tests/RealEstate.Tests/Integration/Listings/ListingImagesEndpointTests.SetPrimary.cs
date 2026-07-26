using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingImagesEndpointTests
{
    [Fact]
    public async Task SetPrimaryListingImage_WhenAlreadyPrimary_ReturnsExistingSuccess()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        var firstImage = await UploadImageAsync(
            listingId,
            owner,
            "already-primary.png");

        var secondImage = await UploadImageAsync(
            listingId,
            owner,
            "secondary.png");

        Guid firstImageId = firstImage.GetProperty("id").GetGuid();
        Guid secondImageId = secondImage.GetProperty("id").GetGuid();

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/images/{firstImageId}/primary",
                null);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().Be(firstImageId);
            json.GetProperty("isPrimary").GetBoolean().Should().BeTrue();
            json.GetProperty("sortOrder").GetInt32().Should().Be(0);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        await using AsyncServiceScope assertionScope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext assertionDbContext =
            assertionScope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        List<ListingImage> savedImages =
            await assertionDbContext.Set<ListingImage>()
                .AsNoTracking()
                .Where(image => image.ListingId == listingId)
                .OrderBy(image => image.SortOrder)
                .ToListAsync();

        savedImages.Should().HaveCount(2);
        savedImages.Select(image => image.Id)
            .Should().Equal(firstImageId, secondImageId);
        savedImages.Select(image => image.SortOrder)
            .Should().Equal(0, 1);
        savedImages.Count(image => image.IsPrimary).Should().Be(1);
        savedImages.Single(image => image.Id == firstImageId)
            .IsPrimary.Should().BeTrue();
        savedImages.Single(image => image.Id == secondImageId)
            .IsPrimary.Should().BeFalse();
    }

    [Fact]
    public async Task SetPrimaryListingImage_WithExistingImage_ReturnsOk()
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
