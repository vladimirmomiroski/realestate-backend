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
    [Fact]
    public async Task GetListingManagement_WithoutToken_ReturnsUnauthorized()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings/{listingId}/management");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetListingManagement_WhenListingDoesNotExist_ReturnsNotFound()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{Guid.NewGuid()}/management");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Active)]
    [InlineData(ListingStatus.Archived)]
    public async Task GetListingManagement_WhenPersonalOwner_ReturnsEveryStatus(
        ListingStatus status)
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetListingStatusAsync(listingId, status);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            json.GetProperty("status").GetString().Should().Be(status.ToString());
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetListingManagement_WhenPersonalNonowner_ReturnsForbidden()
    {
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SeedDormantSubtypeDetailsAsync(listingId);
        AuthenticatedTestUser nonowner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(nonowner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetListingManagement_WhenPersonalOwnerIsDisabled_ReturnsForbidden()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Disabled);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetListingManagement_WhenPersonalOwnerIsPendingVerification_ReturnsOk()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.PendingVerification);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetListingManagement_WhenAgencyOwnerMembershipIsActive_ReturnsOk()
    {
        (Guid listingId, _, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetListingManagement_WhenAgencyAgentMembershipIsActive_ReturnsOk()
    {
        (Guid listingId, Guid agencyId, _) =
            await CreateAgencyListingWithOwnerAsync();
        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(agent.UserId, UserStatus.Active);
        await AddAgencyMemberAsync(
            agencyId,
            agent.UserId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Active);
        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(AgencyMemberRole.Manager, AgencyMemberStatus.Active)]
    [InlineData(AgencyMemberRole.Agent, AgencyMemberStatus.Pending)]
    [InlineData(AgencyMemberRole.Agent, AgencyMemberStatus.Disabled)]
    public async Task GetListingManagement_WhenAgencyMembershipCannotAuthor_ReturnsForbidden(
        AgencyMemberRole role,
        AgencyMemberStatus memberStatus)
    {
        (Guid listingId, Guid agencyId, _) =
            await CreateAgencyListingWithOwnerAsync();
        AuthenticatedTestUser member =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(member.UserId, UserStatus.Active);
        await AddAgencyMemberAsync(agencyId, member.UserId, role, memberStatus);
        _httpClient.AuthorizeAs(member.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetListingManagement_WhenUserIsNotAgencyMember_ReturnsForbidden()
    {
        (Guid listingId, _, _) = await CreateAgencyListingWithOwnerAsync();
        await SeedDormantSubtypeDetailsAsync(listingId);
        AuthenticatedTestUser nonmember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(nonmember.UserId, UserStatus.Active);
        _httpClient.AuthorizeAs(nonmember.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetListingManagement_WhenPendingVerificationAgencyAgent_ReturnsOk()
    {
        (Guid listingId, Guid agencyId, _) =
            await CreateAgencyListingWithOwnerAsync();
        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(agent.UserId, UserStatus.PendingVerification);
        await AddAgencyMemberAsync(
            agencyId,
            agent.UserId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Active);
        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(AgencyStatus.PendingVerification)]
    [InlineData(AgencyStatus.Disabled)]
    [InlineData(AgencyStatus.Rejected)]
    public async Task GetListingManagement_WhenAgencyIsNotActive_DoesNotBlockOwner(
        AgencyStatus agencyStatus)
    {
        (Guid listingId, Guid agencyId, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();
        await SetAgencyStatusAsync(agencyId, agencyStatus);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetListingManagement_ReturnsCompleteDeterministicApartmentState()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        Guid sqId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        Guid deId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid enId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId,
            NewTranslation(sqId, "sq", "Titull", "Përshkrim", "Shkup"),
            NewTranslation(deId, "de", "Titel", null, null),
            NewTranslation(enId, "en", "Title", "Description", "Skopje"));

        Guid imageId = await AddListingImageAsync(listingId);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().Be(listingId);
            json.GetProperty("createdByUserId").GetGuid().Should().Be(owner.UserId);
            json.GetProperty("agencyId").ValueKind.Should().Be(JsonValueKind.Null);
            json.GetProperty("listingType").GetString().Should().Be("Sale");
            json.GetProperty("propertyType").GetString().Should().Be("Apartment");
            json.GetProperty("price").GetDecimal().Should().Be(99_000m);
            json.GetProperty("currency").GetString().Should().Be("EUR");
            json.GetProperty("areaSquareMeters").GetDecimal().Should().Be(58m);
            json.GetProperty("createdAtUtc").GetDateTime().Should().NotBe(default);

            JsonElement apartment = json.GetProperty("apartmentDetails");
            apartment.GetProperty("apartmentType").GetString().Should().Be("Standard");
            apartment.GetProperty("floor").GetInt32().Should().Be(4);
            apartment.GetProperty("totalFloors").GetInt32().Should().Be(8);
            apartment.GetProperty("hasElevator").GetBoolean().Should().BeTrue();
            json.GetProperty("houseDetails").ValueKind.Should().Be(JsonValueKind.Null);
            json.GetProperty("commercialDetails").ValueKind
                .Should().Be(JsonValueKind.Null);
            json.GetProperty("landDetails").ValueKind
                .Should().Be(JsonValueKind.Null);

            JsonElement.ArrayEnumerator translations =
                json.GetProperty("translations").EnumerateArray();
            JsonElement[] translationRows = translations.ToArray();
            translationRows.Select(row => row.GetProperty("languageCode").GetString())
                .Should().Equal("de", "en", "sq");
            translationRows[0].GetProperty("id").GetGuid().Should().Be(deId);
            translationRows[0].GetProperty("title").GetString().Should().Be("Titel");
            translationRows[0].GetProperty("city").ValueKind.Should().Be(JsonValueKind.Null);
            translationRows[0].GetProperty("description").ValueKind.Should().Be(JsonValueKind.Null);
            translationRows[1].GetProperty("description").GetString().Should().Be("Description");
            translationRows[2].GetProperty("city").GetString().Should().Be("Shkup");

            JsonElement image = json.GetProperty("images").EnumerateArray().Single();
            image.GetProperty("id").GetGuid().Should().Be(imageId);
            image.GetProperty("url").GetString().Should().Be("/uploads/listings/test.jpg");
            image.GetProperty("contentType").GetString().Should().Be("image/jpeg");
            image.GetProperty("sizeBytes").GetInt64().Should().Be(1234);
            image.GetProperty("sortOrder").GetInt32().Should().Be(0);
            image.GetProperty("isPrimary").GetBoolean().Should().BeTrue();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetListingManagement_ForHouse_ReturnsCompleteHouseDetails()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage createResponse = await _httpClient.PostAsJsonAsync(
                "/api/listings",
                ListingTestHelpers.CreateValidHouseListingRequest());
            createResponse.EnsureSuccessStatusCode();
            JsonElement created =
                await createResponse.Content.ReadFromJsonAsync<JsonElement>();
            Guid listingId = created.GetProperty("id").GetGuid();

            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            json.GetProperty("apartmentDetails").ValueKind.Should().Be(JsonValueKind.Null);
            JsonElement house = json.GetProperty("houseDetails");
            house.GetProperty("houseType").GetString().Should().Be("Detached");
            house.GetProperty("numberOfFloors").GetInt32().Should().Be(2);
            house.GetProperty("yardAreaSquareMeters").GetDecimal().Should().Be(350m);
            json.GetProperty("commercialDetails").ValueKind
                .Should().Be(JsonValueKind.Null);
            json.GetProperty("landDetails").ValueKind
                .Should().Be(JsonValueKind.Null);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    [Trait("Name", "ManagementReadContract")]
    public async Task ManagementReadContract_GetManagement_RepresentsAllSubtypeSlotsTruthfully()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await ListingTestHelpers.ReplaceListingTranslationsAsync(
            _factory,
            listingId,
            NewTranslation(Guid.NewGuid(), "sq", "Titull", "Description", "Shkup"),
            NewTranslation(Guid.NewGuid(), "de", "Titel", "Description", "Skopje"),
            NewTranslation(Guid.NewGuid(), "en", "Title", "Description", "Skopje"));
        await SeedDormantSubtypeDetailsAsync(listingId);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/listings/{listingId}/management");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("propertyType").GetString()
                .Should().Be("Apartment");
            json.GetProperty("apartmentDetails").ValueKind
                .Should().Be(JsonValueKind.Object);
            json.GetProperty("houseDetails").ValueKind
                .Should().Be(JsonValueKind.Null);
            json.GetProperty("commercialDetails")
                .GetProperty("commercialType").GetString()
                .Should().Be("Office");
            json.GetProperty("landDetails")
                .GetProperty("landType").GetString()
                .Should().Be("AgriculturalLand");
            json.GetProperty("translations").EnumerateArray()
                .Select(translation =>
                    translation.GetProperty("languageCode").GetString())
                .Should().Equal("de", "en", "sq");

            json.GetProperty("latitude").ValueKind.Should().Be(JsonValueKind.Null);
            json.GetProperty("longitude").ValueKind.Should().Be(JsonValueKind.Null);
            json.GetProperty("locationPrecision").ValueKind
                .Should().Be(JsonValueKind.Null);
            json.GetProperty("geocodedDisplayName").ValueKind
                .Should().Be(JsonValueKind.Null);
            json.GetProperty("locationConfirmedAtUtc").ValueKind
                .Should().Be(JsonValueKind.Null);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private static ListingTranslation NewTranslation(
        Guid id,
        string languageCode,
        string title,
        string? description,
        string? city)
    {
        return new ListingTranslation
        {
            Id = id,
            LanguageCode = languageCode,
            Title = title,
            Description = description,
            AddressLine = city is null ? null : $"{city} address",
            City = city,
            Municipality = city is null ? null : "Municipality",
            Neighborhood = city is null ? null : "Neighborhood"
        };
    }

    private async Task<Guid> AddListingImageAsync(Guid listingId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
        Guid imageId = Guid.NewGuid();

        dbContext.Set<ListingImage>().Add(new ListingImage
        {
            Id = imageId,
            ListingId = listingId,
            OriginalFileName = "test.jpg",
            StoredFileName = $"{imageId:N}.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 1234,
            Url = "/uploads/listings/test.jpg",
            SortOrder = 0,
            IsPrimary = true
        });
        await dbContext.SaveChangesAsync();

        return imageId;
    }

    private async Task SeedDormantSubtypeDetailsAsync(Guid listingId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        dbContext.Set<ListingCommercialDetails>().Add(
            new ListingCommercialDetails
            {
                ListingId = listingId,
                CommercialType = CommercialType.Office
            });
        dbContext.Set<ListingLandDetails>().Add(
            new ListingLandDetails
            {
                ListingId = listingId,
                LandType = LandType.AgriculturalLand
            });
        await dbContext.SaveChangesAsync();
    }
}
