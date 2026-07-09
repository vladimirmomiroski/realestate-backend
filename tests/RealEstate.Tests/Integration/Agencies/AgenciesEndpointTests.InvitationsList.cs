using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Arrange
        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{Guid.NewGuid()}/invitations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnNotFound_WhenAgencyDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{Guid.NewGuid()}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnForbidden_WhenUserIsNotAgencyMember()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser nonMember = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnForbidden_WhenMemberIsAgent()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser agent = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnForbidden_WhenMemberIsManager()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser manager = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            manager.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Manager);

        _httpClient.AuthorizeAs(manager.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await DisableUserAsync(owner.UserId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnOk_WhenMemberIsOwner()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.ValueKind.Should().Be(JsonValueKind.Array);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldReturnAgencyInvitations()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        string invitedEmail = $"agent-{Guid.NewGuid():N}@test.com";

        await CreateAgencyInvitationForListAsync(
            agencyId,
            owner.UserId,
            invitedEmail,
            AgencyInvitationStatus.Pending);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetArrayLength().Should().Be(1);

            JsonElement invitation = json.EnumerateArray().Single();

            invitation.GetProperty("agencyId").GetGuid().Should().Be(agencyId);
            invitation.GetProperty("email").GetString().Should().Be(invitedEmail);
            invitation.GetProperty("role").GetString().Should().Be(nameof(AgencyMemberRole.Agent));
            invitation.GetProperty("status").GetString().Should().Be(nameof(AgencyInvitationStatus.Pending));
            invitation.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
            invitation.GetProperty("code").GetString().Should().NotBeNullOrWhiteSpace();
            invitation.GetProperty("invitedByUserId").GetGuid().Should().Be(owner.UserId);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldNotReturnInvitationsFromAnotherAgency()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid firstAgencyId = await CreateAgencyAsAsync(owner);
        Guid secondAgencyId = await CreateAgencyAsAsync(owner);

        string firstEmail = $"first-{Guid.NewGuid():N}@test.com";
        string secondEmail = $"second-{Guid.NewGuid():N}@test.com";

        await CreateAgencyInvitationForListAsync(
            firstAgencyId,
            owner.UserId,
            firstEmail,
            AgencyInvitationStatus.Pending);

        await CreateAgencyInvitationForListAsync(
            secondAgencyId,
            owner.UserId,
            secondEmail,
            AgencyInvitationStatus.Pending);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{firstAgencyId}/invitations");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetArrayLength().Should().Be(1);

            JsonElement invitation = json.EnumerateArray().Single();

            invitation.GetProperty("agencyId").GetGuid().Should().Be(firstAgencyId);
            invitation.GetProperty("email").GetString().Should().Be(firstEmail);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyInvitations_ShouldFilterByStatus()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        string pendingEmail = $"pending-{Guid.NewGuid():N}@test.com";
        string cancelledEmail = $"cancelled-{Guid.NewGuid():N}@test.com";

        await CreateAgencyInvitationForListAsync(
            agencyId,
            owner.UserId,
            pendingEmail,
            AgencyInvitationStatus.Pending);

        await CreateAgencyInvitationForListAsync(
            agencyId,
            owner.UserId,
            cancelledEmail,
            AgencyInvitationStatus.Cancelled);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations?status=Pending");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetArrayLength().Should().Be(1);

            JsonElement invitation = json.EnumerateArray().Single();

            invitation.GetProperty("email").GetString().Should().Be(pendingEmail);
            invitation.GetProperty("status").GetString().Should().Be(nameof(AgencyInvitationStatus.Pending));
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task CreateAgencyInvitationForListAsync(
        Guid agencyId,
        Guid invitedByUserId,
        string email,
        AgencyInvitationStatus status)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        DateTime utcNow = DateTime.UtcNow;

        var invitation = new AgencyInvitation(
            agencyId: agencyId,
            email: email,
            normalizedEmail: email.ToUpperInvariant(),
            token: Guid.NewGuid().ToString("N"),
            code: Random.Shared.Next(0, 1_000_000).ToString("D6"),
            role: AgencyMemberRole.Agent,
            invitedByUserId: invitedByUserId,
            expiresAtUtc: status == AgencyInvitationStatus.Expired
                ? utcNow.AddDays(-1)
                : utcNow.AddDays(7));

        if (status == AgencyInvitationStatus.Cancelled)
        {
            invitation.Cancel(utcNow);
        }

        if (status == AgencyInvitationStatus.Expired)
        {
            invitation.MarkExpired(utcNow);
        }

        dbContext.AgencyInvitations.Add(invitation);

        await dbContext.SaveChangesAsync();
    }
}
