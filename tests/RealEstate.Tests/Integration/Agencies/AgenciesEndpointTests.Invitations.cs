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

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task CreateAgencyInvitation_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Arrange
        _httpClient.ClearAuthorization();

        var request = CreateValidCreateAgencyInvitationRequest();

        // Act
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            $"/api/agencies/{Guid.NewGuid()}/invitations",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAgencyInvitation_ShouldReturnNotFound_WhenAgencyDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyInvitationRequest();

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{Guid.NewGuid()}/invitations",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateAgencyInvitation_ShouldReturnForbidden_WhenUserIsNotAgencyMember()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser nonMember = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyInvitationRequest();

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{agencyId}/invitations",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateAgencyInvitation_ShouldReturnForbidden_WhenMemberIsAgent()
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
            var request = CreateValidCreateAgencyInvitationRequest();

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{agencyId}/invitations",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateAgencyInvitation_ShouldReturnForbidden_WhenMemberIsManager()
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
            var request = CreateValidCreateAgencyInvitationRequest();

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{agencyId}/invitations",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateAgencyInvitation_ShouldCreateInvitation_WhenMemberIsOwner()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        string invitedEmail = $"agent-{Guid.NewGuid():N}@test.com";

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyInvitationRequest(invitedEmail);

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{agencyId}/invitations",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().NotBeEmpty();
            json.GetProperty("agencyId").GetGuid().Should().Be(agencyId);
            json.GetProperty("email").GetString().Should().Be(invitedEmail);
            json.GetProperty("role").GetString().Should().Be(nameof(AgencyMemberRole.Agent));
            json.GetProperty("status").GetString().Should().Be(nameof(AgencyInvitationStatus.Pending));
            json.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
            json.GetProperty("code").GetString().Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateAgencyInvitation_ShouldPersistInvitation_WhenMemberIsOwner()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        string invitedEmail = $"agent-{Guid.NewGuid():N}@test.com";
        string normalizedEmail = invitedEmail.ToUpperInvariant();

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyInvitationRequest(invitedEmail);

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{agencyId}/invitations",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        AgencyInvitation savedInvitation = await dbContext.AgencyInvitations
            .SingleAsync(invitation =>
                invitation.AgencyId == agencyId &&
                invitation.NormalizedEmail == normalizedEmail);

        savedInvitation.Email.Should().Be(invitedEmail);
        savedInvitation.NormalizedEmail.Should().Be(normalizedEmail);
        savedInvitation.Role.Should().Be(AgencyMemberRole.Agent);
        savedInvitation.Status.Should().Be(AgencyInvitationStatus.Pending);
        savedInvitation.InvitedByUserId.Should().Be(owner.UserId);
        savedInvitation.Token.Should().NotBeNullOrWhiteSpace();
        savedInvitation.Code.Should().NotBeNullOrWhiteSpace();
        savedInvitation.ExpiresAtUtc.Should().BeAfter(savedInvitation.CreatedAtUtc);
        savedInvitation.CreatedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task CreateAgencyInvitation_ShouldReturnBadRequest_WhenPendingInvitationAlreadyExists()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        string invitedEmail = $"agent-{Guid.NewGuid():N}@test.com";

        await CreatePendingAgencyInvitationAsync(
            agencyId,
            owner.UserId,
            invitedEmail);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyInvitationRequest(invitedEmail);

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{agencyId}/invitations",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateAgencyInvitation_ShouldReturnBadRequest_WhenInvitedUserIsAlreadyMember()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser existingMember = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            existingMember.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyInvitationRequest(existingMember.Email);

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{agencyId}/invitations",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateAgencyInvitation_ShouldReturnBadRequest_WhenInvitationRoleIsManager()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyInvitationRequest(
                role: AgencyMemberRole.Manager);

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{agencyId}/invitations",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateAgencyInvitation_ShouldReturnBadRequest_WhenEmailIsInvalid()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyInvitationRequest("not-valid-email");

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{agencyId}/invitations",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateAgencyInvitation_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await DisableUserAsync(owner.UserId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyInvitationRequest();

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{agencyId}/invitations",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private static object CreateValidCreateAgencyInvitationRequest(
        string? email = null,
        AgencyMemberRole role = AgencyMemberRole.Agent)
    {
        return new
        {
            email = email ?? $"agent-{Guid.NewGuid():N}@test.com",
            role
        };
    }

    private async Task CreatePendingAgencyInvitationAsync(
        Guid agencyId,
        Guid invitedByUserId,
        string email)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        var invitation = new AgencyInvitation(
            agencyId: agencyId,
            email: email,
            normalizedEmail: email.ToUpperInvariant(),
            token: Guid.NewGuid().ToString("N"),
            code: "123456",
            role: AgencyMemberRole.Agent,
            invitedByUserId: invitedByUserId,
            expiresAtUtc: DateTime.UtcNow.AddDays(7));

        dbContext.AgencyInvitations.Add(invitation);

        await dbContext.SaveChangesAsync();
    }

    private async Task DisableUserAsync(Guid userId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(user => user.Status, UserStatus.Disabled));
    }
}
