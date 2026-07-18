using FluentAssertions;
using RealEstate.Application.Listings.Queries.GetComparableListings;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RealEstate.Tests.Integration.Auth;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Agencies;
using System.Text.Json.Nodes;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{

    private async Task<Guid> CreateActiveComparableFromRequestAsync(
    AuthenticatedTestUser owner,
    object request,
    string title,
    DateTime createdAtUtc)
    {
        Guid listingId;

        _httpClient.AuthorizeAs(
            owner.AccessToken);

        try
        {
            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync(
                    "/api/listings",
                    request);

            response.StatusCode.Should().Be(
                HttpStatusCode.Created);

            JsonElement json =
                await response.Content
                    .ReadFromJsonAsync<JsonElement>();

            listingId =
                json.GetProperty("id").GetGuid();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId,
            CreateComparableTranslation(
                "en",
                "Skopje",
                municipality: "Centar",
                neighborhood: "Center",
                title: title));

        await ListingTestHelpers
            .SetListingStatusAndCreatedAtUtcAsync(
                _factory,
                listingId,
                ListingStatus.Active,
                createdAtUtc);

        return listingId;
    }

    private async Task<Guid> CreateComparableAgencyWithOwnerAsync(
        Guid ownerUserId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        Agency agency =
            AgencyTestHelpers.CreateAgency();

        agency.AddMember(
            ownerUserId,
            AgencyMemberRole.Owner);

        dbContext.Agencies.Add(
            agency);

        await dbContext.SaveChangesAsync();

        return agency.Id;
    }

    private static ListingTranslation CreateComparableTranslation(
    string languageCode,
    string? city,
    string? municipality = "Centar",
    string? neighborhood = "Center",
    string? title = null,
    Guid? id = null)
    {
        return new ListingTranslation
        {
            Id = id ?? Guid.NewGuid(),
            LanguageCode = languageCode,
            Title = title ?? $"Comparable {languageCode}",
            Description = "Comparable test description",
            AddressLine = "Comparable address",
            City = city,
            Municipality = municipality,
            Neighborhood = neighborhood
        };
    }

    private static string CreateUniqueCurrency()
    {
        byte[] bytes =
            Guid.NewGuid().ToByteArray();

        return new string(
            bytes
                .Take(3)
                .Select(value =>
                    (char)('A' + value % 26))
                .ToArray());
    }

    private async Task<Guid> CreateActiveComparableAsync(
    AuthenticatedTestUser owner,
    string currency,
    decimal price,
    decimal areaSquareMeters,
    string city = "Skopje",
    string municipality = "Centar",
    string neighborhood = "Center",
    DateTime? createdAtUtc = null)
    {
        Guid listingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                owner,
                price: price,
                currency: currency,
                areaSquareMeters: areaSquareMeters);

        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId,
            CreateComparableTranslation(
                "en",
                city,
                municipality,
                neighborhood));

        if (createdAtUtc.HasValue)
        {
            await ListingTestHelpers
                .SetListingStatusAndCreatedAtUtcAsync(
                    _factory,
                    listingId,
                    ListingStatus.Active,
                    createdAtUtc.Value);
        }
        else
        {
            await ListingTestHelpers.SetListingStatusAsync(
                _factory,
                listingId,
                ListingStatus.Active);
        }

        return listingId;
    }

    private static async Task<Guid[]> ReadComparableIdsAsync(
        HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(
            HttpStatusCode.OK);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.ValueKind.Should().Be(
            JsonValueKind.Array);

        return json.EnumerateArray()
            .Select(item =>
                item.GetProperty("id").GetGuid())
            .ToArray();
    }

    private static async Task<JsonElement> ReadSingleComparableAsync(
    HttpResponseMessage response,
    Guid expectedListingId)
    {
        response.StatusCode.Should().Be(
            HttpStatusCode.OK);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.ValueKind.Should().Be(
            JsonValueKind.Array);

        json.GetArrayLength().Should().Be(1);

        JsonElement item = json[0];

        item.GetProperty("id")
            .GetGuid()
            .Should()
            .Be(expectedListingId);

        return item;
    }

    private static void AssertSelectedTranslation(
        JsonElement item,
        string languageCode,
        string title,
        string city,
        string municipality,
        string neighborhood)
    {
        item.GetProperty("languageCode")
            .GetString()
            .Should()
            .Be(languageCode);

        item.GetProperty("title")
            .GetString()
            .Should()
            .Be(title);

        item.GetProperty("city")
            .GetString()
            .Should()
            .Be(city);

        item.GetProperty("municipality")
            .GetString()
            .Should()
            .Be(municipality);

        item.GetProperty("neighborhood")
            .GetString()
            .Should()
            .Be(neighborhood);
    }
}
