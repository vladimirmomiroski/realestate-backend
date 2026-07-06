using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Users.Dtos;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Users;

public sealed partial class UsersEndpointTests
{
    [Fact]
    public async Task UpdateProfile_WithoutToken_ReturnsUnauthorized()
    {
        _client.ClearAuthorization();

        var request = new UpdateUserProfileRequest(
            "Updated",
            "User",
            "+38970111111");

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            "/api/users/me/profile",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_WhenUserIsActive_UpdatesProfile()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "update-profile-active@test.com");

        await SetUserStatusAsync(user.UserId, UserStatus.Active);

        _client.AuthorizeAs(user.AccessToken);

        var request = new UpdateUserProfileRequest(
            "Updated",
            "Person",
            "+38970111111");

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            "/api/users/me/profile",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        UserProfileResponse? body =
            await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Id.Should().Be(user.UserId);
        body.FirstName.Should().Be("Updated");
        body.LastName.Should().Be("Person");
        body.PhoneNumber.Should().Be("+38970111111");
        body.Status.Should().Be(UserStatus.Active);

        await AssertUserProfileInDatabaseAsync(
            user.UserId,
            "Updated",
            "Person",
            "+38970111111");
    }

    [Fact]
    public async Task UpdateProfile_WhenUserIsPendingVerification_UpdatesProfile()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "update-profile-pending@test.com");

        _client.AuthorizeAs(user.AccessToken);

        var request = new UpdateUserProfileRequest(
            "Pending",
            "User",
            null);

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            "/api/users/me/profile",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        UserProfileResponse? body =
            await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.FirstName.Should().Be("Pending");
        body.LastName.Should().Be("User");
        body.PhoneNumber.Should().BeNull();
        body.Status.Should().Be(UserStatus.PendingVerification);
    }

    [Fact]
    public async Task UpdateProfile_WhenUserIsDisabled_ReturnsForbidden()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "update-profile-disabled@test.com");

        await SetUserStatusAsync(user.UserId, UserStatus.Disabled);

        _client.AuthorizeAs(user.AccessToken);

        var request = new UpdateUserProfileRequest(
            "Disabled",
            "User",
            "+38970111111");

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            "/api/users/me/profile",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateProfile_WithMissingFirstName_ReturnsBadRequest()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "update-profile-missing-first-name@test.com");

        _client.AuthorizeAs(user.AccessToken);

        var request = new UpdateUserProfileRequest(
            "",
            "User",
            "+38970111111");

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            "/api/users/me/profile",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProfile_WithMissingLastName_ReturnsBadRequest()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "update-profile-missing-last-name@test.com");

        _client.AuthorizeAs(user.AccessToken);

        var request = new UpdateUserProfileRequest(
            "Updated",
            " ",
            "+38970111111");

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            "/api/users/me/profile",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProfile_WithReadOnlyFields_DoesNotChangeReadOnlyFields()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "update-profile-read-only@test.com");

        _client.AuthorizeAs(user.AccessToken);

        var request = new
        {
            firstName = "Updated",
            lastName = "User",
            phoneNumber = "+38970111111",
            email = "hacked@test.com",
            role = "Admin",
            status = "Disabled",
            avatarUrl = "/uploads/hacked.webp"
        };

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            "/api/users/me/profile",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        User dbUser = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(dbUser => dbUser.Id == user.UserId);

        dbUser.Email.Should().Be(user.Email);
        dbUser.NormalizedEmail.Should().Be(user.Email.ToUpperInvariant());
        dbUser.Role.Should().Be(UserRole.User);
        dbUser.Status.Should().Be(UserStatus.PendingVerification);
        dbUser.AvatarUrl.Should().BeNull();
    }

    private async Task AssertUserProfileInDatabaseAsync(
        Guid userId,
        string firstName,
        string lastName,
        string? phoneNumber)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        User dbUser = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Id == userId);

        dbUser.FirstName.Should().Be(firstName);
        dbUser.LastName.Should().Be(lastName);
        dbUser.PhoneNumber.Should().Be(phoneNumber);
    }
}
