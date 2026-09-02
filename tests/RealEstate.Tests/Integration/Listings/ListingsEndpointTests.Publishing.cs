using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Common;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task PublishListing_ShouldReturnUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.PutAsync($"/api/listings/{listingId}/publish", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublishListing_ShouldReturnNotFound_WhenListingDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(user.UserId, UserStatus.Active);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{Guid.NewGuid()}/publish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldPublishPersonalDraftListing_WhenUserIsOwnerAndActive()
    {
        // Arrange
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await PrepareListingForPublishAsync(listingId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/publish?lang=en", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            await AssertStrictPublishedIdentityAsync(response);

            ListingStatus databaseStatus = await GetListingStatusAsync(listingId);
            databaseStatus.Should().Be(ListingStatus.Active);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldReturnListingNotReady_WhenPersonalDraftIsIncomplete()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await PrepareListingForPublishAsync(listingId);
        await MakeListingPublicationContentIncompleteAsync(listingId);
        PublicationSnapshot before = await GetPublicationSnapshotAsync(listingId);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish",
                null);

            JsonElement problem = await AssertFailureAsync(
                response,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictListingNotReady,
                $"/api/listings/{listingId}/publish");

            problem.GetProperty("detail").GetString().Should()
                .Be("The listing is not ready for publication.");
            string publicDescriptor =
                problem.GetProperty("title").GetString() + " " +
                problem.GetProperty("detail").GetString();
            publicDescriptor.Should().NotContainAny(
                "description",
                "translation",
                "InvalidDescription",
                "ListingTranslations",
                "constraint");
            (await GetPublicationSnapshotAsync(listingId)).Should().BeEquivalentTo(before);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("Municipality")]
    [InlineData("AddressLine")]
    public async Task PublishListing_ShouldReturnListingNotReady_WhenRequiredLocationTranslationFieldIsInvalid(
        string fieldName)
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await PrepareListingForPublishAsync(listingId);
        await MakeRequiredLocationTranslationFieldInvalidAsync(
            listingId,
            fieldName);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish",
                null);

            JsonElement problem = await AssertFailureAsync(
                response,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictListingNotReady,
                $"/api/listings/{listingId}/publish");
            problem.GetProperty("detail").GetString().Should().Be(
                "The listing is not ready for publication.");
            problem.ToString().Should().NotContain(fieldName);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishListing_ShouldReturnListingNotReady_WhenConfirmedRootIsMissing(
        bool useLegacyCoordinates)
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);

        if (useLegacyCoordinates)
        {
            await ListingTestHelpers.SetLegacyCoordinatesAsync(
                _factory,
                listingId,
                41.9981m,
                21.4254m);
        }

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish",
                null);

            JsonElement problem = await AssertFailureAsync(
                response,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictListingNotReady,
                $"/api/listings/{listingId}/publish");
            problem.GetProperty("detail").GetString().Should().Be(
                "The listing is not ready for publication.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldReturnOk_WhenPersonalListingIsAlreadyActive()
    {
        // Arrange
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await SetListingStatusAsync(listingId, ListingStatus.Active);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/publish?lang=en", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            await AssertStrictPublishedIdentityAsync(response);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldReturnConflict_WhenPersonalListingIsArchived()
    {
        // Arrange
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await SetListingStatusAsync(listingId, ListingStatus.Archived);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/publish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldReturnResourceStateBeforeReadiness_WhenArchivedIsIncomplete()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Active);
        await SetListingStatusAsync(listingId, ListingStatus.Archived);
        await MakeListingPublicationContentIncompleteAsync(listingId);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish",
                null);

            await AssertFailureAsync(
                response,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictResourceState,
                $"/api/listings/{listingId}/publish");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldReturnForbidden_WhenUserIsNotPersonalListingOwner()
    {
        // Arrange
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        AuthenticatedTestUser otherUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(otherUser.UserId, UserStatus.Active);

        _httpClient.AuthorizeAs(otherUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/publish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldReturnForbiddenBeforeReadiness_ForPersonalNonOwner()
    {
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await MakeListingPublicationContentIncompleteAsync(listingId);
        AuthenticatedTestUser otherUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(otherUser.UserId, UserStatus.Active);
        _httpClient.AuthorizeAs(otherUser.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish",
                null);

            await AssertFailureAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                $"/api/listings/{listingId}/publish");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(UserStatus.PendingVerification)]
    [InlineData(UserStatus.Disabled)]
    public async Task PublishListing_ShouldReturnForbidden_WhenUserIsNotActive(
        UserStatus userStatus)
    {
        // Arrange
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);

        await SetUserStatusAsync(owner.UserId, userStatus);
        await MakeListingPublicationContentIncompleteAsync(listingId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/publish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldPublishAgencyDraftListing_WhenUserIsActiveOwner()
    {
        // Arrange
        (Guid listingId, _, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();
        await PrepareListingForPublishAsync(listingId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/publish?lang=en", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            await AssertStrictPublishedIdentityAsync(response);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldPublishAgencyDraftListing_WhenUserIsActiveAgent()
    {
        // Arrange
        (Guid listingId, Guid agencyId, _) =
            await CreateAgencyListingWithOwnerAsync();
        await PrepareListingForPublishAsync(listingId);

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
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/publish?lang=en", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ListingStatus responseStatus = await ReadListingStatusAsync(response);
            responseStatus.Should().Be(ListingStatus.Active);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldReturnListingNotReady_WhenAgencyDraftIsIncomplete()
    {
        (Guid listingId, _, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();
        await PrepareListingForPublishAsync(listingId);
        await MakeListingPublicationContentIncompleteAsync(listingId);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish",
                null);

            await AssertFailureAsync(
                response,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictListingNotReady,
                $"/api/listings/{listingId}/publish");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldReturnOk_WhenAgencyListingIsAlreadyActiveAndReady()
    {
        (Guid listingId, _, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();
        await SetListingStatusAsync(listingId, ListingStatus.Active);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish?lang=en",
                null);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await AssertStrictPublishedIdentityAsync(response);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldReturnForbidden_WhenUserIsNotAgencyMember()
    {
        // Arrange
        (Guid listingId, _, _) =
            await CreateAgencyListingWithOwnerAsync();

        AuthenticatedTestUser nonMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(nonMember.UserId, UserStatus.Active);

        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/publish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_ShouldReturnForbiddenBeforeReadiness_ForAgencyNonMember()
    {
        (Guid listingId, _, _) = await CreateAgencyListingWithOwnerAsync();
        await MakeListingPublicationContentIncompleteAsync(listingId);
        AuthenticatedTestUser nonMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(nonMember.UserId, UserStatus.Active);
        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish",
                null);

            await AssertFailureAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                $"/api/listings/{listingId}/publish");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(AgencyMemberStatus.Disabled)]
    [InlineData(AgencyMemberStatus.Pending)]
    public async Task PublishListing_ShouldReturnForbidden_WhenAgencyMemberIsNotActive(
        AgencyMemberStatus memberStatus)
    {
        // Arrange
        (Guid listingId, Guid agencyId, _) =
            await CreateAgencyListingWithOwnerAsync();

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(agent.UserId, UserStatus.Active);

        await AddAgencyMemberAsync(
            agencyId,
            agent.UserId,
            AgencyMemberRole.Agent,
            memberStatus);

        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/publish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
    public async Task PublishListing_ShouldReturnForbidden_WhenAgencyIsNotActive(
        AgencyStatus agencyStatus)
    {
        // Arrange
        (Guid listingId, Guid agencyId, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();

        await SetAgencyStatusAsync(agencyId, agencyStatus);
        await MakeListingPublicationContentIncompleteAsync(listingId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/publish", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<(Guid ListingId, Guid AgencyId, AuthenticatedTestUser Owner)>
        CreateAgencyListingWithOwnerAsync()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusAsync(owner.UserId, UserStatus.Active);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await SetAgencyStatusAsync(agencyId, AgencyStatus.Active);

        Guid listingId = await ListingTestHelpers.CreateListingAsAsync(
            _httpClient,
            owner,
            agencyId);

        return (listingId, agencyId, owner);
    }

    private static async Task AssertStrictPublishedIdentityAsync(
        HttpResponseMessage response)
    {
        JsonElement body =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("status").GetString().Should()
            .Be("Active");
        body.GetProperty("languageCode").GetString().Should()
            .Be("en");
        body.GetProperty("title").GetString().Should()
            .Be("Integration test apartment");
        body.GetProperty("city").GetString().Should()
            .Be("Skopje");
        body.GetProperty("municipality").GetString().Should()
            .Be("Centar");
        body.GetProperty("addressLine").GetString().Should()
            .Be("Center");
        body.GetProperty("description").GetString().Should()
            .Be("Test listing created from integration tests.");
        body.GetProperty("latitude").GetDecimal().Should().Be(41.9981m);
        body.GetProperty("longitude").GetDecimal().Should().Be(21.4254m);
        body.GetProperty("locationPrecision").GetString().Should()
            .Be("ExactAddress");
    }

    private async Task<Guid> CreateAgencyAsAsync(AuthenticatedTestUser user)
    {
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            string unique = Guid.NewGuid().ToString("N");

            var request = new
            {
                name = $"Publishing Test Agency {unique}",
                slug = $"publishing-test-agency-{unique}",
                description = "Agency used for listing publishing integration tests.",
                phoneNumber = "+38970123456",
                email = $"agency-{unique}@test.com",
                websiteUrl = "https://agency.test",
                addressLine = "Partizanska 1",
                city = "Skopje",
                municipality = "Centar"
            };

            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync("/api/agencies", request);

            response.EnsureSuccessStatusCode();

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            return json.GetProperty("id").GetGuid();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task SetUserStatusAsync(Guid userId, UserStatus status)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Users""
               SET ""Status"" = {status.ToString()}
               WHERE ""Id"" = {userId}");
    }

    private async Task SetAgencyStatusAsync(Guid agencyId, AgencyStatus status)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Agencies""
               SET ""Status"" = {status.ToString()}
               WHERE ""Id"" = {agencyId}");
    }

    private async Task SetListingStatusAsync(Guid listingId, ListingStatus status)
    {
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            status);
    }

    private async Task MakeListingPublicationContentIncompleteAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""ListingTranslations""
               SET ""Description"" = NULL
               WHERE ""ListingId"" = {listingId}");
    }

    private Task PrepareListingForPublishAsync(Guid listingId)
    {
        return ListingTestHelpers.PrepareStrongLocationPublishableDraftAsync(
            _factory,
            listingId);
    }

    private async Task MakeRequiredLocationTranslationFieldInvalidAsync(
        Guid listingId,
        string fieldName)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        if (fieldName == "Municipality")
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE "ListingTranslations"
                 SET "Municipality" = NULL
                 WHERE "ListingId" = {listingId}
                 """);
            return;
        }

        if (fieldName == "AddressLine")
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 UPDATE "ListingTranslations"
                 SET "AddressLine" = NULL
                 WHERE "ListingId" = {listingId}
                 """);
            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(fieldName),
            fieldName,
            null);
    }

    private async Task<PublicationSnapshot> GetPublicationSnapshotAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        var root = await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => new
            {
                listing.Status,
                listing.CreatedAtUtc,
                listing.ModifiedAtUtc
            })
            .SingleAsync();

        PublicationTranslationSnapshot[] translations = await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .SelectMany(listing => listing.Translations)
            .OrderBy(translation => translation.LanguageCode)
            .Select(translation => new PublicationTranslationSnapshot(
                translation.Id,
                translation.LanguageCode,
                translation.Title,
                translation.City,
                translation.Description))
            .ToArrayAsync();

        return new PublicationSnapshot(
            root.Status,
            root.CreatedAtUtc,
            root.ModifiedAtUtc,
            translations);
    }

    private async Task AddAgencyMemberAsync(
        Guid agencyId,
        Guid userId,
        AgencyMemberRole role,
        AgencyMemberStatus status)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        Guid memberId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO ""AgencyMembers""
               (""Id"", ""AgencyId"", ""UserId"", ""Role"", ""Status"", ""CreatedAtUtc"", ""ModifiedAtUtc"")
               VALUES
               ({memberId}, {agencyId}, {userId}, {role.ToString()}, {status.ToString()}, {now}, NULL)");
    }

    private async Task<ListingStatus> GetListingStatusAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        return await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => listing.Status)
            .SingleAsync();
    }

    private static async Task<ListingStatus> ReadListingStatusAsync(
        HttpResponseMessage response)
    {
        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

        JsonElement statusElement = json.GetProperty("status");

        if (statusElement.ValueKind == JsonValueKind.String)
        {
            return Enum.Parse<ListingStatus>(
                statusElement.GetString()!,
                ignoreCase: true);
        }

        return (ListingStatus)statusElement.GetInt32();
    }

    private sealed record PublicationSnapshot(
        ListingStatus Status,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc,
        IReadOnlyList<PublicationTranslationSnapshot> Translations);

    private sealed record PublicationTranslationSnapshot(
        Guid Id,
        string LanguageCode,
        string Title,
        string? City,
        string? Description);
}
