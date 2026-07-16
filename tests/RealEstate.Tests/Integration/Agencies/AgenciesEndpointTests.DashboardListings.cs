using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Tests.Integration.Listings;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task GetAgencyDashboardListings_ShouldReturnUnauthorized_WhenTokenIsMissing()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{agencyId}/dashboard/listings?lang=en&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAgencyDashboardListings_ShouldReturnNotFound_WhenAgencyDoesNotExist()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{Guid.NewGuid()}/dashboard/listings?lang=en&page=1&pageSize=20");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyDashboardListings_ShouldReturnAllStatusesForAgency()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        Guid activeListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 90000m);

        Guid draftListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 100000m);

        Guid archivedListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 110000m);

        Guid reservedListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 120000m);

        Guid soldListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 130000m);

        Guid rentedListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 140000m);

        var expectedListings =
            new Dictionary<Guid, ListingStatus>
            {
                [activeListingId] = ListingStatus.Active,
                [draftListingId] = ListingStatus.Draft,
                [archivedListingId] = ListingStatus.Archived,
                [reservedListingId] = ListingStatus.Reserved,
                [soldListingId] = ListingStatus.Sold,
                [rentedListingId] = ListingStatus.Rented
            };

        foreach ((Guid listingId, ListingStatus status) in expectedListings)
        {
            await ListingTestHelpers.SetListingStatusAsync(
                _factory,
                listingId,
                status);
        }

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response =
                await _httpClient.GetAsync(
                    $"/api/agencies/{agencyId}/dashboard/listings" +
                    "?lang=en" +
                    "&page=1" +
                    "&pageSize=100");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();

            JsonElement items = json.GetProperty("items");

            items.GetArrayLength().Should().Be(6);
            json.GetProperty("totalCount").GetInt32().Should().Be(6);

            Dictionary<Guid, ListingStatus> actualListings = items
                .EnumerateArray()
                .ToDictionary(
                    item => item.GetProperty("id").GetGuid(),
                    ReadListingStatusFromJson);

            actualListings.Should().BeEquivalentTo(expectedListings);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyDashboardListings_ShouldFilterByStatus()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        Guid draftListingId = await CreateAgencyListingAsAsync(owner, agencyId, price: 90000);
        Guid activeListingId = await CreateAgencyListingAsAsync(owner, agencyId, price: 100000);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            draftListingId,
            ListingStatus.Draft);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            activeListingId,
            ListingStatus.Active);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/dashboard/listings?lang=en&status=Draft&page=1&pageSize=100");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("totalCount").GetInt32().Should().Be(1);

            JsonElement items = json.GetProperty("items");

            items.GetArrayLength().Should().Be(1);
            items[0].GetProperty("id").GetGuid().Should().Be(draftListingId);
            ReadListingStatusFromJson(items[0]).Should().Be(ListingStatus.Draft);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyDashboardListings_ShouldReturnOk_WhenUserIsActiveAgent()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await AddAgencyMemberForDashboardTestAsync(
            agencyId,
            agent.UserId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Active);

        Guid listingId = await CreateAgencyListingAsAsync(owner, agencyId);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Draft);

        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/dashboard/listings?lang=en&page=1&pageSize=20");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyDashboardListings_ShouldReturnForbidden_WhenUserIsNotAgencyMember()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser nonMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/dashboard/listings?lang=en&page=1&pageSize=20");

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
    public async Task GetAgencyDashboardListings_ShouldReturnForbidden_WhenAgencyMemberIsNotActive(
        AgencyMemberStatus memberStatus)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await AddAgencyMemberForDashboardTestAsync(
            agencyId,
            agent.UserId,
            AgencyMemberRole.Agent,
            memberStatus);

        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/dashboard/listings?lang=en&page=1&pageSize=20");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyDashboardListings_ShouldReturnForbidden_WhenUserIsDisabled()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await SetUserStatusForDashboardTestAsync(owner.UserId, UserStatus.Disabled);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/dashboard/listings?lang=en&page=1&pageSize=20");

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
    public async Task GetAgencyDashboardListings_ShouldReturnOk_WhenAgencyIsNotActive(
        AgencyStatus agencyStatus)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        Guid listingId = await CreateAgencyListingAsAsync(owner, agencyId);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Draft);

        await SetAgencyStatusForDashboardTestAsync(agencyId, agencyStatus);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/dashboard/listings?lang=en&page=1&pageSize=20");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task AddAgencyMemberForDashboardTestAsync(
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

    private async Task SetUserStatusForDashboardTestAsync(
        Guid userId,
        UserStatus status)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Users""
               SET ""Status"" = {status.ToString()}
               WHERE ""Id"" = {userId}");
    }

    private async Task SetAgencyStatusForDashboardTestAsync(
        Guid agencyId,
        AgencyStatus status)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Agencies""
               SET ""Status"" = {status.ToString()}
               WHERE ""Id"" = {agencyId}");
    }

    private static ListingStatus ReadListingStatusFromJson(JsonElement listingJson)
    {
        JsonElement statusElement = listingJson.GetProperty("status");

        if (statusElement.ValueKind == JsonValueKind.String)
        {
            return Enum.Parse<ListingStatus>(
                statusElement.GetString()!,
                ignoreCase: true);
        }

        return (ListingStatus)statusElement.GetInt32();
    }

    [Fact]
    public async Task GetAgencyDashboardListings_NewestOrderingAndPaginationRemainUnchanged()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        Guid oldestListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 100000m);

        Guid middleListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 110000m);

        Guid newestListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 120000m);

        DateTime oldestTimestamp =
            new(2032, 4, 1, 10, 0, 0, DateTimeKind.Utc);

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            oldestListingId,
            ListingStatus.Reserved,
            oldestTimestamp);

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            middleListingId,
            ListingStatus.Sold,
            oldestTimestamp.AddHours(1));

        await ListingTestHelpers.SetListingStatusAndCreatedAtUtcAsync(
            _factory,
            newestListingId,
            ListingStatus.Rented,
            oldestTimestamp.AddHours(2));

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response =
                await _httpClient.GetAsync(
                    $"/api/agencies/{agencyId}/dashboard/listings" +
                    "?lang=en" +
                    "&page=2" +
                    "&pageSize=1");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();

            JsonElement items = json.GetProperty("items");

            items.GetArrayLength().Should().Be(1);
            items[0].GetProperty("id").GetGuid()
                .Should().Be(middleListingId);

            json.GetProperty("page").GetInt32().Should().Be(2);
            json.GetProperty("pageSize").GetInt32().Should().Be(1);
            json.GetProperty("totalCount").GetInt32().Should().Be(3);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}
