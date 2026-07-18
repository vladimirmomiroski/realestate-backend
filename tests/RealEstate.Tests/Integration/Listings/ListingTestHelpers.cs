using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using System.Net.Http.Json;
using System.Text.Json;
using RealEstate.Domain.Entities;

namespace RealEstate.Tests.Integration.Listings;

internal static class ListingTestHelpers
{
    public static async Task<Guid> CreateListingAsync(
     HttpClient httpClient,
     decimal price = 99000m,
     string currency = "EUR",
     decimal areaSquareMeters = 58m,
     decimal? rooms = 2m)
    {
        (Guid listingId, _) = await CreateListingWithOwnerAsync(
            httpClient,
            price,
            currency,
            areaSquareMeters,
            rooms);

        return listingId;
    }

    public static async Task<(Guid ListingId, AuthenticatedTestUser Owner)>
        CreateListingWithOwnerAsync(
            HttpClient httpClient,
            decimal price = 99000m,
            string currency = "EUR",
            decimal areaSquareMeters = 58m,
            decimal? rooms = 2m)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(httpClient);

        Guid listingId = await CreateListingAsAsync(
            httpClient,
            owner,
            price: price,
            currency: currency,
            areaSquareMeters: areaSquareMeters,
            rooms: rooms);

        return (listingId, owner);
    }

    public static async Task<Guid> CreateListingAsAsync(
        HttpClient httpClient,
        AuthenticatedTestUser user,
        Guid? agencyId = null,
        decimal price = 99000m,
        string currency = "EUR",
        decimal areaSquareMeters = 58m,
        decimal? rooms = 2m)
    {
        httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            object request = CreateValidListingRequest(
                price: price,
                agencyId: agencyId,
                currency: currency,
                areaSquareMeters: areaSquareMeters,
                rooms: rooms);

            return await PostListingAndReturnIdAsync(
                httpClient,
                request);
        }
        finally
        {
            httpClient.ClearAuthorization();
        }
    }

    public static object CreateValidListingRequest(
        decimal price = 99000,
        Guid? agencyId = null,
        string currency = "EUR",
        decimal areaSquareMeters = 58m,
        decimal? rooms = 2m,
        decimal? latitude = 41.9981m,
        decimal? longitude = 21.4254m)
    {
        return new
        {
            listingType = "Sale",
            propertyType = "Apartment",
            agencyId,
            price,
            currency,
            areaSquareMeters,
            rooms,
            bathrooms = 1,
            apartmentDetails = new
            {
                apartmentType = "Standard",
                floor = 4,
                totalFloors = 8,
                hasElevator = true
            },
            houseDetails = (object?)null,
            balconyCount = 2,
            parkingSpaces = 1,
            hasBasement = true,
            isExchangePossible = false,
            heatingType = "Central",
            furnishingStatus = "Furnished",
            condition = "Good",
            orientation = "SouthEast",
            yearRenovated = 2022,
            yearBuilt = 2015,
            latitude,
            longitude,
            translations = new[]
            {
                new
                {
                    languageCode = "en",
                    title = "Integration test apartment",
                    description = "Test listing created from integration tests.",
                    addressLine = "Center",
                    city = "Skopje",
                    municipality = "Centar",
                    neighborhood = "Center"
                },
                new
                {
                    languageCode = "mk",
                    title = "Интеграциски тест стан",
                    description = "Тест оглас креиран од integration tests.",
                    addressLine = "Центар",
                    city = "Скопје",
                    municipality = "Центар",
                    neighborhood = "Центар"
                }
            }
        };
    }

    public static object CreateValidHouseListingRequest(
        decimal price = 150000)
    {
        return new
        {
            listingType = "Sale",
            propertyType = "House",
            price,
            currency = "EUR",
            areaSquareMeters = 120,
            rooms = 4,
            bathrooms = 2,
            yearBuilt = 2005,
            yearRenovated = 2020,
            balconyCount = 1,
            parkingSpaces = 2,
            hasBasement = true,
            isExchangePossible = false,
            heatingType = "Gas",
            furnishingStatus = "SemiFurnished",
            condition = "Good",
            orientation = "South",
            latitude = 41.9981m,
            longitude = 21.4254m,
            apartmentDetails = (object?)null,
            houseDetails = new
            {
                houseType = "Detached",
                numberOfFloors = 2,
                yardAreaSquareMeters = 350
            },
            translations = new[]
            {
                new
                {
                    languageCode = "en",
                    title = "Integration test house",
                    description = "Integration test house description",
                    addressLine = "Test house address",
                    city = "Skopje",
                    municipality = "Centar",
                    neighborhood = "Center"
                },
                new
                {
                    languageCode = "mk",
                    title = "Интеграциска тест куќа",
                    description = "Интеграциски тест опис за куќа",
                    addressLine = "Тест адреса за куќа",
                    city = "Скопје",
                    municipality = "Центар",
                    neighborhood = "Центар"
                }
            }
        };
    }

    public static async Task SetListingStatusAsync(
        CustomWebApplicationFactory factory,
        Guid listingId,
        ListingStatus status)
    {
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Listings"
             SET "Status" = {status.ToString()}
             WHERE "Id" = {listingId}
             """);
    }

    public static async Task SetListingStatusAndCreatedAtUtcAsync(
        CustomWebApplicationFactory factory,
        Guid listingId,
        ListingStatus status,
        DateTime createdAtUtc)
    {
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
         UPDATE "Listings"
         SET "Status" = {status.ToString()},
             "CreatedAtUtc" = {createdAtUtc}
         WHERE "Id" = {listingId}
         """);
    }

    public static async Task UpdateComparableFieldsAsync(
    CustomWebApplicationFactory factory,
    Guid listingId,
    ListingType? listingType = null,
    PropertyType? propertyType = null,
    string? currency = null,
    decimal? price = null,
    decimal? areaSquareMeters = null)
    {
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        Listing listing =
            await dbContext.Listings.SingleAsync(
                item => item.Id == listingId);

        if (listingType.HasValue)
        {
            listing.ListingType = listingType.Value;
        }

        if (propertyType.HasValue)
        {
            listing.PropertyType = propertyType.Value;
        }

        if (currency is not null)
        {
            listing.Currency = currency;
        }

        if (price.HasValue)
        {
            listing.Price = price.Value;
        }

        if (areaSquareMeters.HasValue)
        {
            listing.AreaSquareMeters =
                areaSquareMeters.Value;
        }

        await dbContext.SaveChangesAsync();
    }

    public static async Task ReplaceListingTranslationsAsync(
    CustomWebApplicationFactory factory,
    Guid listingId,
    params ListingTranslation[] translations)
    {
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();

        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Set<ListingTranslation>()
            .Where(translation =>
                translation.ListingId == listingId)
            .ExecuteDeleteAsync();

        foreach (ListingTranslation translation in translations)
        {
            translation.ListingId = listingId;
        }

        if (translations.Length > 0)
        {
            await dbContext.Set<ListingTranslation>()
                .AddRangeAsync(translations);

            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task<Guid> PostListingAndReturnIdAsync(
        HttpClient httpClient,
        object request)
    {
        HttpResponseMessage response =
            await httpClient.PostAsJsonAsync(
                "/api/listings",
                request);

        response.EnsureSuccessStatusCode();

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        return json.GetProperty("id").GetGuid();
    }
}
