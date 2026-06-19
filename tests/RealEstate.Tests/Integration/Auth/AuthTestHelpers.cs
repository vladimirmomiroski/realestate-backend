using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RealEstate.Application.Auth.Commands.LoginUser;
using RealEstate.Application.Auth.Commands.RegisterUser;
using RealEstate.Application.Auth.Dtos;

namespace RealEstate.Tests.Integration.Auth;

public sealed record AuthenticatedTestUser(
    string AccessToken,
    Guid UserId,
    string Email);

public static class AuthTestHelpers
{
    public static async Task<AuthenticatedTestUser> RegisterAndLoginAsync(
        HttpClient client,
        string? email = null,
        string password = "Password123!")
    {
        email ??= $"test-user-{Guid.NewGuid():N}@test.com";

        var registerRequest = new RegisterRequest(
            email,
            password,
            "Test",
            "User",
            "+38970123456");

        HttpResponseMessage registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            registerRequest);

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginRequest = new LoginRequest(email, password);

        HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            loginRequest);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        LoginResponse? loginResponseBody =
            await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        loginResponseBody.Should().NotBeNull();
        loginResponseBody!.AccessToken.Should().NotBeNullOrWhiteSpace();

        return new AuthenticatedTestUser(
            loginResponseBody.AccessToken,
            loginResponseBody.User.Id,
            email);
    }

    public static void AuthorizeAs(
        this HttpClient client,
        string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public static void ClearAuthorization(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
    }
}
