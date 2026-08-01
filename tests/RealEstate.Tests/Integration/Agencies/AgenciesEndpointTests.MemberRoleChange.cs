using System.Net;
using System.Net.Http.Json;
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
    public async Task ChangeAgencyMemberRole_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Arrange
        _httpClient.ClearAuthorization();

        var request = new
        {
            role = AgencyMemberRole.Owner
        };

        // Act
        HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
            $"/api/agencies/{Guid.NewGuid()}/members/{Guid.NewGuid()}/role",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnNotFound_WhenAgencyDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        var request = new
        {
            role = AgencyMemberRole.Owner
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            user.AccessToken,
            Guid.NewGuid(),
            Guid.NewGuid(),
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnForbidden_WhenUserIsNotAgencyMember()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser nonMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        Guid ownerMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            owner.UserId);

        var request = new
        {
            role = AgencyMemberRole.Owner
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            nonMember.AccessToken,
            agencyId,
            ownerMemberId,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnForbidden_WhenCurrentMemberIsAgent()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        Guid ownerMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            owner.UserId);

        var request = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            agent.AccessToken,
            agencyId,
            ownerMemberId,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        Guid agentMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            agent.UserId);

        await DisableUserAsync(owner.UserId);

        var request = new
        {
            role = AgencyMemberRole.Owner
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            owner.AccessToken,
            agencyId,
            agentMemberId,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnNotFound_WhenMemberDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        var request = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            owner.AccessToken,
            agencyId,
            Guid.NewGuid(),
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnNotFound_WhenMemberBelongsToDifferentAgency()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid firstAgencyId = await CreateAgencyAsAsync(owner);
        Guid secondAgencyId = await CreateAgencyAsAsync(owner);

        Guid secondAgencyMemberId = await GetAgencyMemberIdAsync(
            secondAgencyId,
            owner.UserId);

        var request = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            owner.AccessToken,
            firstAgencyId,
            secondAgencyMemberId,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnBadRequest_WhenRequestedRoleIsManager()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        Guid agentMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            agent.UserId);

        var request = new
        {
            role = AgencyMemberRole.Manager
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            owner.AccessToken,
            agencyId,
            agentMemberId,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        AgencyMemberRole savedRole = await GetAgencyMemberRoleAsync(
            agencyId,
            agent.UserId);

        savedRole.Should().Be(AgencyMemberRole.Agent);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnBadRequest_WhenRequestedRoleIsUndefined()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        Guid agentMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            agent.UserId);

        var request = new
        {
            role = 999
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            owner.AccessToken,
            agencyId,
            agentMemberId,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        AgencyMemberRole savedRole = await GetAgencyMemberRoleAsync(
            agencyId,
            agent.UserId);

        savedRole.Should().Be(AgencyMemberRole.Agent);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnConflict_WhenTargetIsPending()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser pendingMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            pendingMember.UserId,
            AgencyMemberStatus.Pending,
            AgencyMemberRole.Agent);

        Guid pendingMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            pendingMember.UserId);

        var request = new
        {
            role = AgencyMemberRole.Owner
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            owner.AccessToken,
            agencyId,
            pendingMemberId,
            request);

        // Assert
        await AssertResourceStateConflictAsync(
            response,
            $"/api/agencies/{agencyId}/members/{pendingMemberId}/role");

        AgencyMemberRole savedRole = await GetAgencyMemberRoleAsync(
            agencyId,
            pendingMember.UserId);

        savedRole.Should().Be(AgencyMemberRole.Agent);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnConflict_WhenTargetIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser disabledMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            disabledMember.UserId,
            AgencyMemberStatus.Disabled,
            AgencyMemberRole.Agent);

        Guid disabledMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            disabledMember.UserId);

        var request = new
        {
            role = AgencyMemberRole.Owner
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            owner.AccessToken,
            agencyId,
            disabledMemberId,
            request);

        // Assert
        await AssertResourceStateConflictAsync(
            response,
            $"/api/agencies/{agencyId}/members/{disabledMemberId}/role");

        AgencyMemberRole savedRole = await GetAgencyMemberRoleAsync(
            agencyId,
            disabledMember.UserId);

        savedRole.Should().Be(AgencyMemberRole.Agent);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldPromoteAgentToOwner()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        Guid agentMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            agent.UserId);

        var request = new
        {
            role = AgencyMemberRole.Owner
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            owner.AccessToken,
            agencyId,
            agentMemberId,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        AgencyMemberRole savedRole = await GetAgencyMemberRoleAsync(
            agencyId,
            agent.UserId);

        savedRole.Should().Be(AgencyMemberRole.Owner);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnConflict_WhenSoleOwnerDemotesSelf()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        Guid ownerMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            owner.UserId);

        var request = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            owner.AccessToken,
            agencyId,
            ownerMemberId,
            request);

        // Assert
        await AssertResourceStateConflictAsync(
            response,
            $"/api/agencies/{agencyId}/members/{ownerMemberId}/role");

        AgencyMemberRole savedRole = await GetAgencyMemberRoleAsync(
            agencyId,
            owner.UserId);

        savedRole.Should().Be(AgencyMemberRole.Owner);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldAllowOwnerToDemoteSelf_WhenAnotherActiveOwnerExists()
    {
        // Arrange
        AuthenticatedTestUser firstOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser secondOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            firstOwner.UserId,
            secondOwner.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Owner);

        Guid firstOwnerMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            firstOwner.UserId);

        var request = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            firstOwner.AccessToken,
            agencyId,
            firstOwnerMemberId,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        AgencyMemberRole firstOwnerRole = await GetAgencyMemberRoleAsync(
            agencyId,
            firstOwner.UserId);

        AgencyMemberRole secondOwnerRole = await GetAgencyMemberRoleAsync(
            agencyId,
            secondOwner.UserId);

        firstOwnerRole.Should().Be(AgencyMemberRole.Agent);
        secondOwnerRole.Should().Be(AgencyMemberRole.Owner);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldBeIdempotent_WhenAgentRoleIsUnchanged()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        Guid agentMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            agent.UserId);

        var request = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        HttpResponseMessage firstResponse = await PutMemberRoleAsync(
            owner.AccessToken,
            agencyId,
            agentMemberId,
            request);

        HttpResponseMessage secondResponse = await PutMemberRoleAsync(
            owner.AccessToken,
            agencyId,
            agentMemberId,
            request);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        AgencyMemberRole savedRole = await GetAgencyMemberRoleAsync(
            agencyId,
            agent.UserId);

        savedRole.Should().Be(AgencyMemberRole.Agent);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldChangeExistingManagerToAgent()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser manager =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            manager.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Manager);

        Guid managerMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            manager.UserId);

        var request = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            owner.AccessToken,
            agencyId,
            managerMemberId,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        AgencyMemberRole savedRole = await GetAgencyMemberRoleAsync(
            agencyId,
            manager.UserId);

        savedRole.Should().Be(AgencyMemberRole.Agent);
    }

    [Fact]
    public async Task ChangeAgencyMemberRole_ShouldReturnForbidden_WhenCurrentMemberIsManager()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser manager =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            manager.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Manager);

        Guid ownerMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            owner.UserId);

        var request = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            manager.AccessToken,
            agencyId,
            ownerMemberId,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(AgencyMemberStatus.Pending)]
    [InlineData(AgencyMemberStatus.Disabled)]
    public async Task ChangeAgencyMemberRole_ShouldReturnForbidden_WhenCurrentOwnerIsNotActive(
    AgencyMemberStatus currentOwnerStatus)
    {
        // Arrange
        AuthenticatedTestUser activeOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser inactiveOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            activeOwner.UserId,
            inactiveOwner.UserId,
            currentOwnerStatus,
            AgencyMemberRole.Owner);

        Guid activeOwnerMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            activeOwner.UserId);

        var request = new
        {
            role = AgencyMemberRole.Agent
        };

        // Act
        HttpResponseMessage response = await PutMemberRoleAsync(
            inactiveOwner.AccessToken,
            agencyId,
            activeOwnerMemberId,
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> PutMemberRoleAsync(
        string accessToken,
        Guid agencyId,
        Guid memberId,
        object request)
    {
        _httpClient.AuthorizeAs(accessToken);

        try
        {
            return await _httpClient.PutAsJsonAsync(
                $"/api/agencies/{agencyId}/members/{memberId}/role",
                request);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<AgencyMemberRole> GetAgencyMemberRoleAsync(
        Guid agencyId,
        Guid userId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        return await dbContext.Set<AgencyMember>()
            .AsNoTracking()
            .Where(member =>
                member.AgencyId == agencyId &&
                member.UserId == userId)
            .Select(member => member.Role)
            .SingleAsync();
    }
}
