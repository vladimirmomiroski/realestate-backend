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
    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("disable")]
    public async Task AdminAgencyAction_ShouldReturnUnauthorized_WhenNoToken(
        string action)
    {
        // Arrange
        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                accessToken: null,
                Guid.NewGuid(),
                action);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("disable")]
    public async Task AdminAgencyAction_ShouldReturnForbidden_WhenUserIsNotAdmin(
        string action)
    {
        // Arrange
        AuthenticatedTestUser user =
            await CreateUserWithPlatformAccessAsync(
                UserRole.User,
                UserStatus.Active);

        Guid agencyId =
            await CreateAgencyForAdminVerificationAsync(
                AgencyStatus.PendingVerification);

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                user.AccessToken,
                agencyId,
                action);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("disable")]
    public async Task AdminAgencyAction_ShouldReturnForbidden_WhenAdminIsPendingVerification(
        string action)
    {
        // Arrange
        AuthenticatedTestUser admin =
            await CreateUserWithPlatformAccessAsync(
                UserRole.Admin,
                UserStatus.PendingVerification);

        Guid agencyId =
            await CreateAgencyForAdminVerificationAsync(
                AgencyStatus.PendingVerification);

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                admin.AccessToken,
                agencyId,
                action);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("disable")]
    public async Task AdminAgencyAction_ShouldReturnForbidden_WhenAdminIsDisabled(
        string action)
    {
        // Arrange
        AuthenticatedTestUser admin =
            await CreateUserWithPlatformAccessAsync(
                UserRole.Admin,
                UserStatus.Disabled);

        Guid agencyId =
            await CreateAgencyForAdminVerificationAsync(
                AgencyStatus.PendingVerification);

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                admin.AccessToken,
                agencyId,
                action);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("disable")]
    public async Task AdminAgencyAction_ShouldReturnNotFound_WhenAgencyDoesNotExist(
        string action)
    {
        // Arrange
        AuthenticatedTestUser admin =
            await CreateUserWithPlatformAccessAsync(
                UserRole.Admin,
                UserStatus.Active);

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                admin.AccessToken,
                Guid.NewGuid(),
                action);

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(AgencyStatus.PendingVerification)]
    [InlineData(AgencyStatus.Rejected)]
    [InlineData(AgencyStatus.Active)]
    public async Task ApproveAgency_ShouldReturnActive_WhenTransitionIsAllowedOrIdempotent(
        AgencyStatus initialStatus)
    {
        // Arrange
        AuthenticatedTestUser admin =
            await CreateUserWithPlatformAccessAsync(
                UserRole.Admin,
                UserStatus.Active);

        Guid agencyId =
            await CreateAgencyForAdminVerificationAsync(
                initialStatus);

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                admin.AccessToken,
                agencyId,
                "approve");

        // Assert
        await AssertSuccessfulAgencyStatusChangeAsync(
            response,
            agencyId,
            AgencyStatus.Active);
    }

    [Fact]
    public async Task ApproveAgency_ShouldReturnBadRequest_WhenAgencyIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser admin =
            await CreateUserWithPlatformAccessAsync(
                UserRole.Admin,
                UserStatus.Active);

        Guid agencyId =
            await CreateAgencyForAdminVerificationAsync(
                AgencyStatus.Disabled);

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                admin.AccessToken,
                agencyId,
                "approve");

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);

        await AssertPersistedAgencyStatusAsync(
            agencyId,
            AgencyStatus.Disabled);
    }

    [Theory]
    [InlineData(AgencyStatus.PendingVerification)]
    [InlineData(AgencyStatus.Rejected)]
    public async Task RejectAgency_ShouldReturnRejected_WhenTransitionIsAllowedOrIdempotent(
        AgencyStatus initialStatus)
    {
        // Arrange
        AuthenticatedTestUser admin =
            await CreateUserWithPlatformAccessAsync(
                UserRole.Admin,
                UserStatus.Active);

        Guid agencyId =
            await CreateAgencyForAdminVerificationAsync(
                initialStatus);

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                admin.AccessToken,
                agencyId,
                "reject");

        // Assert
        await AssertSuccessfulAgencyStatusChangeAsync(
            response,
            agencyId,
            AgencyStatus.Rejected);
    }

    [Theory]
    [InlineData(AgencyStatus.Active)]
    [InlineData(AgencyStatus.Disabled)]
    public async Task RejectAgency_ShouldReturnBadRequest_WhenTransitionIsNotAllowed(
        AgencyStatus initialStatus)
    {
        // Arrange
        AuthenticatedTestUser admin =
            await CreateUserWithPlatformAccessAsync(
                UserRole.Admin,
                UserStatus.Active);

        Guid agencyId =
            await CreateAgencyForAdminVerificationAsync(
                initialStatus);

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                admin.AccessToken,
                agencyId,
                "reject");

        // Assert
        response.StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);

        await AssertPersistedAgencyStatusAsync(
            agencyId,
            initialStatus);
    }

    [Theory]
    [InlineData(AgencyStatus.PendingVerification)]
    [InlineData(AgencyStatus.Active)]
    [InlineData(AgencyStatus.Rejected)]
    [InlineData(AgencyStatus.Disabled)]
    public async Task DisableAgency_ShouldReturnDisabled_WhenTransitionIsAllowedOrIdempotent(
        AgencyStatus initialStatus)
    {
        // Arrange
        AuthenticatedTestUser admin =
            await CreateUserWithPlatformAccessAsync(
                UserRole.Admin,
                UserStatus.Active);

        Guid agencyId =
            await CreateAgencyForAdminVerificationAsync(
                initialStatus);

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                admin.AccessToken,
                agencyId,
                "disable");

        // Assert
        await AssertSuccessfulAgencyStatusChangeAsync(
            response,
            agencyId,
            AgencyStatus.Disabled);
    }

    private async Task<AuthenticatedTestUser>
        CreateUserWithPlatformAccessAsync(
            UserRole role,
            UserStatus status)
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        await dbContext.Users
            .Where(currentUser =>
                currentUser.Id == user.UserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    currentUser => currentUser.Role,
                    role)
                .SetProperty(
                    currentUser => currentUser.Status,
                    status));

        return user;
    }

    private async Task<Guid>
        CreateAgencyForAdminVerificationAsync(
            AgencyStatus initialStatus)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        Agency agency = AgencyTestHelpers.CreateAgency();

        switch (initialStatus)
        {
            case AgencyStatus.PendingVerification:
                break;

            case AgencyStatus.Active:
                agency.Approve();
                break;

            case AgencyStatus.Rejected:
                agency.Reject();
                break;

            case AgencyStatus.Disabled:
                agency.Disable();
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(initialStatus),
                    initialStatus,
                    "Unsupported agency status.");
        }

        dbContext.Agencies.Add(agency);

        await dbContext.SaveChangesAsync();

        return agency.Id;
    }

    private async Task<HttpResponseMessage>
        PutAdminAgencyActionAsync(
            string? accessToken,
            Guid agencyId,
            string action)
    {
        _httpClient.ClearAuthorization();

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            _httpClient.AuthorizeAs(accessToken);
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/admin/agencies/{agencyId}/{action}");

            return await _httpClient.SendAsync(request);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task AssertSuccessfulAgencyStatusChangeAsync(
        HttpResponseMessage response,
        Guid agencyId,
        AgencyStatus expectedStatus)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.GetProperty("id")
            .GetGuid()
            .Should()
            .Be(agencyId);

        json.GetProperty("status")
            .GetString()
            .Should()
            .Be(expectedStatus.ToString());

        await AssertPersistedAgencyStatusAsync(
            agencyId,
            expectedStatus);
    }

    private async Task AssertPersistedAgencyStatusAsync(
        Guid agencyId,
        AgencyStatus expectedStatus)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        Agency agency =
            await dbContext.Agencies
                .AsNoTracking()
                .SingleAsync(currentAgency =>
                    currentAgency.Id == agencyId);

        agency.Status.Should().Be(expectedStatus);
    }
}
