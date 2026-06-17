using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

internal static class ListingTestHelpers
{
    public static async Task<Guid> CreateListingAsync(HttpClient httpClient)
    {
        var request = CreateValidListingRequest();

        var response = await httpClient.PostAsJsonAsync("/api/listings", request);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return json.GetProperty("id").GetGuid();
    }

    public static object CreateValidListingRequest(decimal price = 99000)
    {
        return new
        {
            listingType = "Sale",
            propertyType = "Apartment",
            price,
            currency = "EUR",
            areaSquareMeters = 58,
            rooms = 2,
            bathrooms = 1,
            floor = 2,
            totalFloors = 5,
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
}
