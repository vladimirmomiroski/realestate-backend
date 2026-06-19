using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Auth.Commands.RegisterUser;
using RealEstate.Infrastructure.Persistence;

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

    private static RegisterRequest CreateValidRegisterRequest(string email)
    {
        return new RegisterRequest(
            email,
            "Password123!",
            "Test",
            "User",
            "+38970123456");
    }
}
