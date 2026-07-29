using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response =
                await _httpClient.PutAsync($"/api/listings/{listingId}/publish?lang=en", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            ListingStatus responseStatus = await ReadListingStatusAsync(response);
            responseStatus.Should().Be(ListingStatus.Active);

            ListingStatus databaseStatus = await GetListingStatusAsync(listingId);
            databaseStatus.Should().Be(ListingStatus.Active);
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

            ListingStatus responseStatus = await ReadListingStatusAsync(response);
            responseStatus.Should().Be(ListingStatus.Active);
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

        _httpClient.AuthorizeAs(owner.AccessToken);

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
    public async Task PublishListing_ShouldPublishAgencyDraftListing_WhenUserIsActiveAgent()
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
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Listings""
               SET ""Status"" = {status.ToString()}
               WHERE ""Id"" = {listingId}");
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
}
