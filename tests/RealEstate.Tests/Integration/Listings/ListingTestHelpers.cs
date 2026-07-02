using System.Net.Http.Json;
using System.Text.Json;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

internal static class ListingTestHelpers
{
    public static async Task<Guid> CreateListingAsync(HttpClient httpClient)
    {
        (Guid listingId, _) = await CreateListingWithOwnerAsync(httpClient);

        return listingId;
    }

    public static async Task<(Guid ListingId, AuthenticatedTestUser Owner)> CreateListingWithOwnerAsync(
        HttpClient httpClient)
    {
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(httpClient);

        Guid listingId = await CreateListingAsAsync(httpClient, owner);

        return (listingId, owner);
    }

    public static async Task<Guid> CreateListingAsAsync(
        HttpClient httpClient,
        AuthenticatedTestUser user,
        Guid? agencyId = null)
    {
        httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = CreateValidListingRequest(agencyId: agencyId);

            return await PostListingAndReturnIdAsync(httpClient, request);
        }
        finally
        {
            httpClient.ClearAuthorization();
        }
    }

    public static object CreateValidListingRequest(decimal price = 99000, Guid? agencyId = null)
    {
        return new
        {
            listingType = "Sale",
            propertyType = "Apartment",
            agencyId,
            price,
            currency = "EUR",
            areaSquareMeters = 58,
            rooms = 2,
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
            latitude = 41.9981,
            longitude = 21.4254,
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

    public static object CreateValidHouseListingRequest(decimal price = 150000)
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

    private static async Task<Guid> PostListingAndReturnIdAsync(
        HttpClient httpClient,
        object request)
    {
        HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "/api/listings",
            request);

        response.EnsureSuccessStatusCode();

        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return json.GetProperty("id").GetGuid();
    }
}
