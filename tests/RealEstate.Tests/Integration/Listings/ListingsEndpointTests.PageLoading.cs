using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task GetListings_PageLoading_PreservesSelectedPageAggregatesAndOrder()
    {
        // Arrange
        const string currency = "QSL";
        const string searchPhrase = "QS1 aggregate hydration";

        var listingWithOwner =
            await ListingTestHelpers.CreateListingWithOwnerAsync(
                _httpClient,
                currency: currency);

        Guid olderListingId = listingWithOwner.ListingId;

        Guid newerListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                listingWithOwner.Owner,
                currency: currency);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            olderListingId,
            CreateTranslation(
                "en",
                $"Older {searchPhrase}"),
            CreateTranslation(
                "mk",
                "Постар оглас"));

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            newerListingId,
            CreateTranslation(
                "en",
                $"Newer {searchPhrase}"),
            CreateTranslation(
                "mk",
                "Понов оглас"));

        DateTime olderTimestamp = new(
            2036,
            1,
            1,
            10,
            0,
            0,
            DateTimeKind.Utc);

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            olderListingId,
            ListingStatus.Active,
            olderTimestamp);

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            newerListingId,
            ListingStatus.Active,
            olderTimestamp.AddHours(1));

        await AddQueryShapeImagesAsync(
            CreateQueryShapeImage(
                newerListingId,
                "newer-second.webp",
                "/uploads/qs1/newer-second.webp",
                sortOrder: 2,
                isPrimary: false),
            CreateQueryShapeImage(
                olderListingId,
                "older.webp",
                "/uploads/qs1/older.webp",
                sortOrder: 0,
                isPrimary: true),
            CreateQueryShapeImage(
                newerListingId,
                "newer-primary.webp",
                "/uploads/qs1/newer-primary.webp",
                sortOrder: 0,
                isPrimary: true),
            CreateQueryShapeImage(
                newerListingId,
                "newer-first.webp",
                "/uploads/qs1/newer-first.webp",
                sortOrder: 1,
                isPrimary: false));

        // Act
        HttpResponseMessage response = await _httpClient.GetAsync(
            "/api/listings" +
            "?lang=en" +
            $"&currency={currency}" +
            $"&q={Uri.EscapeDataString(searchPhrase)}" +
            "&sort=newest" +
            "&page=1" +
            "&pageSize=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("totalCount").GetInt32().Should().Be(2);

        JsonElement[] items = json.GetProperty("items")
            .EnumerateArray()
            .ToArray();

        items.Should().ContainSingle();

        JsonElement item = items[0];

        item.GetProperty("id").GetGuid().Should().Be(newerListingId);
        item.GetProperty("title").GetString()
            .Should().Be($"Newer {searchPhrase}");
        item.GetProperty("languageCode").GetString().Should().Be("en");

        item.GetProperty("apartmentDetails").ValueKind
            .Should().Be(JsonValueKind.Object);
        item.GetProperty("houseDetails").ValueKind
            .Should().Be(JsonValueKind.Null);

        item.GetProperty("primaryImageUrl").GetString()
            .Should().Be("/uploads/qs1/newer-primary.webp");

        JsonElement[] images = item.GetProperty("images")
            .EnumerateArray()
            .ToArray();

        images.Select(image => image.GetProperty("sortOrder").GetInt32())
            .Should().Equal(0, 1, 2);

        images.Select(image => image.GetProperty("url").GetString())
            .Should().Equal(
                "/uploads/qs1/newer-primary.webp",
                "/uploads/qs1/newer-first.webp",
                "/uploads/qs1/newer-second.webp");

        images.Select(image => image.GetProperty("url").GetString())
            .Should().NotContain("/uploads/qs1/older.webp");
    }

    private async Task AddQueryShapeImagesAsync(
        params ListingImage[] images)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Set<ListingImage>().AddRangeAsync(images);
        await dbContext.SaveChangesAsync();
    }

    private static ListingImage CreateQueryShapeImage(
        Guid listingId,
        string fileName,
        string url,
        int sortOrder,
        bool isPrimary)
    {
        return new ListingImage
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            OriginalFileName = fileName,
            StoredFileName = fileName,
            ContentType = "image/webp",
            SizeBytes = 1_024,
            Url = url,
            SortOrder = sortOrder,
            IsPrimary = isPrimary
        };
    }
}
