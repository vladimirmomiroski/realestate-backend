using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RealEstate.Application.Auth.Commands.LoginUser;
using RealEstate.Application.Auth.Commands.RegisterUser;
using RealEstate.Application.Auth.Dtos;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Infrastructure.Security;

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

    public static string CreateSignedToken(
        CustomWebApplicationFactory factory,
        string subject,
        DateTime expiresUtc)
    {
        JwtOptions options = factory.Services
            .GetRequiredService<IOptions<JwtOptions>>()
            .Value;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim(ClaimTypes.NameIdentifier, subject),
            new Claim(ClaimTypes.Email, "signed-token@test.com"),
            new Claim(ClaimTypes.Role, UserRole.User.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            expires: expiresUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static async Task SetUserStatusAsync(
        CustomWebApplicationFactory factory,
        Guid userId,
        UserStatus status)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        await dbContext.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.Status, status));
    }

    public static async Task DeleteUserAsync(
        CustomWebApplicationFactory factory,
        Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        await dbContext.Users
            .Where(user => user.Id == userId)
            .ExecuteDeleteAsync();
    }
}
