using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Tests.Integration.Listings;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task GetAgencyDashboardSummary_ShouldReturnUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                accessToken: null,
                agencyId);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAgencyDashboardSummary_ShouldReturnNotFound_WhenAgencyDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                user.AccessToken,
                Guid.NewGuid());

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAgencyDashboardSummary_ShouldReturnForbidden_WhenUserIsNotAgencyMember()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser nonMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                nonMember.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(
        AgencyMemberRole.Manager,
        AgencyMemberStatus.Active)]
    [InlineData(
        AgencyMemberRole.Agent,
        AgencyMemberStatus.Pending)]
    [InlineData(
        AgencyMemberRole.Agent,
        AgencyMemberStatus.Disabled)]
    public async Task GetAgencyDashboardSummary_ShouldReturnForbidden_WhenMembershipIsNotAllowed(
        AgencyMemberRole memberRole,
        AgencyMemberStatus memberStatus)
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser member =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusForDashboardTestAsync(
            member.UserId,
            UserStatus.Active);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            member.UserId,
            memberStatus,
            memberRole);

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                member.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAgencyDashboardSummary_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await SetUserStatusForDashboardTestAsync(
            owner.UserId,
            UserStatus.Disabled);

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                owner.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAgencyDashboardSummary_ShouldReturnSummary_WhenUserIsActiveOwner()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await SetUserStatusForDashboardTestAsync(
            owner.UserId,
            UserStatus.Active);

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                owner.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.GetProperty("agencyId")
            .GetGuid()
            .Should()
            .Be(agencyId);

        json.GetProperty("agencyName")
            .GetString()
            .Should()
            .Be("Dom Real Estate");

        json.GetProperty("agencyStatus")
            .GetString()
            .Should()
            .Be(nameof(AgencyStatus.PendingVerification));

        json.GetProperty("totalListings")
            .GetInt32()
            .Should()
            .Be(0);

        json.GetProperty("draftListings")
            .GetInt32()
            .Should()
            .Be(0);

        json.GetProperty("activeListings")
            .GetInt32()
            .Should()
            .Be(0);

        json.GetProperty("archivedListings")
            .GetInt32()
            .Should()
            .Be(0);

        json.GetProperty("membersCount")
            .GetInt32()
            .Should()
            .Be(1);

        json.GetProperty("activeMembersCount")
            .GetInt32()
            .Should()
            .Be(1);

        json.GetProperty("pendingInvitationsCount")
            .GetInt32()
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task GetAgencyDashboardSummary_ShouldReturnOk_WhenUserIsActiveAgent()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusForDashboardTestAsync(
            agent.UserId,
            UserStatus.Active);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                agent.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAgencyDashboardSummary_ShouldReturnOk_WhenCurrentUserIsPendingVerification()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await SetUserStatusForDashboardTestAsync(
            owner.UserId,
            UserStatus.PendingVerification);

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                owner.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(AgencyStatus.PendingVerification)]
    [InlineData(AgencyStatus.Active)]
    [InlineData(AgencyStatus.Disabled)]
    [InlineData(AgencyStatus.Rejected)]
    public async Task GetAgencyDashboardSummary_ShouldReturnOk_RegardlessOfAgencyStatus(
        AgencyStatus agencyStatus)
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await SetUserStatusForDashboardTestAsync(
            owner.UserId,
            UserStatus.Active);

        await SetAgencyStatusForDashboardTestAsync(
            agencyId,
            agencyStatus);

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                owner.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.GetProperty("agencyStatus")
            .GetString()
            .Should()
            .Be(agencyStatus.ToString());
    }

    [Fact]
    public async Task GetAgencyDashboardSummary_ShouldReturnCorrectListingCounts()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser otherAgencyOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser personalListingOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusForDashboardTestAsync(
            owner.UserId,
            UserStatus.Active);

        await SetUserStatusForDashboardTestAsync(
            otherAgencyOwner.UserId,
            UserStatus.Active);

        await SetUserStatusForDashboardTestAsync(
            personalListingOwner.UserId,
            UserStatus.Active);

        Guid agencyId =
            await CreateAgencyAsAsync(owner);

        Guid otherAgencyId =
            await CreateAgencyAsAsync(otherAgencyOwner);

        Guid draftListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 90000);

        Guid activeListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 100000);

        Guid archivedListingId =
            await CreateAgencyListingAsAsync(
                owner,
                agencyId,
                price: 110000);

        Guid otherAgencyListingId =
            await CreateAgencyListingAsAsync(
                otherAgencyOwner,
                otherAgencyId,
                price: 120000);

        Guid personalListingId =
            await ListingTestHelpers.CreateListingAsAsync(
                _httpClient,
                personalListingOwner);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            draftListingId,
            ListingStatus.Draft);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            activeListingId,
            ListingStatus.Active);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            archivedListingId,
            ListingStatus.Archived);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            otherAgencyListingId,
            ListingStatus.Active);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            personalListingId,
            ListingStatus.Active);

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                owner.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        int totalListings =
            json.GetProperty("totalListings").GetInt32();

        int draftListings =
            json.GetProperty("draftListings").GetInt32();

        int activeListings =
            json.GetProperty("activeListings").GetInt32();

        int archivedListings =
            json.GetProperty("archivedListings").GetInt32();

        totalListings.Should().Be(3);
        draftListings.Should().Be(1);
        activeListings.Should().Be(1);
        archivedListings.Should().Be(1);

        totalListings.Should().Be(
            draftListings +
            activeListings +
            archivedListings);
    }

    [Fact]
    public async Task GetAgencyDashboardSummary_ShouldReturnCorrectMemberCounts()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser activeAgent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser pendingAgent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser disabledAgent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusForDashboardTestAsync(
            owner.UserId,
            UserStatus.Active);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            activeAgent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        await AddAgencyMemberForDashboardTestAsync(
            agencyId,
            pendingAgent.UserId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Pending);

        await AddAgencyMemberForDashboardTestAsync(
            agencyId,
            disabledAgent.UserId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Disabled);

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                owner.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.GetProperty("membersCount")
            .GetInt32()
            .Should()
            .Be(4);

        json.GetProperty("activeMembersCount")
            .GetInt32()
            .Should()
            .Be(2);
    }

    [Fact]
    public async Task GetAgencyDashboardSummary_ShouldExcludeExpiredPendingInvitations()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusForDashboardTestAsync(
            owner.UserId,
            UserStatus.Active);

        Guid agencyId = await CreateAgencyAsAsync(owner);
        Guid otherAgencyId = await CreateAgencyAsAsync(owner);

        DateTime utcNow = DateTime.UtcNow;

        await CreateDashboardSummaryInvitationAsync(
            agencyId,
            owner.UserId,
            AgencyInvitationStatus.Pending,
            utcNow.AddDays(7));

        await CreateDashboardSummaryInvitationAsync(
            agencyId,
            owner.UserId,
            AgencyInvitationStatus.Pending,
            utcNow.AddDays(-1));

        await CreateDashboardSummaryInvitationAsync(
            agencyId,
            owner.UserId,
            AgencyInvitationStatus.Accepted,
            utcNow.AddDays(7));

        await CreateDashboardSummaryInvitationAsync(
            agencyId,
            owner.UserId,
            AgencyInvitationStatus.Cancelled,
            utcNow.AddDays(7));

        await CreateDashboardSummaryInvitationAsync(
            agencyId,
            owner.UserId,
            AgencyInvitationStatus.Expired,
            utcNow.AddDays(-1));

        await CreateDashboardSummaryInvitationAsync(
            otherAgencyId,
            owner.UserId,
            AgencyInvitationStatus.Pending,
            utcNow.AddDays(7));

        // Act
        HttpResponseMessage response =
            await GetAgencyDashboardSummaryAsync(
                owner.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.GetProperty("pendingInvitationsCount")
            .GetInt32()
            .Should()
            .Be(1);
    }

    private async Task<HttpResponseMessage>
        GetAgencyDashboardSummaryAsync(
            string? accessToken,
            Guid agencyId)
    {
        _httpClient.ClearAuthorization();

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            _httpClient.AuthorizeAs(accessToken);
        }

        try
        {
            return await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/dashboard/summary");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task CreateDashboardSummaryInvitationAsync(
    Guid agencyId,
    Guid invitedByUserId,
    AgencyInvitationStatus status,
    DateTime expiresAtUtc)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        DateTime transitionUtc = DateTime.UtcNow;

        string email =
            $"summary-{Guid.NewGuid():N}@test.com";

        var invitation = new AgencyInvitation(
            agencyId: agencyId,
            email: email,
            normalizedEmail: email.ToUpperInvariant(),
            token: Guid.NewGuid().ToString("N"),
            code: Random.Shared
                .Next(0, 1_000_000)
                .ToString("D6"),
            role: AgencyMemberRole.Agent,
            invitedByUserId: invitedByUserId,
            expiresAtUtc: expiresAtUtc);

        switch (status)
        {
            case AgencyInvitationStatus.Pending:
                break;

            case AgencyInvitationStatus.Accepted:
                invitation.Accept(
                    invitedByUserId,
                    transitionUtc);
                break;

            case AgencyInvitationStatus.Cancelled:
                invitation.Cancel(transitionUtc);
                break;

            case AgencyInvitationStatus.Expired:
                invitation.MarkExpired(transitionUtc);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "Unsupported invitation status.");
        }

        dbContext.AgencyInvitations.Add(invitation);

        await dbContext.SaveChangesAsync();
    }
}
