using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task ComparableRootIsolation_CommercialSourceReturnsOnlyCommercialInMetricOrder()
    {
        string currency = CreateUniqueCurrency();
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        DateTime baseTime = new(2039, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        Guid sourceId = await CreateActiveComparableRootAsync(
            owner,
            PropertyType.Commercial,
            currency,
            price: 100_000m,
            areaSquareMeters: 100m,
            createdAtUtc: baseTime,
            languageCode: "de",
            city: "Skopje",
            title: "Commercial Office source",
            commercialType: CommercialType.Office);

        Guid differentSubtypeId = await CreateActiveComparableRootAsync(
            owner,
            PropertyType.Commercial,
            currency,
            price: 105_000m,
            areaSquareMeters: 105m,
            createdAtUtc: baseTime.AddHours(1),
            languageCode: "de",
            city: "sKoPjE",
            title: "Commercial Shop closer candidate",
            commercialType: CommercialType.Shop);

        Guid sameSubtypeId = await CreateActiveComparableRootAsync(
            owner,
            PropertyType.Commercial,
            currency,
            price: 115_000m,
            areaSquareMeters: 115m,
            createdAtUtc: baseTime.AddHours(2),
            languageCode: "de",
            city: "SKOPJE",
            title: "Commercial Office farther candidate",
            commercialType: CommercialType.Office);

        Guid apartmentId = await CreatePerfectCrossRootComparableAsync(
            owner,
            PropertyType.Apartment,
            currency,
            baseTime.AddDays(1));
        Guid houseId = await CreatePerfectCrossRootComparableAsync(
            owner,
            PropertyType.House,
            currency,
            baseTime.AddDays(2));
        Guid landId = await CreatePerfectCrossRootComparableAsync(
            owner,
            PropertyType.Land,
            currency,
            baseTime.AddDays(3));

        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings/{sourceId}/comparables?lang=fr&limit=12");
        JsonElement results = await ReadComparableResultsAsync(response);
        Guid[] returnedIds = results
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();

        returnedIds.Should().Equal(differentSubtypeId, sameSubtypeId);
        returnedIds.Should().NotContain([sourceId, apartmentId, houseId, landId]);
        AssertCommercialComparable(
            results[0],
            differentSubtypeId,
            CommercialType.Shop,
            "Commercial Shop closer candidate",
            "sKoPjE");
        AssertCommercialComparable(
            results[1],
            sameSubtypeId,
            CommercialType.Office,
            "Commercial Office farther candidate",
            "SKOPJE");
    }

    [Fact]
    public async Task ComparableRootIsolation_LandSourceReturnsOnlyLandInMetricOrder()
    {
        string currency = CreateUniqueCurrency();
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        DateTime baseTime = new(2039, 2, 1, 10, 0, 0, DateTimeKind.Utc);

        Guid sourceId = await CreateActiveComparableRootAsync(
            owner,
            PropertyType.Land,
            currency,
            price: 100_000m,
            areaSquareMeters: 100m,
            createdAtUtc: baseTime,
            languageCode: "de",
            city: "Skopje",
            title: "Land BuildingPlot source",
            landType: LandType.BuildingPlot);

        Guid differentSubtypeId = await CreateActiveComparableRootAsync(
            owner,
            PropertyType.Land,
            currency,
            price: 105_000m,
            areaSquareMeters: 105m,
            createdAtUtc: baseTime.AddHours(1),
            languageCode: "de",
            city: "sKoPjE",
            title: "Land AgriculturalLand closer candidate",
            landType: LandType.AgriculturalLand);

        Guid sameSubtypeId = await CreateActiveComparableRootAsync(
            owner,
            PropertyType.Land,
            currency,
            price: 115_000m,
            areaSquareMeters: 115m,
            createdAtUtc: baseTime.AddHours(2),
            languageCode: "de",
            city: "SKOPJE",
            title: "Land BuildingPlot farther candidate",
            landType: LandType.BuildingPlot);

        Guid apartmentId = await CreatePerfectCrossRootComparableAsync(
            owner,
            PropertyType.Apartment,
            currency,
            baseTime.AddDays(1));
        Guid houseId = await CreatePerfectCrossRootComparableAsync(
            owner,
            PropertyType.House,
            currency,
            baseTime.AddDays(2));
        Guid commercialId = await CreatePerfectCrossRootComparableAsync(
            owner,
            PropertyType.Commercial,
            currency,
            baseTime.AddDays(3));

        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings/{sourceId}/comparables?lang=fr&limit=12");
        JsonElement results = await ReadComparableResultsAsync(response);
        Guid[] returnedIds = results
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();

        returnedIds.Should().Equal(differentSubtypeId, sameSubtypeId);
        returnedIds.Should().NotContain(
            [sourceId, apartmentId, houseId, commercialId]);
        AssertLandComparable(
            results[0],
            differentSubtypeId,
            LandType.AgriculturalLand,
            "Land AgriculturalLand closer candidate",
            "sKoPjE");
        AssertLandComparable(
            results[1],
            sameSubtypeId,
            LandType.BuildingPlot,
            "Land BuildingPlot farther candidate",
            "SKOPJE");
    }

    private async Task<Guid> CreateActiveComparableRootAsync(
        AuthenticatedTestUser owner,
        PropertyType propertyType,
        string currency,
        decimal price,
        decimal areaSquareMeters,
        DateTime createdAtUtc,
        string languageCode,
        string city,
        string title,
        CommercialType commercialType = CommercialType.Office,
        LandType landType = LandType.BuildingPlot)
    {
        Guid listingId = await CreateRootDiscoveryListingAsync(
            owner,
            propertyType,
            currency,
            commercialType: commercialType,
            landType: landType);

        await ListingTestHelpers.UpdateComparableFieldsAsync(
            _factory,
            listingId,
            price: price,
            areaSquareMeters: areaSquareMeters);
        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId,
            CreateComparableTranslation(
                languageCode,
                city,
                municipality: "Centar",
                neighborhood: "Center",
                title: title));
        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            listingId,
            ListingStatus.Active,
            createdAtUtc);

        return listingId;
    }

    private async Task<Guid> CreatePerfectCrossRootComparableAsync(
        AuthenticatedTestUser owner,
        PropertyType propertyType,
        string currency,
        DateTime createdAtUtc)
    {
        return await CreateActiveComparableRootAsync(
            owner,
            propertyType,
            currency,
            price: 100_000m,
            areaSquareMeters: 100m,
            createdAtUtc,
            languageCode: "de",
            city: "SkOpJe",
            title: $"Perfect {propertyType} cross-root candidate");
    }

    private static async Task<JsonElement> ReadComparableResultsAsync(
        HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement results =
            await response.Content.ReadFromJsonAsync<JsonElement>();
        results.ValueKind.Should().Be(JsonValueKind.Array);
        return results;
    }

    private static void AssertCommercialComparable(
        JsonElement result,
        Guid expectedId,
        CommercialType expectedSubtype,
        string expectedTitle,
        string expectedCity)
    {
        result.GetProperty("id").GetGuid().Should().Be(expectedId);
        result.GetProperty("propertyType").GetString().Should().Be("Commercial");
        result.GetProperty("languageCode").GetString().Should().Be("de");
        result.GetProperty("city").GetString().Should().Be(expectedCity);
        result.GetProperty("title").GetString().Should().Be(expectedTitle);
        result.GetProperty("apartmentDetails").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("houseDetails").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("landDetails").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("commercialDetails")
            .GetProperty("commercialType")
            .GetString()
            .Should().Be(expectedSubtype.ToString());
    }

    private static void AssertLandComparable(
        JsonElement result,
        Guid expectedId,
        LandType expectedSubtype,
        string expectedTitle,
        string expectedCity)
    {
        result.GetProperty("id").GetGuid().Should().Be(expectedId);
        result.GetProperty("propertyType").GetString().Should().Be("Land");
        result.GetProperty("languageCode").GetString().Should().Be("de");
        result.GetProperty("city").GetString().Should().Be(expectedCity);
        result.GetProperty("title").GetString().Should().Be(expectedTitle);
        result.GetProperty("apartmentDetails").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("houseDetails").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("commercialDetails").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("landDetails")
            .GetProperty("landType")
            .GetString()
            .Should().Be(expectedSubtype.ToString());
    }
}
