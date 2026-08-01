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
    public async Task CancelAgencyInvitation_ShouldReturnUnauthorized_WhenNoAccessToken()
    {
        // Arrange
        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response = await _httpClient.PutAsync(
            $"/api/agencies/{Guid.NewGuid()}/invitations/{Guid.NewGuid()}/cancel",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldReturnOk_WhenMemberIsOwner()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        CancelInvitationSeed seed = await CreateInvitationForCancelAsync(
            agencyId,
            owner.UserId,
            $"agent-{Guid.NewGuid():N}@test.com");

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel",
                null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().Be(seed.InvitationId);
            json.GetProperty("agencyId").GetGuid().Should().Be(agencyId);
            json.GetProperty("status").GetString().Should().Be(nameof(AgencyInvitationStatus.Cancelled));
            json.TryGetProperty("token", out _).Should().BeFalse();
            json.TryGetProperty("code", out _).Should().BeFalse();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldMarkInvitationCancelled()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        CancelInvitationSeed seed = await CreateInvitationForCancelAsync(
            agencyId,
            owner.UserId,
            $"agent-{Guid.NewGuid():N}@test.com");

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel",
                null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        AgencyInvitation invitation = await dbContext.AgencyInvitations
            .SingleAsync(invitation => invitation.Id == seed.InvitationId);

        invitation.Status.Should().Be(AgencyInvitationStatus.Cancelled);
        invitation.CancelledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldReturnForbidden_WhenMemberIsAgent()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser agent = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        CancelInvitationSeed seed = await CreateInvitationForCancelAsync(
            agencyId,
            owner.UserId,
            $"invite-{Guid.NewGuid():N}@test.com");

        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel",
                null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldReturnForbidden_WhenMemberIsManager()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser manager = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            manager.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Manager);

        CancelInvitationSeed seed = await CreateInvitationForCancelAsync(
            agencyId,
            owner.UserId,
            $"invite-{Guid.NewGuid():N}@test.com");

        _httpClient.AuthorizeAs(manager.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel",
                null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldReturnForbidden_WhenUserIsNotAgencyMember()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser nonMember = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        CancelInvitationSeed seed = await CreateInvitationForCancelAsync(
            agencyId,
            owner.UserId,
            $"invite-{Guid.NewGuid():N}@test.com");

        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel",
                null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        CancelInvitationSeed seed = await CreateInvitationForCancelAsync(
            agencyId,
            owner.UserId,
            $"invite-{Guid.NewGuid():N}@test.com");

        await DisableUserAsync(owner.UserId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel",
                null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldReturnNotFound_WhenAgencyDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{Guid.NewGuid()}/invitations/{Guid.NewGuid()}/cancel",
                null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldReturnNotFound_WhenInvitationDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{Guid.NewGuid()}/cancel",
                null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldReturnNotFound_WhenInvitationBelongsToDifferentAgency()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid routeAgencyId = await CreateAgencyAsAsync(owner);
        Guid invitationAgencyId = await CreateAgencyAsAsync(owner);

        CancelInvitationSeed seed = await CreateInvitationForCancelAsync(
            invitationAgencyId,
            owner.UserId,
            $"invite-{Guid.NewGuid():N}@test.com");

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{routeAgencyId}/invitations/{seed.InvitationId}/cancel",
                null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldReturnConflict_WhenInvitationIsAccepted()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        CancelInvitationSeed seed = await CreateInvitationForCancelAsync(
            agencyId,
            owner.UserId,
            $"invite-{Guid.NewGuid():N}@test.com",
            status: AgencyInvitationStatus.Accepted);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel",
                null);

            // Assert
            await AssertResourceStateConflictAsync(
                response,
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldReturnConflict_WhenInvitationIsCancelled()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        CancelInvitationSeed seed = await CreateInvitationForCancelAsync(
            agencyId,
            owner.UserId,
            $"invite-{Guid.NewGuid():N}@test.com",
            status: AgencyInvitationStatus.Cancelled);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel",
                null);

            // Assert
            await AssertResourceStateConflictAsync(
                response,
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldReturnConflict_WhenInvitationIsExpired()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        CancelInvitationSeed seed = await CreateInvitationForCancelAsync(
            agencyId,
            owner.UserId,
            $"invite-{Guid.NewGuid():N}@test.com",
            status: AgencyInvitationStatus.Expired);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel",
                null);

            // Assert
            await AssertResourceStateConflictAsync(
                response,
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CancelAgencyInvitation_ShouldMarkInvitationExpired_WhenPendingInvitationExpiryDateHasPassed()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        CancelInvitationSeed seed = await CreateInvitationForCancelAsync(
            agencyId,
            owner.UserId,
            $"invite-{Guid.NewGuid():N}@test.com",
            expiresAtUtc: DateTime.UtcNow.AddDays(-1));

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel",
                null);

            // Assert
            await AssertResourceStateConflictAsync(
                response,
                $"/api/agencies/{agencyId}/invitations/{seed.InvitationId}/cancel");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        AgencyInvitation invitation = await dbContext.AgencyInvitations
            .SingleAsync(invitation => invitation.Id == seed.InvitationId);

        invitation.Status.Should().Be(AgencyInvitationStatus.Expired);
    }

    private async Task<CancelInvitationSeed> CreateInvitationForCancelAsync(
        Guid agencyId,
        Guid invitedByUserId,
        string email,
        AgencyInvitationStatus status = AgencyInvitationStatus.Pending,
        DateTime? expiresAtUtc = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        DateTime utcNow = DateTime.UtcNow;
        DateTime invitationExpiresAtUtc = expiresAtUtc ??
            (status == AgencyInvitationStatus.Expired
                ? utcNow.AddDays(-1)
                : utcNow.AddDays(7));

        var invitation = new AgencyInvitation(
            agencyId: agencyId,
            email: email,
            normalizedEmail: email.ToUpperInvariant(),
            token: Guid.NewGuid().ToString("N"),
            code: Random.Shared.Next(0, 1_000_000).ToString("D6"),
            role: AgencyMemberRole.Agent,
            invitedByUserId: invitedByUserId,
            expiresAtUtc: invitationExpiresAtUtc);

        if (status == AgencyInvitationStatus.Accepted)
        {
            invitation.Accept(invitedByUserId, utcNow);
        }

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

        return new CancelInvitationSeed(invitation.Id);
    }

    private sealed record CancelInvitationSeed(Guid InvitationId);
}
