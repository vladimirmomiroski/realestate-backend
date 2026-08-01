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
    public async Task AcceptAgencyInvitation_ShouldReturnUnauthorized_WhenNoAccessToken()
    {
        // Arrange
        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
            "/api/agencies/invitations/accept",
            new { token = Guid.NewGuid().ToString("N") });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldReturnBadRequest_WhenTokenIsMissing()
    {
        // Arrange
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = " " });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldReturnOk_WhenTokenAndEmailMatch()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await SetUserStatusForInvitationAcceptAsync(invitedUser.UserId, UserStatus.Active);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            invitedUser.Email);

        _httpClient.AuthorizeAs(invitedUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = seed.Token });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().Be(seed.InvitationId);
            json.GetProperty("agencyId").GetGuid().Should().Be(agencyId);
            json.GetProperty("email").GetString().Should().Be(invitedUser.Email);
            json.GetProperty("role").GetString().Should().Be(nameof(AgencyMemberRole.Agent));
            json.GetProperty("status").GetString().Should().Be(nameof(AgencyInvitationStatus.Accepted));
            json.GetProperty("acceptedByUserId").GetGuid().Should().Be(invitedUser.UserId);
            json.TryGetProperty("token", out _).Should().BeFalse();
            json.TryGetProperty("code", out _).Should().BeFalse();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldCreateActiveAgencyMember()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            invitedUser.Email,
            role: AgencyMemberRole.Owner);

        _httpClient.AuthorizeAs(invitedUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = seed.Token });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        AgencyMember member = await dbContext.Set<AgencyMember>()
            .SingleAsync(member =>
                member.AgencyId == agencyId &&
                member.UserId == invitedUser.UserId);

        member.Role.Should().Be(AgencyMemberRole.Owner);
        member.Status.Should().Be(AgencyMemberStatus.Active);
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldMarkInvitationAccepted()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            invitedUser.Email);

        _httpClient.AuthorizeAs(invitedUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = seed.Token });

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

        invitation.Status.Should().Be(AgencyInvitationStatus.Accepted);
        invitation.AcceptedByUserId.Should().Be(invitedUser.UserId);
        invitation.AcceptedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldReturnOk_WhenCurrentUserIsPendingVerification()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            invitedUser.Email);

        _httpClient.AuthorizeAs(invitedUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = seed.Token });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            invitedUser.Email);

        await SetUserStatusForInvitationAcceptAsync(invitedUser.UserId, UserStatus.Disabled);

        _httpClient.AuthorizeAs(invitedUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = seed.Token });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldReturnForbidden_WhenEmailDoesNotMatch()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            $"other-{Guid.NewGuid():N}@test.com");

        _httpClient.AuthorizeAs(invitedUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = seed.Token });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldReturnNotFound_WhenTokenIsUnknown()
    {
        // Arrange
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = Guid.NewGuid().ToString("N") });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldReturnConflict_WhenInvitationIsAlreadyAccepted()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            invitedUser.Email,
            status: AgencyInvitationStatus.Accepted,
            acceptedByUserId: invitedUser.UserId);

        _httpClient.AuthorizeAs(invitedUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = seed.Token });

            // Assert
            await AssertResourceStateConflictAsync(
                response,
                "/api/agencies/invitations/accept");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldReturnConflict_WhenInvitationIsCancelled()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            invitedUser.Email,
            status: AgencyInvitationStatus.Cancelled);

        _httpClient.AuthorizeAs(invitedUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = seed.Token });

            // Assert
            await AssertResourceStateConflictAsync(
                response,
                "/api/agencies/invitations/accept");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldReturnConflict_WhenInvitationIsExpired()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            invitedUser.Email,
            status: AgencyInvitationStatus.Expired);

        _httpClient.AuthorizeAs(invitedUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = seed.Token });

            // Assert
            await AssertResourceStateConflictAsync(
                response,
                "/api/agencies/invitations/accept");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldMarkInvitationExpired_WhenExpiryDateHasPassed()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            invitedUser.Email,
            expiresAtUtc: DateTime.UtcNow.AddDays(-1));

        _httpClient.AuthorizeAs(invitedUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = seed.Token });

            // Assert
            await AssertResourceStateConflictAsync(
                response,
                "/api/agencies/invitations/accept");
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

    [Fact]
    public async Task AcceptAgencyInvitation_ShouldReturnConflict_WhenUserIsAlreadyMember()
    {
        // Arrange
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            invitedUser.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            invitedUser.Email);

        _httpClient.AuthorizeAs(invitedUser.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = seed.Token });

            // Assert
            await AssertResourceStateConflictAsync(
                response,
                "/api/agencies/invitations/accept");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<AcceptInvitationSeed> CreateInvitationForAcceptAsync(
        Guid agencyId,
        Guid invitedByUserId,
        string email,
        AgencyInvitationStatus status = AgencyInvitationStatus.Pending,
        DateTime? expiresAtUtc = null,
        AgencyMemberRole role = AgencyMemberRole.Agent,
        Guid? acceptedByUserId = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        DateTime utcNow = DateTime.UtcNow;
        string token = Guid.NewGuid().ToString("N");
        DateTime invitationExpiresAtUtc = expiresAtUtc ??
            (status == AgencyInvitationStatus.Expired
                ? utcNow.AddDays(-1)
                : utcNow.AddDays(7));

        var invitation = new AgencyInvitation(
            agencyId: agencyId,
            email: email,
            normalizedEmail: email.ToUpperInvariant(),
            token: token,
            code: Random.Shared.Next(0, 1_000_000).ToString("D6"),
            role: role,
            invitedByUserId: invitedByUserId,
            expiresAtUtc: invitationExpiresAtUtc);

        if (status == AgencyInvitationStatus.Accepted)
        {
            invitation.Accept(acceptedByUserId ?? invitedByUserId, utcNow);
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

        return new AcceptInvitationSeed(
            agencyId,
            invitation.Id,
            token);
    }

    private async Task SetUserStatusForInvitationAcceptAsync(
        Guid userId,
        UserStatus status)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        await dbContext.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(user => user.Status, status));
    }

    private sealed record AcceptInvitationSeed(
        Guid AgencyId,
        Guid InvitationId,
        string Token);
}
