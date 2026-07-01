using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Auth.Commands.LoginUser;
using RealEstate.Application.Auth.Commands.RegisterUser;
using RealEstate.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Auth;

public sealed class AuthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsCreated()
    {
        var request = CreateValidRegisterRequest("register-valid@test.com");

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var request = CreateValidRegisterRequest("duplicate@test.com");

        HttpResponseMessage firstResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        HttpResponseMessage secondResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainTextPassword()
    {
        const string email = "hashed-password@test.com";
        const string plainPassword = "Password123!";

        var request = new RegisterRequest(
            email,
            plainPassword,
            "Test",
            "User",
            "+38970123456");

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        var user = await dbContext.Users.SingleAsync(
            user => user.NormalizedEmail == email.ToUpperInvariant());

        user.PasswordHash.Should().NotBeNullOrWhiteSpace();
        user.PasswordHash.Should().NotBe(plainPassword);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        var request = CreateValidRegisterRequest("not-valid-email");

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        var request = new RegisterRequest(
            "short-password@test.com",
            "123",
            "Test",
            "User",
            "+38970123456");

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static RegisterRequest CreateValidRegisterRequest(
        string email,
        string password = "Password123!")
    {
        return new RegisterRequest(
            email,
            password,
            "Test",
            "User",
            "+38970123456");
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        const string email = "login-valid@test.com";
        const string password = "Password123!";

        var registerRequest = CreateValidRegisterRequest(email, password);

        HttpResponseMessage registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            registerRequest);

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginRequest = new LoginRequest(email, password);

        HttpResponseMessage loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            loginRequest);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        string content = await loginResponse.Content.ReadAsStringAsync();

        using JsonDocument json = JsonDocument.Parse(content);

        string accessToken = json.RootElement
            .GetProperty("accessToken")
            .GetString()!;

        accessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        const string email = "login-wrong-password@test.com";

        var registerRequest = CreateValidRegisterRequest(email, "Password123!");

        HttpResponseMessage registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            registerRequest);

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginRequest = new LoginRequest(email, "WrongPassword123!");

        HttpResponseMessage loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            loginRequest);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        var loginRequest = new LoginRequest(
            "unknown-login@test.com",
            "Password123!");

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
