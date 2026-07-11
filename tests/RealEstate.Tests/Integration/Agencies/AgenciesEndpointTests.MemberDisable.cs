using System.Net;
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
    public async Task DisableAgencyMember_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Arrange
        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response = await _httpClient.PutAsync(
            $"/api/agencies/{Guid.NewGuid()}/members/{Guid.NewGuid()}/disable",
            content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnNotFound_WhenAgencyDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{Guid.NewGuid()}/members/{Guid.NewGuid()}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnForbidden_WhenUserIsNotAgencyMember()
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

        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{ownerMemberId}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnForbidden_WhenCurrentMemberIsAgent()
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

        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{ownerMemberId}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnForbidden_WhenCurrentMemberIsManager()
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

        _httpClient.AuthorizeAs(manager.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{ownerMemberId}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active);

        Guid agentMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            agent.UserId);

        await DisableUserAsync(owner.UserId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{agentMemberId}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnNotFound_WhenMemberDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{Guid.NewGuid()}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnNotFound_WhenMemberBelongsToDifferentAgency()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid firstAgencyId = await CreateAgencyAsAsync(owner);
        Guid secondAgencyId = await CreateAgencyAsAsync(owner);

        Guid secondAgencyMemberId = await GetAgencyMemberIdAsync(
            secondAgencyId,
            owner.UserId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{firstAgencyId}/members/{secondAgencyMemberId}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnBadRequest_WhenOwnerTargetsSelf()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        Guid ownerMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            owner.UserId);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{ownerMemberId}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        AgencyMemberStatus savedStatus = await GetAgencyMemberStatusAsync(
            agencyId,
            owner.UserId);

        savedStatus.Should().Be(AgencyMemberStatus.Active);
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnNoContent_WhenTargetIsActiveAgent()
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

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{agentMemberId}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldPersistDisabledStatus_WhenTargetIsActiveAgent()
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

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{agentMemberId}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        AgencyMemberStatus savedStatus = await GetAgencyMemberStatusAsync(
            agencyId,
            agent.UserId);

        savedStatus.Should().Be(AgencyMemberStatus.Disabled);
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnNoContent_WhenTargetIsPending()
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

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{pendingMemberId}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        AgencyMemberStatus savedStatus = await GetAgencyMemberStatusAsync(
            agencyId,
            pendingMember.UserId);

        savedStatus.Should().Be(AgencyMemberStatus.Disabled);
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnNoContent_WhenTargetIsManager()
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

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{managerMemberId}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        AgencyMemberStatus savedStatus = await GetAgencyMemberStatusAsync(
            agencyId,
            manager.UserId);

        savedStatus.Should().Be(AgencyMemberStatus.Disabled);
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldReturnNoContent_WhenTargetIsAnotherActiveOwner()
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

        Guid secondOwnerMemberId = await GetAgencyMemberIdAsync(
            agencyId,
            secondOwner.UserId);

        _httpClient.AuthorizeAs(firstOwner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{secondOwnerMemberId}/disable",
                content: null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        AgencyMemberStatus firstOwnerStatus =
            await GetAgencyMemberStatusAsync(
                agencyId,
                firstOwner.UserId);

        AgencyMemberStatus secondOwnerStatus =
            await GetAgencyMemberStatusAsync(
                agencyId,
                secondOwner.UserId);

        firstOwnerStatus.Should().Be(AgencyMemberStatus.Active);
        secondOwnerStatus.Should().Be(AgencyMemberStatus.Disabled);
    }

    [Fact]
    public async Task DisableAgencyMember_ShouldBeIdempotent_WhenTargetIsAlreadyDisabled()
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

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            // Act
            HttpResponseMessage firstResponse = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{disabledMemberId}/disable",
                content: null);

            HttpResponseMessage secondResponse = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{disabledMemberId}/disable",
                content: null);

            // Assert
            firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
            secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        AgencyMemberStatus savedStatus = await GetAgencyMemberStatusAsync(
            agencyId,
            disabledMember.UserId);

        savedStatus.Should().Be(AgencyMemberStatus.Disabled);
    }

    private async Task<Guid> GetAgencyMemberIdAsync(
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
            .Select(member => member.Id)
            .SingleAsync();
    }

    private async Task<AgencyMemberStatus> GetAgencyMemberStatusAsync(
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
            .Select(member => member.Status)
            .SingleAsync();
    }
}
