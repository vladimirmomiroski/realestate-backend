using System.Net;
using System.Text.Json;
using FluentAssertions;
using RealEstate.Application.Common;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task CommercialAndLandSubtypeDiscovery_ExactScalarFiltersPreserveRootIsolationAndCompositionalSemantics()
    {
        const string currency = "CLD";
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid commercialOfficeId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Commercial,
            currency,
            commercialType: CommercialType.Office);
        Guid commercialUnknownId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Commercial,
            currency,
            commercialType: CommercialType.Unknown);
        Guid commercialShopId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Commercial,
            currency,
            commercialType: CommercialType.Shop);
        Guid commercialOtherId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Commercial,
            currency,
            commercialType: CommercialType.Other);
        Guid landBuildingPlotId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Land,
            currency,
            landType: LandType.BuildingPlot);
        Guid landAgriculturalId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Land,
            currency,
            landType: LandType.AgriculturalLand);
        Guid landUnknownId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Land,
            currency,
            landType: LandType.Unknown);
        Guid landOtherId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Land,
            currency,
            landType: LandType.Other);
        Guid malformedApartmentId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Apartment,
            currency);
        Guid draftCommercialOfficeId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Commercial,
            currency,
            commercialType: CommercialType.Office);

        foreach (Guid listingId in new[]
                 {
                     commercialOfficeId,
                     commercialUnknownId,
                     commercialShopId,
                     commercialOtherId,
                     landBuildingPlotId,
                     landAgriculturalId,
                     landUnknownId,
                     landOtherId,
                     malformedApartmentId
                 })
        {
            await ListingTestHelpers.SetListingStatusAsync(
                _factory,
                listingId,
                ListingStatus.Active);
        }

        await ListingTestHelpers.SeedDormantSubtypeDetailsAsync(
            _factory,
            malformedApartmentId,
            CommercialType.Office,
            LandType.BuildingPlot);

        JsonElement commercialSymbolic = await ReadSubtypePageAsync(
            currency,
            "commercialType=Office");
        JsonElement commercialNumeric = await ReadSubtypePageAsync(
            currency,
            $"commercialType={(int)CommercialType.Office}");
        JsonElement commercialExplicitRoot = await ReadSubtypePageAsync(
            currency,
            "propertyType=Commercial&commercialType=Office");

        AssertExactSubtypePage(commercialSymbolic, commercialOfficeId);
        ReadItems(commercialNumeric).Select(ReadId).Should()
            .Equal(commercialOfficeId);
        ReadItems(commercialExplicitRoot).Select(ReadId).Should()
            .Equal(commercialOfficeId);
        ReadItems(commercialSymbolic).Select(ReadId).Should().NotContain(
            [malformedApartmentId, draftCommercialOfficeId]);

        AssertExactSubtypePage(
            await ReadSubtypePageAsync(currency, "commercialType=Unknown"),
            commercialUnknownId);
        AssertExactSubtypePage(
            await ReadSubtypePageAsync(currency, "commercialType=Shop"),
            commercialShopId);
        AssertExactSubtypePage(
            await ReadSubtypePageAsync(currency, "commercialType=Other"),
            commercialOtherId);
        JsonElement landSymbolic = await ReadSubtypePageAsync(
            currency,
            "landType=BuildingPlot");
        JsonElement landExplicitRoot = await ReadSubtypePageAsync(
            currency,
            "propertyType=Land&landType=BuildingPlot");
        AssertExactSubtypePage(landSymbolic, landBuildingPlotId);
        ReadItems(landExplicitRoot).Select(ReadId).Should()
            .Equal(landBuildingPlotId);
        AssertExactSubtypePage(
            await ReadSubtypePageAsync(currency, "landType=Unknown"),
            landUnknownId);
        AssertExactSubtypePage(
            await ReadSubtypePageAsync(currency, "landType=AgriculturalLand"),
            landAgriculturalId);
        AssertExactSubtypePage(
            await ReadSubtypePageAsync(currency, "landType=Other"),
            landOtherId);
        AssertExactSubtypePage(
            await ReadSubtypePageAsync(currency, "landType=0"),
            landUnknownId);

        AssertEmptySubtypePage(
            await ReadSubtypePageAsync(
                currency,
                "propertyType=Land&commercialType=Office"));
        AssertEmptySubtypePage(
            await ReadSubtypePageAsync(
                currency,
                "commercialType=Office&landType=BuildingPlot"));

        JsonElement absent = await ReadSubtypePageAsync(currency, string.Empty);
        JsonElement emptyValues = await ReadSubtypePageAsync(
            currency,
            "commercialType=&landType=");
        ReadItems(emptyValues).Select(ReadId).Should()
            .Equal(ReadItems(absent).Select(ReadId));
        emptyValues.GetProperty("totalCount").GetInt32().Should()
            .Be(absent.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task CommercialAndLandSubtypeDiscovery_FilteringPrecedesCountAndPagingWithStableNewestOrder()
    {
        const string currency = "CLP";
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        DateTime baseTime = new(2039, 2, 1, 10, 0, 0, DateTimeKind.Utc);
        var officeIds = new List<Guid>();

        for (int index = 0; index < 3; index++)
        {
            Guid listingId = await CreateRootDiscoveryListingAsync(
                owner,
                PropertyType.Commercial,
                currency,
                commercialType: CommercialType.Office);
            await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
                _factory,
                listingId,
                ListingStatus.Active,
                baseTime.AddHours(index));
            officeIds.Add(listingId);
        }

        for (int index = 0; index < 2; index++)
        {
            Guid listingId = await CreateRootDiscoveryListingAsync(
                owner,
                PropertyType.Commercial,
                currency,
                commercialType: CommercialType.Shop);
            await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
                _factory,
                listingId,
                ListingStatus.Active,
                baseTime.AddDays(1).AddHours(index));
        }

        JsonElement firstPage = await ReadSubtypePageAsync(
            currency,
            "commercialType=Office&sort=newest&page=1&pageSize=2");
        JsonElement secondPage = await ReadSubtypePageAsync(
            currency,
            "commercialType=Office&sort=newest&page=2&pageSize=2");

        AssertPagingMetadata(
            firstPage,
            pageNumber: 1,
            pageSize: 2,
            totalCount: 3,
            totalPages: 2);
        AssertPagingMetadata(
            secondPage,
            pageNumber: 2,
            pageSize: 2,
            totalCount: 3,
            totalPages: 2);
        ReadItems(firstPage).Select(ReadId).Should()
            .Equal(officeIds[2], officeIds[1]);
        ReadItems(secondPage).Select(ReadId).Should().Equal(officeIds[0]);
    }

    [Theory]
    [InlineData("commercialType", "Warehouse")]
    [InlineData("landType", "999")]
    public async Task CommercialAndLandSubtypeDiscovery_InvalidHttpEnumReturnsAutomaticCanonicalModelState400(
        string field,
        string value)
    {
        string path = $"/api/listings?{field}={value}";

        HttpResponseMessage response = await _httpClient.GetAsync(path);

        JsonElement problem = await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            "/api/listings",
            validationKey: field);
        string binderMessage = problem.GetProperty("errors")
            .GetProperty(field)[0]
            .GetString()!;
        binderMessage.Should().Contain(value);
        binderMessage.Should().NotContain("must be a defined value");
    }

    private async Task<JsonElement> ReadSubtypePageAsync(
        string currency,
        string query)
    {
        string suffix = string.IsNullOrEmpty(query)
            ? string.Empty
            : $"&{query}";

        return await ReadRootDiscoveryPageAsync(
            "/api/listings" +
            $"?lang=en&currency={currency}" +
            suffix);
    }

    private static void AssertExactSubtypePage(
        JsonElement page,
        Guid expectedId)
    {
        page.GetProperty("totalCount").GetInt32().Should().Be(1);
        ReadItems(page).Select(ReadId).Should().Equal(expectedId);
    }

    private static void AssertEmptySubtypePage(JsonElement page)
    {
        page.GetProperty("totalCount").GetInt32().Should().Be(0);
        ReadItems(page).Should().BeEmpty();
    }
}
