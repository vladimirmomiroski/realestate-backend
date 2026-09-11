using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using RealEstate.Api.Controllers;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Application.Listings.Queries.GetListings;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Tests.Listings;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task RootTypeDiscovery_NameNumericAndNullFiltersPreserveExactRootSemantics()
    {
        const string currency = "RTA";
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        var expectedIds = new Dictionary<PropertyType, Guid>();

        foreach (PropertyType propertyType in Enum.GetValues<PropertyType>())
        {
            Guid listingId = await CreateRootDiscoveryListingAsync(
                owner,
                propertyType,
                currency);
            await ListingTestHelpers.SetListingStatusAsync(
                _factory,
                listingId,
                ListingStatus.Active);
            expectedIds.Add(propertyType, listingId);
        }

        foreach ((PropertyType propertyType, Guid listingId) in expectedIds)
        {
            foreach (string serializedValue in
                     new[] { propertyType.ToString(), ((int)propertyType).ToString() })
            {
                string query =
                    "/api/listings" +
                    "?lang=en" +
                    $"&currency={currency}" +
                    $"&propertyType={serializedValue}" +
                    "&page=1&pageSize=20";

                JsonElement page = await ReadRootDiscoveryPageAsync(query);

                AssertExactRootPage(page, listingId, propertyType);
            }
        }

        foreach (string query in new[]
                 {
                     $"/api/listings?lang=en&currency={currency}&page=1&pageSize=20",
                     $"/api/listings?lang=en&currency={currency}&propertyType=&page=1&pageSize=20"
                 })
        {
            JsonElement page = await ReadRootDiscoveryPageAsync(query);
            List<JsonElement> items = ReadItems(page);

            page.GetProperty("totalCount").GetInt32().Should().Be(4);
            items.Select(ReadId).Should().BeEquivalentTo(expectedIds.Values);
            items.Select(ReadPropertyType).Should().BeEquivalentTo(expectedIds.Keys);
        }
    }

    [Theory]
    [InlineData(PropertyType.Commercial, "RTB")]
    [InlineData(PropertyType.Land, "RTC")]
    public async Task RootTypeDiscovery_PublicVisibilityReturnsOnlyActiveListings(
        PropertyType propertyType,
        string currency)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid activeId = await CreateRootDiscoveryListingAsync(
            owner,
            propertyType,
            currency);
        Guid draftId = await CreateRootDiscoveryListingAsync(
            owner,
            propertyType,
            currency);
        Guid archivedId = await CreateRootDiscoveryListingAsync(
            owner,
            propertyType,
            currency);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            activeId,
            ListingStatus.Active);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            archivedId,
            ListingStatus.Archived);

        JsonElement page = await ReadRootDiscoveryPageAsync(
            "/api/listings" +
            "?lang=en" +
            $"&currency={currency}" +
            $"&propertyType={propertyType}" +
            "&page=1&pageSize=20");

        AssertExactRootPage(page, activeId, propertyType);
        ReadItems(page).Select(ReadId).Should().NotContain([draftId, archivedId]);
    }

    [Theory]
    [InlineData(PropertyType.Commercial, "RTD")]
    [InlineData(PropertyType.Land, "RTE")]
    public async Task RootTypeDiscovery_FilteringPrecedesCountAndPagingWithStableNewestOrder(
        PropertyType targetType,
        string currency)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        DateTime baseTime = new(2038, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var targetIds = new List<Guid>();

        for (int index = 0; index < 3; index++)
        {
            Guid listingId = await CreateRootDiscoveryListingAsync(
                owner,
                targetType,
                currency);
            await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
                _factory,
                listingId,
                ListingStatus.Active,
                baseTime.AddHours(index));
            targetIds.Add(listingId);
        }

        foreach ((PropertyType otherType, int index) in Enum
                     .GetValues<PropertyType>()
                     .Where(value => value != targetType)
                     .Select((value, index) => (value, index)))
        {
            Guid listingId = await CreateRootDiscoveryListingAsync(
                owner,
                otherType,
                currency);
            await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
                _factory,
                listingId,
                ListingStatus.Active,
                baseTime.AddDays(1).AddHours(index));
        }

        string baseQuery =
            "/api/listings" +
            "?lang=en" +
            $"&currency={currency}" +
            $"&propertyType={targetType}" +
            "&sort=newest&pageSize=2";
        JsonElement firstPage = await ReadRootDiscoveryPageAsync(
            baseQuery + "&page=1");
        JsonElement secondPage = await ReadRootDiscoveryPageAsync(
            baseQuery + "&page=2");

        AssertPagingMetadata(firstPage, pageNumber: 1, pageSize: 2, totalCount: 3, totalPages: 2);
        AssertPagingMetadata(secondPage, pageNumber: 2, pageSize: 2, totalCount: 3, totalPages: 2);
        ReadItems(firstPage).Select(ReadId).Should().Equal(targetIds[2], targetIds[1]);
        ReadItems(secondPage).Select(ReadId).Should().Equal(targetIds[0]);
        ReadItems(firstPage).Concat(ReadItems(secondPage))
            .Should().OnlyContain(item => ReadPropertyType(item) == targetType);
    }

    [Theory]
    [InlineData(PropertyType.Commercial, "RTF")]
    [InlineData(PropertyType.Land, "RTG")]
    public async Task RootTypeDiscovery_EffectiveFallbackTranslationAndTrustedLocationAreShared(
        PropertyType propertyType,
        string currency)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        string prefix = propertyType.ToString();
        Guid listingId = await CreateRootDiscoveryListingAsync(
            owner,
            propertyType,
            currency,
            translations:
            [
                CreateRootDiscoveryTranslation(
                    "sq",
                    $"{prefix} supplementary title",
                    $"{prefix} supplementary city",
                    $"{prefix} supplementary municipality",
                    $"{prefix} supplementary neighborhood"),
                CreateRootDiscoveryTranslation(
                    "de",
                    $"{prefix} selected title",
                    $"{prefix} selected city",
                    $"{prefix} selected municipality",
                    $"{prefix} selected neighborhood")
            ]);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);

        JsonElement page = await ReadRootDiscoveryPageAsync(
            "/api/listings" +
            "?lang=fr" +
            $"&currency={currency}" +
            $"&propertyType={propertyType}" +
            "&page=1&pageSize=20");
        JsonElement item = ReadItems(page).Should().ContainSingle().Subject;

        item.GetProperty("id").GetGuid().Should().Be(listingId);
        item.GetProperty("languageCode").GetString().Should().Be("de");
        item.GetProperty("title").GetString().Should().Be($"{prefix} selected title");
        item.GetProperty("city").GetString().Should().Be($"{prefix} selected city");
        item.GetProperty("municipality").GetString().Should()
            .Be($"{prefix} selected municipality");
        item.GetProperty("neighborhood").GetString().Should()
            .Be($"{prefix} selected neighborhood");
        item.GetProperty("latitude").GetDecimal().Should()
            .Be(StrongLocationListingTestFixtures.ConfirmedLatitude);
        item.GetProperty("longitude").GetDecimal().Should()
            .Be(StrongLocationListingTestFixtures.ConfirmedLongitude);
        item.GetProperty("locationPrecision").GetString().Should().Be("ExactAddress");
    }

    [Theory]
    [InlineData(PropertyType.Commercial, "title", "RTH")]
    [InlineData(PropertyType.Commercial, "city", "RTI")]
    [InlineData(PropertyType.Commercial, "municipality", "RTJ")]
    [InlineData(PropertyType.Commercial, "neighborhood", "RTK")]
    [InlineData(PropertyType.Land, "title", "RTL")]
    [InlineData(PropertyType.Land, "city", "RTM")]
    [InlineData(PropertyType.Land, "municipality", "RTN")]
    [InlineData(PropertyType.Land, "neighborhood", "RTO")]
    public async Task RootTypeDiscovery_QMatchesOnlyAnIncludedEffectiveTranslationField(
        PropertyType propertyType,
        string field,
        string currency)
    {
        const string needle = "Root Discovery Needle";
        CreateListingTranslationRequest translation =
            CreateRootDiscoveryTranslation(
                "en",
                "Neutral title",
                "Neutral city",
                "Neutral municipality",
                "Neutral neighborhood");
        SetRootDiscoveryTranslationField(translation, field, $"Prefix {needle} suffix");
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid listingId = await CreateRootDiscoveryListingAsync(
            owner,
            propertyType,
            currency,
            translations: [translation]);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);

        JsonElement page = await ReadRootDiscoveryPageAsync(
            "/api/listings" +
            "?lang=en" +
            $"&currency={currency}" +
            $"&propertyType={propertyType}" +
            $"&q={Uri.EscapeDataString(needle)}" +
            "&page=1&pageSize=20");

        AssertExactRootPage(page, listingId, propertyType);
    }

    [Theory]
    [InlineData(PropertyType.Commercial, "RTP", "Office")]
    [InlineData(PropertyType.Land, "RTQ", "AgriculturalLand")]
    public async Task RootTypeDiscovery_QDoesNotMatchExcludedTextOrSubtypeLabels(
        PropertyType propertyType,
        string currency,
        string subtypeLabel)
    {
        const string excludedNeedle = "Excluded Root Discovery Needle";
        CreateListingTranslationRequest translation =
            CreateRootDiscoveryTranslation(
                "en",
                "Neutral title",
                "Neutral city",
                "Neutral municipality",
                "Neutral neighborhood");
        translation.Description = $"Description {excludedNeedle}";
        translation.AddressLine = $"Address {excludedNeedle}";
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid listingId = await CreateRootDiscoveryListingAsync(
            owner,
            propertyType,
            currency,
            translations: [translation],
            commercialType: CommercialType.Office,
            landType: LandType.AgriculturalLand);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);

        foreach (string excludedQuery in new[] { excludedNeedle, subtypeLabel })
        {
            JsonElement page = await ReadRootDiscoveryPageAsync(
                "/api/listings" +
                "?lang=en" +
                $"&currency={currency}" +
                $"&propertyType={propertyType}" +
                $"&q={Uri.EscapeDataString(excludedQuery)}" +
                "&page=1&pageSize=20");

            page.GetProperty("totalCount").GetInt32().Should().Be(0);
            ReadItems(page).Should().BeEmpty();
        }
    }

    [Fact]
    public async Task RootTypeDiscovery_AgencyPublicSharedPathReturnsCommercialAndLand()
    {
        const string currency = "RTR";
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        await SetAgencyStatusAsync(agencyId, AgencyStatus.Active);
        Guid commercialId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Commercial,
            currency,
            agencyId);
        Guid landId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Land,
            currency,
            agencyId);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            commercialId,
            ListingStatus.Active);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            landId,
            ListingStatus.Active);

        JsonElement page = await ReadRootDiscoveryPageAsync(
            $"/api/agencies/{agencyId}/listings" +
            $"?lang=en&currency={currency}&page=1&pageSize=20");
        List<JsonElement> items = ReadItems(page);

        page.GetProperty("totalCount").GetInt32().Should().Be(2);
        items.Select(ReadId).Should().BeEquivalentTo([commercialId, landId]);
        items.Select(ReadPropertyType).Should()
            .BeEquivalentTo([PropertyType.Commercial, PropertyType.Land]);
        items.Single(item => ReadId(item) == commercialId)
            .GetProperty("commercialDetails")
            .GetProperty("commercialType")
            .GetString()
            .Should().Be("Office");
        items.Single(item => ReadId(item) == landId)
            .GetProperty("landDetails")
            .GetProperty("landType")
            .GetString()
            .Should().Be("AgriculturalLand");
    }

    [Fact]
    public async Task RootTypeDiscovery_UndefinedNumericPropertyTypeReturnsCanonicalValidation()
    {
        const string path = "/api/listings?propertyType=999";

        HttpResponseMessage response = await _httpClient.GetAsync(path);

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            "/api/listings",
            validationKey: "propertyType");
    }

    [Fact]
    public async Task RootTypeDiscovery_DoesNotExposeCommercialOrLandSubtypeFilters()
    {
        string[] forbiddenNames = ["commercialType", "landType"];
        string[] queryMembers = typeof(GetListingsQuery)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToArray();
        MethodInfo endpoint = typeof(ListingsController)
            .GetMethod(nameof(ListingsController.GetListings))!;
        string[] endpointParameters = endpoint
            .GetParameters()
            .Select(parameter => parameter.Name!)
            .ToArray();

        queryMembers.Should().NotContainEquivalentOf(forbiddenNames[0]);
        queryMembers.Should().NotContainEquivalentOf(forbiddenNames[1]);
        endpointParameters.Should().NotContainEquivalentOf(forbiddenNames[0]);
        endpointParameters.Should().NotContainEquivalentOf(forbiddenNames[1]);

        const string currency = "RTS";
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid commercialId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Commercial,
            currency,
            commercialType: CommercialType.Office);
        Guid landId = await CreateRootDiscoveryListingAsync(
            owner,
            PropertyType.Land,
            currency,
            landType: LandType.AgriculturalLand);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            commercialId,
            ListingStatus.Active);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            landId,
            ListingStatus.Active);

        JsonElement page = await ReadRootDiscoveryPageAsync(
            "/api/listings" +
            $"?lang=en&currency={currency}" +
            "&commercialType=Shop&landType=BuildingPlot" +
            "&page=1&pageSize=20");

        ReadItems(page).Select(ReadId).Should()
            .BeEquivalentTo([commercialId, landId]);
        page.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    private async Task<Guid> CreateRootDiscoveryListingAsync(
        AuthenticatedTestUser owner,
        PropertyType propertyType,
        string currency,
        Guid? agencyId = null,
        IReadOnlyList<CreateListingTranslationRequest>? translations = null,
        CommercialType commercialType = CommercialType.Office,
        LandType landType = LandType.AgriculturalLand)
    {
        CreateListingRequest request = new()
        {
            ListingType = ListingType.Sale,
            PropertyType = propertyType,
            AgencyId = agencyId,
            Price = 125_000m,
            Currency = currency,
            AreaSquareMeters = 100m,
            Rooms = propertyType == PropertyType.Land ? null : 3m,
            Bathrooms = propertyType == PropertyType.Land ? null : 1m,
            HeatingType = HeatingType.Unknown,
            FurnishingStatus = FurnishingStatus.Unknown,
            Condition = PropertyCondition.Unknown,
            Orientation = Orientation.Unknown,
            ApartmentDetails = propertyType == PropertyType.Apartment
                ? new CreateListingApartmentDetailsRequest
                {
                    ApartmentType = ApartmentType.Standard,
                    Floor = 2,
                    TotalFloors = 6,
                    HasElevator = true
                }
                : null,
            HouseDetails = propertyType == PropertyType.House
                ? new CreateListingHouseDetailsRequest
                {
                    HouseType = HouseType.Detached,
                    NumberOfFloors = 2,
                    YardAreaSquareMeters = 250m
                }
                : null,
            CommercialDetails = propertyType == PropertyType.Commercial
                ? new CreateListingCommercialDetailsRequest
                {
                    CommercialType = commercialType
                }
                : null,
            LandDetails = propertyType == PropertyType.Land
                ? new CreateListingLandDetailsRequest
                {
                    LandType = landType
                }
                : null,
            Translations = translations?.ToList() ??
            [
                CreateRootDiscoveryTranslation(
                    "en",
                    $"{propertyType} discovery listing",
                    "Skopje",
                    "Centar",
                    "Center"),
                CreateRootDiscoveryTranslation(
                    "mk",
                    $"{propertyType} discovery fallback",
                    "Skopje",
                    "Centar",
                    "Center")
            ]
        };

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/listings",
                request);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("id").GetGuid();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private static CreateListingTranslationRequest CreateRootDiscoveryTranslation(
        string languageCode,
        string title,
        string city,
        string municipality,
        string neighborhood)
    {
        return new CreateListingTranslationRequest
        {
            LanguageCode = languageCode,
            Title = title,
            Description = $"{title} description",
            AddressLine = $"{title} address",
            City = city,
            Municipality = municipality,
            Neighborhood = neighborhood
        };
    }

    private static void SetRootDiscoveryTranslationField(
        CreateListingTranslationRequest translation,
        string field,
        string value)
    {
        switch (field)
        {
            case "title":
                translation.Title = value;
                break;
            case "city":
                translation.City = value;
                break;
            case "municipality":
                translation.Municipality = value;
                break;
            case "neighborhood":
                translation.Neighborhood = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }
    }

    private async Task<JsonElement> ReadRootDiscoveryPageAsync(string requestUri)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(requestUri);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<JsonElement> ReadItems(JsonElement page)
    {
        return page.GetProperty("items").EnumerateArray().ToList();
    }

    private static Guid ReadId(JsonElement item)
    {
        return item.GetProperty("id").GetGuid();
    }

    private static PropertyType ReadPropertyType(JsonElement item)
    {
        return Enum.Parse<PropertyType>(
            item.GetProperty("propertyType").GetString()!,
            ignoreCase: false);
    }

    private static void AssertExactRootPage(
        JsonElement page,
        Guid listingId,
        PropertyType propertyType)
    {
        page.GetProperty("totalCount").GetInt32().Should().Be(1);
        JsonElement item = ReadItems(page).Should().ContainSingle().Subject;
        ReadId(item).Should().Be(listingId);
        ReadPropertyType(item).Should().Be(propertyType);
    }

    private static void AssertPagingMetadata(
        JsonElement page,
        int pageNumber,
        int pageSize,
        int totalCount,
        int totalPages)
    {
        page.GetProperty("page").GetInt32().Should().Be(pageNumber);
        page.GetProperty("pageSize").GetInt32().Should().Be(pageSize);
        page.GetProperty("totalCount").GetInt32().Should().Be(totalCount);
        page.GetProperty("totalPages").GetInt32().Should().Be(totalPages);
        page.GetProperty("hasNextPage").GetBoolean().Should()
            .Be(pageNumber < totalPages);
        page.GetProperty("hasPreviousPage").GetBoolean().Should().Be(pageNumber > 1);
    }
}
