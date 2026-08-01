using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RealEstate.Application.Common;
using RealEstate.Application.Users.Dtos;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Users;

public sealed partial class UsersEndpointTests
{
    [Theory]
    [InlineData("get", false)]
    [InlineData("profile", false)]
    [InlineData("upload", false)]
    [InlineData("delete", false)]
    [InlineData("get", true)]
    [InlineData("profile", true)]
    [InlineData("upload", true)]
    [InlineData("delete", true)]
    public async Task UserEndpoint_WithInvalidOrDeletedPrincipal_IsCanonicalUnauthorized(
        string operation,
        bool deleteRegisteredUser)
    {
        string token;

        if (deleteRegisteredUser)
        {
            AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
                _client,
                $"deleted-principal-{operation}-{Guid.NewGuid():N}@test.com");
            await AuthTestHelpers.DeleteUserAsync(_factory, user.UserId);
            token = user.AccessToken;
        }
        else
        {
            token = AuthTestHelpers.CreateSignedToken(
                _factory,
                "not-a-guid",
                DateTime.UtcNow.AddHours(1));
        }

        _client.AuthorizeAs(token);

        HttpResponseMessage response = await SendUserOperationAsync(operation);
        string path = operation switch
        {
            "get" => "/api/users/me",
            "profile" => "/api/users/me/profile",
            _ => "/api/users/me/avatar"
        };

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            ErrorCodes.AuthenticationInvalidPrincipal,
            path,
            bearerChallenge: true);
    }

    [Fact]
    public async Task DisabledProfileMutation_PrecedesHandlerValidation()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            "disabled-profile-precedence@test.com");
        await SetUserStatusAsync(user.UserId, UserStatus.Disabled);
        _client.AuthorizeAs(user.AccessToken);

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            "/api/users/me/profile",
            new UpdateUserProfileRequest("", "", null));

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            ErrorCodes.AuthorizationAccountDisabled,
            "/api/users/me/profile");
    }

    [Fact]
    public async Task DisabledAvatarMutation_PrecedesFileValidation()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            "disabled-avatar-precedence@test.com");
        await SetUserStatusAsync(user.UserId, UserStatus.Disabled);
        _client.AuthorizeAs(user.AccessToken);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("avatar requested"), "metadata");
        HttpResponseMessage response = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            ErrorCodes.AuthorizationAccountDisabled,
            "/api/users/me/avatar");
    }

    private async Task<HttpResponseMessage> SendUserOperationAsync(string operation)
    {
        switch (operation)
        {
            case "get":
                return await _client.GetAsync("/api/users/me");
            case "profile":
                return await _client.PutAsJsonAsync(
                    "/api/users/me/profile",
                    new UpdateUserProfileRequest("Valid", "User", null));
            case "upload":
                using (MultipartFormDataContent content = CreateAvatarContent(
                    "avatar.png",
                    "image/png"))
                {
                    return await _client.PutAsync("/api/users/me/avatar", content);
                }
            case "delete":
                return await _client.DeleteAsync("/api/users/me/avatar");
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
