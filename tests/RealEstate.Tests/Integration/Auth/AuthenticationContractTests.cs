using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RealEstate.Application.Auth.Commands.LoginUser;
using RealEstate.Application.Auth.Commands.RegisterUser;
using RealEstate.Application.Auth.Dtos;
using RealEstate.Application.Common;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Api;

namespace RealEstate.Tests.Integration.Auth;

public sealed class AuthenticationContractTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token")]
    public async Task BearerChallenge_WithMissingOrMalformedToken_IsCanonical(
        string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token);
        }

        HttpResponseMessage response = await _client.SendAsync(request);

        JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            ErrorCodes.AuthenticationRequired,
            "/api/users/me",
            bearerChallenge: true);

        string json = body.GetRawText();
        json.Contains("token", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        json.Contains("expired", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        json.Contains("signature", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Fact]
    public async Task BearerChallenge_WithExpiredToken_IsCanonicalWithoutTokenDetails()
    {
        string token = AuthTestHelpers.CreateSignedToken(
            _factory,
            Guid.NewGuid().ToString(),
            DateTime.UtcNow.AddMinutes(-10));
        _client.AuthorizeAs(token);

        HttpResponseMessage response = await _client.GetAsync("/api/users/me");

        JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            ErrorCodes.AuthenticationRequired,
            "/api/users/me",
            bearerChallenge: true);

        body.GetRawText().Contains(
            "expired",
            StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Fact]
    public async Task BearerAuthentication_WithValidToken_PreservesSuccessContract()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            "auth-contract-valid@test.com");
        _client.AuthorizeAs(user.AccessToken);

        HttpResponseMessage response = await _client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Login_WithUnknownEmailOrWrongPassword_HasSameCanonicalContract()
    {
        const string email = "auth-contract-credentials@test.com";
        await AuthTestHelpers.RegisterAndLoginAsync(_client, email);

        HttpResponseMessage wrongPassword = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "WrongPassword123!"));
        HttpResponseMessage unknownEmail = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("unknown-auth-contract@test.com", "Password123!"));

        JsonElement wrongBody = await ApiFailureAssertions.AssertProblemAsync(
            wrongPassword,
            HttpStatusCode.Unauthorized,
            ErrorCodes.AuthenticationInvalidCredentials,
            "/api/auth/login");
        JsonElement unknownBody = await ApiFailureAssertions.AssertProblemAsync(
            unknownEmail,
            HttpStatusCode.Unauthorized,
            ErrorCodes.AuthenticationInvalidCredentials,
            "/api/auth/login");

        wrongBody.GetProperty("title").GetString().Should()
            .Be(unknownBody.GetProperty("title").GetString());
        wrongBody.GetProperty("detail").GetString().Should()
            .Be(unknownBody.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData(UserStatus.Disabled)]
    [InlineData(UserStatus.PendingVerification)]
    public async Task Login_WithEstablishedAllowedStatus_RemainsSuccessful(
        UserStatus status)
    {
        string email = $"login-status-{status}-{Guid.NewGuid():N}@test.com";
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email);
        await AuthTestHelpers.SetUserStatusAsync(_factory, user.UserId, status);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        LoginResponse? body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.User.Status.Should().Be(status.ToString());
    }

    [Theory]
    [InlineData("register", "email")]
    [InlineData("register", "password")]
    [InlineData("register", "firstName")]
    [InlineData("login", "email")]
    [InlineData("login", "password")]
    public async Task HandlerValidation_UsesCanonicalJsonFacingField(
        string action,
        string field)
    {
        object request = (action, field) switch
        {
            ("register", "email") => new RegisterRequest(
                "invalid", "Password123!", "Test", "User", null),
            ("register", "password") => new RegisterRequest(
                "validation-password@test.com", "short", "Test", "User", null),
            ("register", "firstName") => new RegisterRequest(
                "validation-first@test.com", "Password123!", "", "User", null),
            ("login", "email") => new LoginRequest("invalid", "Password123!"),
            _ => new LoginRequest("validation-login@test.com", "")
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/auth/{action}",
            request);

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            $"/api/auth/{action}",
            validationKey: field);
    }

    [Fact]
    public async Task Register_DuplicateEmail_IsCanonicalConflict_AndSuccessKeepsLocation()
    {
        string email = $"duplicate-contract-{Guid.NewGuid():N}@test.com";
        var request = new RegisterRequest(
            email, "Password123!", "Test", "User", "+38970123456");

        HttpResponseMessage first = await _client.PostAsJsonAsync(
            "/api/auth/register",
            request);
        HttpResponseMessage duplicate = await _client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        AuthResponse? success = await first.Content.ReadFromJsonAsync<AuthResponse>();
        success.Should().NotBeNull();
        first.Headers.Location.Should().Be($"/api/users/{success!.User.Id}");

        await ApiFailureAssertions.AssertProblemAsync(
            duplicate,
            HttpStatusCode.Conflict,
            ErrorCodes.ConflictEmailAlreadyExists,
            "/api/auth/register");
    }
}
