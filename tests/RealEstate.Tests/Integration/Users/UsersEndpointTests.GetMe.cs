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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RealEstate.Tests.Integration.Users;

public sealed partial class UsersEndpointTests
{
    [Fact]
    public async Task GetMe_WithoutToken_ReturnsUnauthorized()
    {
        _client.ClearAuthorization();

        HttpResponseMessage response = await _client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithValidToken_ReturnsCurrentUserProfile()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "get-me-current-user@test.com");

        _client.AuthorizeAs(user.AccessToken);

        HttpResponseMessage response = await _client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        UserProfileResponse? body =
            await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Id.Should().Be(user.UserId);
        body.Email.Should().Be(user.Email);
        body.FirstName.Should().Be("Test");
        body.LastName.Should().Be("User");
        body.PhoneNumber.Should().Be("+38970123456");
        body.Role.Should().Be(UserRole.User);
        body.Status.Should().Be(UserStatus.PendingVerification);
        body.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetMe_ReturnsCurrentUser_NotAnotherUser()
    {
        AuthenticatedTestUser firstUser = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "get-me-first-user@test.com");

        AuthenticatedTestUser secondUser = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "get-me-second-user@test.com");

        _client.AuthorizeAs(secondUser.AccessToken);

        HttpResponseMessage response = await _client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        UserProfileResponse? body =
            await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Id.Should().Be(secondUser.UserId);
        body.Id.Should().NotBe(firstUser.UserId);
        body.Email.Should().Be(secondUser.Email);
    }

    [Fact]
    public async Task GetMe_WhenUserIsDisabled_ReturnsCurrentUserProfile()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "get-me-disabled-user@test.com");

        await SetUserStatusAsync(user.UserId, UserStatus.Disabled);

        _client.AuthorizeAs(user.AccessToken);

        HttpResponseMessage response = await _client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        UserProfileResponse? body =
            await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Id.Should().Be(user.UserId);
        body.Status.Should().Be(UserStatus.Disabled);
    }

    private async Task SetUserStatusAsync(
        Guid userId,
        UserStatus status)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        User user = await dbContext.Users.SingleAsync(user => user.Id == userId);

        dbContext.Entry(user)
            .Property(nameof(User.Status))
            .CurrentValue = status;

        await dbContext.SaveChangesAsync();
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
