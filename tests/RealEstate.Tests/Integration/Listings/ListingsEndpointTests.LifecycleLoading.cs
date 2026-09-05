using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Theory]
    [InlineData("publish", ListingStatus.Draft, ListingStatus.Active, false)]
    [InlineData("unpublish", ListingStatus.Active, ListingStatus.Draft, false)]
    [InlineData("archive", ListingStatus.Active, ListingStatus.Archived, true)]
    [Trait("Name", "Chapter14SharedReadContract")]
    public async Task LifecycleWriter_ReturnsFullyLoadedPersistedAggregate(
        string operation,
        ListingStatus startingStatus,
        ListingStatus expectedStatus,
        bool useHouse)
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await CreateLifecycleListingAsync(useHouse);

        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await SetListingStatusAsync(listingId, startingStatus);

        if (operation == "publish")
        {
            await ListingTestHelpers.PrepareStrongLocationPublishableDraftAsync(
                _factory,
                listingId);
        }

        await ListingTestHelpers.SeedDormantSubtypeDetailsAsync(
            _factory,
            listingId,
            CommercialType.Shop,
            LandType.BuildingPlot);

        Guid imageId = await AddLifecycleImageAsync(listingId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response =
                await _httpClient.PutAsync(
                    $"/api/listings/{listingId}/{operation}?lang=en",
                    null);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("status").GetString()
                .Should().Be(expectedStatus.ToString());
            json.GetProperty("languageCode").GetString()
                .Should().Be("en");
            json.GetProperty("description").GetString()
                .Should().Be(useHouse
                    ? "Integration test house description"
                    : "Test listing created from integration tests.");

            JsonElement image = json.GetProperty("images")
                .EnumerateArray()
                .Should()
                .ContainSingle()
                .Subject;

            image.GetProperty("id").GetGuid().Should().Be(imageId);
            image.GetProperty("url").GetString()
                .Should().Be($"/uploads/listings/{listingId}/lifecycle.jpg");
            image.GetProperty("isPrimary").GetBoolean().Should().BeTrue();
            json.GetProperty("primaryImageUrl").GetString()
                .Should().Be($"/uploads/listings/{listingId}/lifecycle.jpg");
            json.GetProperty("commercialDetails")
                .GetProperty("commercialType").GetString().Should().Be("Shop");
            json.GetProperty("landDetails")
                .GetProperty("landType").GetString().Should().Be("BuildingPlot");

            if (useHouse)
            {
                json.GetProperty("apartmentDetails").ValueKind
                    .Should().Be(JsonValueKind.Null);
                json.GetProperty("houseDetails")
                    .GetProperty("numberOfFloors")
                    .GetInt32()
                    .Should().Be(2);
            }
            else
            {
                json.GetProperty("houseDetails").ValueKind
                    .Should().Be(JsonValueKind.Null);
                json.GetProperty("apartmentDetails")
                    .GetProperty("floor")
                    .GetInt32()
                    .Should().Be(4);
            }
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task LifecycleWriter_WhenAuthorizationFails_PersistsNoStatusMutation()
    {
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        AuthenticatedTestUser otherUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(otherUser.UserId, UserStatus.Active);
        await SetListingStatusAsync(listingId, ListingStatus.Active);

        _httpClient.AuthorizeAs(otherUser.AccessToken);

        try
        {
            HttpResponseMessage response =
                await _httpClient.PutAsync(
                    $"/api/listings/{listingId}/unpublish",
                    null);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            ListingStatus databaseStatus =
                await GetListingStatusAsync(listingId);

            databaseStatus.Should().Be(ListingStatus.Active);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<(Guid ListingId, AuthenticatedTestUser Owner)>
        CreateLifecycleListingAsync(bool useHouse)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            object request = useHouse
                ? ListingTestHelpers.CreateValidHouseListingRequest()
                : ListingTestHelpers.CreateValidListingRequest();

            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync(
                    "/api/listings",
                    request);

            response.EnsureSuccessStatusCode();

            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();

            return (json.GetProperty("id").GetGuid(), owner);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<Guid> AddLifecycleImageAsync(Guid listingId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        var image = new ListingImage
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            OriginalFileName = "lifecycle.jpg",
            StoredFileName = "lifecycle.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 512,
            Url = $"/uploads/listings/{listingId}/lifecycle.jpg",
            SortOrder = 0,
            IsPrimary = true
        };

        dbContext.Set<ListingImage>().Add(image);

        await dbContext.SaveChangesAsync();

        return image.Id;
    }
}
