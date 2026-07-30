using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Common;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Api;
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
        Guid agencyId = Guid.NewGuid();

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                accessToken: null,
                agencyId,
                action);

        // Assert
        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            ErrorCodes.AuthenticationRequired,
            GetAdminAgencyPath(agencyId, action),
            bearerChallenge: true);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("disable")]
    public async Task AdminAgencyAction_ShouldReturnInvalidPrincipal_WhenSubjectIsNotGuid(
        string action)
    {
        // Arrange
        string accessToken = AuthTestHelpers.CreateSignedToken(
            _factory,
            "not-a-guid",
            DateTime.UtcNow.AddMinutes(5));
        Guid agencyId = Guid.NewGuid();

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                accessToken,
                agencyId,
                action);

        // Assert
        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            ErrorCodes.AuthenticationInvalidPrincipal,
            GetAdminAgencyPath(agencyId, action),
            bearerChallenge: true);
    }

    [Fact]
    public async Task ApproveAgency_ShouldReturnInvalidPrincipal_WhenUserWasDeleted()
    {
        // Arrange
        AuthenticatedTestUser admin =
            await CreateUserWithPlatformAccessAsync(
                UserRole.Admin,
                UserStatus.Active);
        await AuthTestHelpers.DeleteUserAsync(_factory, admin.UserId);
        Guid agencyId = Guid.NewGuid();

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                admin.AccessToken,
                agencyId,
                "approve");

        // Assert
        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            ErrorCodes.AuthenticationInvalidPrincipal,
            GetAdminAgencyPath(agencyId, "approve"),
            bearerChallenge: true);
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
        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            ErrorCodes.AuthorizationForbidden,
            GetAdminAgencyPath(agencyId, action));
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
        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            ErrorCodes.AuthorizationForbidden,
            GetAdminAgencyPath(agencyId, action));
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
        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            ErrorCodes.AuthorizationAccountDisabled,
            GetAdminAgencyPath(agencyId, action));
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

        Guid agencyId = Guid.NewGuid();

        // Act
        HttpResponseMessage response =
            await PutAdminAgencyActionAsync(
                admin.AccessToken,
                agencyId,
                action);

        // Assert
        JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.ResourceNotFound,
            GetAdminAgencyPath(agencyId, action));

        body.GetProperty("detail").GetString().Should()
            .Be("The requested resource was not found.");
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
            initialStatus,
            AgencyStatus.Active);
    }

    [Fact]
    public async Task ApproveAgency_ShouldReturnConflict_WhenAgencyIsDisabled()
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
        JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            ErrorCodes.ConflictResourceState,
            GetAdminAgencyPath(agencyId, "approve"));

        body.GetProperty("detail").GetString().Should()
            .Be("The request conflicts with the current resource state.");

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
            initialStatus,
            AgencyStatus.Rejected);
    }

    [Theory]
    [InlineData(AgencyStatus.Active)]
    [InlineData(AgencyStatus.Disabled)]
    public async Task RejectAgency_ShouldReturnConflict_WhenTransitionIsNotAllowed(
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
        JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            ErrorCodes.ConflictResourceState,
            GetAdminAgencyPath(agencyId, "reject"));

        body.GetProperty("detail").GetString().Should()
            .Be("The request conflicts with the current resource state.");

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
            initialStatus,
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
        AgencyStatus initialStatus,
        AgencyStatus expectedStatus)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        json.EnumerateObject()
            .Select(property => property.Name)
            .Should()
            .BeEquivalentTo(
                "id",
                "name",
                "slug",
                "description",
                "logoUrl",
                "phoneNumber",
                "email",
                "websiteUrl",
                "addressLine",
                "city",
                "municipality",
                "status",
                "createdAtUtc",
                "modifiedAtUtc");

        Agency agency = await GetPersistedAgencyAsync(agencyId);

        json.GetProperty("id")
            .GetGuid()
            .Should()
            .Be(agencyId);

        json.GetProperty("status")
            .GetString()
            .Should()
            .Be(expectedStatus.ToString());

        json.GetProperty("name").GetString().Should().Be(agency.Name);
        json.GetProperty("slug").GetString().Should().Be(agency.Slug);
        json.GetProperty("description").GetString().Should()
            .Be(agency.Description);
        json.GetProperty("logoUrl").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("phoneNumber").GetString().Should()
            .Be(agency.PhoneNumber);
        json.GetProperty("email").GetString().Should().Be(agency.Email);
        json.GetProperty("websiteUrl").GetString().Should()
            .Be(agency.WebsiteUrl);
        json.GetProperty("addressLine").GetString().Should()
            .Be(agency.AddressLine);
        json.GetProperty("city").GetString().Should().Be(agency.City);
        json.GetProperty("municipality").GetString().Should()
            .Be(agency.Municipality);
        json.GetProperty("createdAtUtc").GetDateTime().Should()
            .Be(agency.CreatedAtUtc);

        JsonElement modifiedAtUtc = json.GetProperty("modifiedAtUtc");
        if (agency.ModifiedAtUtc is DateTime expectedModifiedAtUtc)
        {
            modifiedAtUtc.GetDateTime().Should().BeCloseTo(
                expectedModifiedAtUtc,
                TimeSpan.FromMicroseconds(1));
        }
        else
        {
            modifiedAtUtc.ValueKind.Should().Be(JsonValueKind.Null);
        }

        agency.Status.Should().Be(expectedStatus);
        agency.CreatedAtUtc.Should().NotBe(default);

        if (initialStatus == expectedStatus)
        {
            agency.ModifiedAtUtc.Should().BeNull();
        }
        else
        {
            agency.ModifiedAtUtc.Should().NotBeNull();
        }
    }

    private async Task AssertPersistedAgencyStatusAsync(
        Guid agencyId,
        AgencyStatus expectedStatus)
    {
        Agency agency = await GetPersistedAgencyAsync(agencyId);

        agency.Status.Should().Be(expectedStatus);
    }

    private async Task<Agency> GetPersistedAgencyAsync(Guid agencyId)
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

        return agency;
    }

    private static string GetAdminAgencyPath(
        Guid agencyId,
        string action)
    {
        return $"/api/admin/agencies/{agencyId}/{action}";
    }
}
