using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Theory]
    [InlineData(CommercialType.Unknown)]
    [InlineData(CommercialType.Office)]
    [InlineData(CommercialType.Shop)]
    [InlineData(CommercialType.Other)]
    public async Task CreateListing_Commercial_PersistsExactSubtypeAndResponse(
        CommercialType commercialType)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await AssertNewRootCreatedAsync(
            owner,
            CreateCommercialRequest(commercialType),
            PropertyType.Commercial,
            commercialType.ToString());
    }

    [Theory]
    [InlineData(LandType.Unknown)]
    [InlineData(LandType.BuildingPlot)]
    [InlineData(LandType.AgriculturalLand)]
    [InlineData(LandType.Other)]
    public async Task CreateListing_Land_PersistsExactSubtypeAndResponse(
        LandType landType)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await AssertNewRootCreatedAsync(
            owner,
            CreateLandRequest(landType),
            PropertyType.Land,
            landType.ToString());
    }

    [Theory]
    [InlineData(PropertyType.Commercial)]
    [InlineData(PropertyType.Land)]
    public async Task CreateListing_NewRootWithOmittedInnerSubtype_DefaultsToUnknown(
        PropertyType propertyType)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/listings",
                CreateRequestWithOmittedInnerSubtype(propertyType));

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            string detailsName = propertyType == PropertyType.Commercial
                ? "commercialDetails"
                : "landDetails";
            string typeName = propertyType == PropertyType.Commercial
                ? "commercialType"
                : "landType";

            json.GetProperty(detailsName).GetProperty(typeName).GetString()
                .Should().Be("Unknown");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(PropertyType.Commercial)]
    [InlineData(PropertyType.Land)]
    public async Task CreateListing_NewRootWithActiveAgencyOwner_PreservesAuthorizationParity(
        PropertyType propertyType)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        CreateListingRequest request = propertyType == PropertyType.Commercial
            ? CreateCommercialRequest(CommercialType.Office, agencyId)
            : CreateLandRequest(LandType.BuildingPlot, agencyId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            json.GetProperty("agencyId").GetGuid().Should().Be(agencyId);

            await using AsyncServiceScope scope =
                _factory.Services.CreateAsyncScope();
            RealEstateDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
            Listing persisted = await dbContext.Listings.SingleAsync(listing =>
                listing.Id == json.GetProperty("id").GetGuid());
            persisted.CreatedByUserId.Should().Be(owner.UserId);
            persisted.AgencyId.Should().Be(agencyId);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_NewRootWithInvalidAgencyRole_RemainsForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser manager =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyWithMemberAsync(
            owner.UserId,
            manager.UserId,
            AgencyMemberRole.Manager,
            AgencyMemberStatus.Active);
        _httpClient.AuthorizeAs(manager.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/listings",
                CreateCommercialRequest(CommercialType.Shop, agencyId));

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                "/api/listings");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_NewRootWithDisabledAgencyOwner_RemainsForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        await SetUserStatusAsync(owner.UserId, UserStatus.Disabled);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/listings",
                CreateLandRequest(LandType.Other, agencyId));

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationAccountDisabled,
                "/api/listings");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(PropertyType.Commercial)]
    [InlineData(PropertyType.Land)]
    public async Task CreateListing_NewRootUnknown_PublishesAndPublicReads(
        PropertyType propertyType)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        CreateListingRequest request = propertyType == PropertyType.Commercial
            ? CreateCommercialRequest(CommercialType.Unknown)
            : CreateLandRequest(LandType.Unknown);
        Guid listingId = await CreateNewRootAsAsync(owner, request);

        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await ListingTestHelpers.PrepareStrongLocationPublishableDraftAsync(
            _factory,
            listingId);
        _httpClient.AuthorizeAs(owner.AccessToken);

        HttpResponseMessage publishResponse;

        try
        {
            publishResponse = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish?lang=en",
                null);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNewRootJson(
            await publishResponse.Content.ReadFromJsonAsync<JsonElement>(),
            propertyType,
            "Unknown");

        HttpResponseMessage publicResponse = await _httpClient.GetAsync(
            $"/api/listings/{listingId}?lang=en");

        publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement publicJson =
            await publicResponse.Content.ReadFromJsonAsync<JsonElement>();
        AssertNewRootJson(publicJson, propertyType, "Unknown");
        publicJson.GetProperty("status").GetString().Should().Be("Active");
        publicJson.GetProperty("title").GetString()
            .Should().Be(propertyType == PropertyType.Commercial
                ? "Integration test commercial property"
                : "Integration test land");
        publicJson.GetProperty("latitude").ValueKind
            .Should().Be(JsonValueKind.Number);
        publicJson.GetProperty("longitude").ValueKind
            .Should().Be(JsonValueKind.Number);
        publicJson.GetProperty("municipality").GetString()
            .Should().Be("Centar");
        publicJson.GetProperty("addressLine").GetString()
            .Should().Be("Center");
    }

    [Theory]
    [InlineData(PropertyType.Commercial)]
    [InlineData(PropertyType.Land)]
    public async Task CreateListing_NewRootPutRemainsRejectedWithoutMutation(
        PropertyType propertyType)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        CreateListingRequest createRequest = propertyType == PropertyType.Commercial
            ? CreateCommercialRequest(CommercialType.Office)
            : CreateLandRequest(LandType.AgriculturalLand);
        Guid listingId = await CreateNewRootAsAsync(owner, createRequest);
        ListingSnapshot before = await ReadListingSnapshotAsync(listingId);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                new
                {
                    listingType = "Rent",
                    propertyType = propertyType.ToString(),
                    price = before.Price + 12_345m,
                    currency = "USD",
                    areaSquareMeters = before.AreaSquareMeters + 10m,
                    apartmentDetails = (object?)null,
                    houseDetails = (object?)null,
                    translations = new[]
                    {
                        new
                        {
                            languageCode = "en",
                            title = "This replacement must not persist",
                            description = "Divergent replacement",
                            addressLine = "Different address",
                            city = "Bitola",
                            municipality = "Bitola",
                            neighborhood = "Center"
                        }
                    }
                });

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed,
                $"/api/listings/{listingId}",
                validationKey: "propertyType");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        ListingSnapshot after = await ReadListingSnapshotAsync(listingId);
        after.Should().BeEquivalentTo(before);
    }

    private async Task AssertNewRootCreatedAsync(
        AuthenticatedTestUser owner,
        CreateListingRequest request,
        PropertyType expectedPropertyType,
        string expectedSubtype)
    {
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync("/api/listings", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            Guid listingId = json.GetProperty("id").GetGuid();

            AssertNewRootJson(json, expectedPropertyType, expectedSubtype);
            json.GetProperty("status").GetString().Should().Be("Draft");
            json.GetProperty("agencyId").ValueKind.Should().Be(JsonValueKind.Null);

            await using AsyncServiceScope scope =
                _factory.Services.CreateAsyncScope();
            RealEstateDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
            Listing persisted = await dbContext.Listings
                .Include(listing => listing.ApartmentDetails)
                .Include(listing => listing.HouseDetails)
                .Include(listing => listing.CommercialDetails)
                .Include(listing => listing.LandDetails)
                .SingleAsync(listing => listing.Id == listingId);

            persisted.Status.Should().Be(ListingStatus.Draft);
            persisted.CreatedByUserId.Should().Be(owner.UserId);
            persisted.AgencyId.Should().BeNull();
            persisted.CreatedAtUtc.Should().NotBe(default);
            persisted.ModifiedAtUtc.Should().NotBe(default);
            persisted.PropertyType.Should().Be(expectedPropertyType);
            CountSubtypeChildren(persisted).Should().Be(1);

            string rootStorage = await dbContext.Database.SqlQueryRaw<string>(
                    """
                    SELECT "PropertyType" AS "Value"
                    FROM "Listings"
                    WHERE "Id" = {0}
                    """,
                    listingId)
                .SingleAsync();
            string subtypeStorage = expectedPropertyType == PropertyType.Commercial
                ? await dbContext.Database.SqlQueryRaw<string>(
                        """
                        SELECT "CommercialType" AS "Value"
                        FROM "ListingCommercialDetails"
                        WHERE "ListingId" = {0}
                        """,
                        listingId)
                    .SingleAsync()
                : await dbContext.Database.SqlQueryRaw<string>(
                        """
                        SELECT "LandType" AS "Value"
                        FROM "ListingLandDetails"
                        WHERE "ListingId" = {0}
                        """,
                        listingId)
                    .SingleAsync();

            rootStorage.Should().Be(expectedPropertyType.ToString());
            subtypeStorage.Should().Be(expectedSubtype);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<Guid> CreateNewRootAsAsync(
        AuthenticatedTestUser owner,
        CreateListingRequest request)
    {
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync("/api/listings", request);
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

    private async Task<ListingSnapshot> ReadListingSnapshotAsync(Guid listingId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        Listing listing = await dbContext.Listings
            .AsNoTracking()
            .Include(candidate => candidate.Translations)
            .Include(candidate => candidate.CommercialDetails)
            .Include(candidate => candidate.LandDetails)
            .SingleAsync(candidate => candidate.Id == listingId);

        return new ListingSnapshot(
            listing.ListingType,
            listing.PropertyType,
            listing.Price,
            listing.Currency,
            listing.AreaSquareMeters,
            listing.Translations.OrderBy(translation => translation.LanguageCode)
                .Select(translation => $"{translation.LanguageCode}:{translation.Title}")
                .ToArray(),
            listing.CommercialDetails?.CommercialType,
            listing.LandDetails?.LandType);
    }

    private static CreateListingRequest CreateCommercialRequest(
        CommercialType commercialType,
        Guid? agencyId = null)
    {
        return CreateBaseNewRootRequest(
            PropertyType.Commercial,
            agencyId,
            commercialDetails: new CreateListingCommercialDetailsRequest
            {
                CommercialType = commercialType
            });
    }

    private static CreateListingRequest CreateLandRequest(
        LandType landType,
        Guid? agencyId = null)
    {
        return CreateBaseNewRootRequest(
            PropertyType.Land,
            agencyId,
            landDetails: new CreateListingLandDetailsRequest
            {
                LandType = landType
            });
    }

    private static CreateListingRequest CreateBaseNewRootRequest(
        PropertyType propertyType,
        Guid? agencyId,
        CreateListingCommercialDetailsRequest? commercialDetails = null,
        CreateListingLandDetailsRequest? landDetails = null)
    {
        string isCommercialTitle = propertyType == PropertyType.Commercial
            ? "commercial property"
            : "land";

        return new CreateListingRequest
        {
            ListingType = ListingType.Sale,
            PropertyType = propertyType,
            AgencyId = agencyId,
            Price = propertyType == PropertyType.Commercial ? 175_000m : 85_000m,
            Currency = "EUR",
            AreaSquareMeters = propertyType == PropertyType.Commercial ? 95m : 650m,
            Rooms = propertyType == PropertyType.Commercial ? 3m : null,
            Bathrooms = propertyType == PropertyType.Commercial ? 1m : null,
            HeatingType = HeatingType.Unknown,
            FurnishingStatus = FurnishingStatus.Unknown,
            Condition = PropertyCondition.Unknown,
            Orientation = Orientation.Unknown,
            CommercialDetails = commercialDetails,
            LandDetails = landDetails,
            Translations =
            [
                new CreateListingTranslationRequest
                {
                    LanguageCode = "en",
                    Title = $"Integration test {isCommercialTitle}",
                    Description = "Complete commercial or land publication description",
                    AddressLine = "Center",
                    City = "Skopje",
                    Municipality = "Centar",
                    Neighborhood = "Center"
                },
                new CreateListingTranslationRequest
                {
                    LanguageCode = "mk",
                    Title = propertyType == PropertyType.Commercial
                        ? "Интеграциски деловен простор"
                        : "Интеграциско земјиште",
                    Description = "Комплетен опис за објавување",
                    AddressLine = "Центар",
                    City = "Скопје",
                    Municipality = "Центар",
                    Neighborhood = "Центар"
                }
            ]
        };
    }

    private static object CreateRequestWithOmittedInnerSubtype(
        PropertyType propertyType)
    {
        return new
        {
            listingType = "Sale",
            propertyType = propertyType.ToString(),
            price = 100_000m,
            currency = "EUR",
            areaSquareMeters = 100m,
            apartmentDetails = (object?)null,
            houseDetails = (object?)null,
            commercialDetails = propertyType == PropertyType.Commercial
                ? new { }
                : null,
            landDetails = propertyType == PropertyType.Land
                ? new { }
                : null,
            translations = new[]
            {
                new
                {
                    languageCode = "en",
                    title = "Omitted subtype defaults to Unknown",
                    description = "Description",
                    addressLine = "Center",
                    city = "Skopje",
                    municipality = "Centar",
                    neighborhood = "Center"
                }
            }
        };
    }

    private static void AssertNewRootJson(
        JsonElement json,
        PropertyType propertyType,
        string expectedSubtype)
    {
        json.GetProperty("propertyType").GetString()
            .Should().Be(propertyType.ToString());
        json.GetProperty("apartmentDetails").ValueKind
            .Should().Be(JsonValueKind.Null);
        json.GetProperty("houseDetails").ValueKind
            .Should().Be(JsonValueKind.Null);

        if (propertyType == PropertyType.Commercial)
        {
            json.GetProperty("commercialDetails")
                .GetProperty("commercialType").GetString()
                .Should().Be(expectedSubtype);
            json.GetProperty("landDetails").ValueKind
                .Should().Be(JsonValueKind.Null);
        }
        else
        {
            json.GetProperty("commercialDetails").ValueKind
                .Should().Be(JsonValueKind.Null);
            json.GetProperty("landDetails")
                .GetProperty("landType").GetString()
                .Should().Be(expectedSubtype);
        }
    }

    private static int CountSubtypeChildren(Listing listing)
    {
        return new object?[]
        {
            listing.ApartmentDetails,
            listing.HouseDetails,
            listing.CommercialDetails,
            listing.LandDetails
        }.Count(details => details is not null);
    }

    private sealed record ListingSnapshot(
        ListingType ListingType,
        PropertyType PropertyType,
        decimal Price,
        string Currency,
        decimal AreaSquareMeters,
        IReadOnlyList<string> Translations,
        CommercialType? CommercialType,
        LandType? LandType);
}
