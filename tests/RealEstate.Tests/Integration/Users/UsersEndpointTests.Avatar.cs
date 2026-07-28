using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Users.Dtos;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Application.Common;
using RealEstate.Tests.Integration.Api;
using Microsoft.Extensions.Options;
using RealEstate.Infrastructure.Storage;

namespace RealEstate.Tests.Integration.Users;

public sealed partial class UsersEndpointTests
{
    [Fact]
    public async Task UploadAvatar_WithoutToken_ReturnsUnauthorized()
    {
        _client.ClearAuthorization();

        using MultipartFormDataContent content = CreateAvatarContent(
            "avatar.png",
            "image/png");

        HttpResponseMessage response = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UploadAvatar_WhenUserIsActive_UploadsAvatar()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "upload-avatar-active@test.com");

        await SetUserStatusAsync(user.UserId, UserStatus.Active);

        _client.AuthorizeAs(user.AccessToken);

        using MultipartFormDataContent content = CreateAvatarContent(
            "avatar.png",
            "image/png");

        HttpResponseMessage response = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        UserProfileResponse? body =
            await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Id.Should().Be(user.UserId);
        body.AvatarUrl.Should().NotBeNullOrWhiteSpace();
        body.AvatarUrl.Should().Contain($"/uploads/users/{user.UserId}/avatar/");

        User dbUser = await GetUserFromDatabaseAsync(user.UserId);

        dbUser.AvatarUrl.Should().Be(body.AvatarUrl);
        dbUser.AvatarStoredFileName.Should().NotBeNullOrWhiteSpace();
        dbUser.AvatarContentType.Should().Be("image/png");
        dbUser.AvatarSizeBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UploadAvatar_WhenUserIsPendingVerification_UploadsAvatar()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "upload-avatar-pending@test.com");

        _client.AuthorizeAs(user.AccessToken);

        using MultipartFormDataContent content = CreateAvatarContent(
            "avatar.webp",
            "image/webp");

        HttpResponseMessage response = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        UserProfileResponse? body =
            await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Status.Should().Be(UserStatus.PendingVerification);
        body.AvatarUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UploadAvatar_WhenUserIsDisabled_ReturnsForbidden()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "upload-avatar-disabled@test.com");

        await SetUserStatusAsync(user.UserId, UserStatus.Disabled);

        _client.AuthorizeAs(user.AccessToken);

        using MultipartFormDataContent content = CreateAvatarContent(
            "avatar.png",
            "image/png");

        HttpResponseMessage response = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            ErrorCodes.AuthorizationAccountDisabled,
            "/api/users/me/avatar");
    }

    [Fact]
    public async Task UploadAvatar_WithMissingFile_ReturnsBadRequest()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "upload-avatar-missing-file@test.com");

        _client.AuthorizeAs(user.AccessToken);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("avatar requested"), "metadata");

        HttpResponseMessage response = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        await AssertAvatarValidationAsync(
            response,
            ErrorCodes.ValidationFileRequired);
    }

    [Fact]
    public async Task UploadAvatar_WithEmptyFile_ReturnsBadRequest()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "upload-avatar-empty-file@test.com");

        _client.AuthorizeAs(user.AccessToken);

        using MultipartFormDataContent content = CreateAvatarContent(
            "avatar.png",
            "image/png",
            Array.Empty<byte>());

        HttpResponseMessage response = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        await AssertAvatarValidationAsync(
            response,
            ErrorCodes.ValidationFileEmpty);
    }

    [Fact]
    public async Task UploadAvatar_WithInvalidExtension_ReturnsBadRequest()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "upload-avatar-invalid-extension@test.com");

        _client.AuthorizeAs(user.AccessToken);

        using MultipartFormDataContent content = CreateAvatarContent(
            "avatar.gif",
            "image/png");

        HttpResponseMessage response = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        await AssertAvatarValidationAsync(
            response,
            ErrorCodes.ValidationFileTypeNotSupported);
    }

    [Fact]
    public async Task UploadAvatar_WithInvalidContentType_ReturnsBadRequest()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "upload-avatar-invalid-content-type@test.com");

        _client.AuthorizeAs(user.AccessToken);

        using MultipartFormDataContent content = CreateAvatarContent(
            "avatar.png",
            "text/plain");

        HttpResponseMessage response = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        await AssertAvatarValidationAsync(
            response,
            ErrorCodes.ValidationFileTypeNotSupported);
    }

    [Fact]
    public async Task UploadAvatar_WithFileTooLarge_ReturnsBadRequest()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "upload-avatar-file-too-large@test.com");

        _client.AuthorizeAs(user.AccessToken);

        byte[] bytes = new byte[(5 * 1024 * 1024) + 1];

        using MultipartFormDataContent content = CreateAvatarContent(
            "avatar.png",
            "image/png",
            bytes);

        HttpResponseMessage response = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        await AssertAvatarValidationAsync(
            response,
            ErrorCodes.ValidationFileTooLarge);
    }

    [Fact]
    public async Task UploadAvatar_WhenAvatarAlreadyExists_ReplacesAvatar()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "upload-avatar-replace@test.com");

        _client.AuthorizeAs(user.AccessToken);

        using MultipartFormDataContent firstContent = CreateAvatarContent(
            "first.png",
            "image/png");

        HttpResponseMessage firstResponse = await _client.PutAsync(
            "/api/users/me/avatar",
            firstContent);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        User firstDbUser = await GetUserFromDatabaseAsync(user.UserId);

        string firstStoredFileName = firstDbUser.AvatarStoredFileName!;
        string firstFilePath = GetAvatarFilePath(
            user.UserId,
            firstStoredFileName);
        File.Exists(firstFilePath).Should().BeTrue();

        using MultipartFormDataContent secondContent = CreateAvatarContent(
            "second.webp",
            "image/webp");

        HttpResponseMessage secondResponse = await _client.PutAsync(
            "/api/users/me/avatar",
            secondContent);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        User secondDbUser = await GetUserFromDatabaseAsync(user.UserId);

        secondDbUser.AvatarStoredFileName.Should().NotBeNullOrWhiteSpace();
        secondDbUser.AvatarStoredFileName.Should().NotBe(firstStoredFileName);
        secondDbUser.AvatarContentType.Should().Be("image/webp");
        secondDbUser.AvatarUrl.Should().Contain($"/uploads/users/{user.UserId}/avatar/");
        File.Exists(firstFilePath).Should().BeFalse();
        File.Exists(GetAvatarFilePath(
            user.UserId,
            secondDbUser.AvatarStoredFileName!)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAvatar_WithoutToken_ReturnsUnauthorized()
    {
        _client.ClearAuthorization();

        HttpResponseMessage response = await _client.DeleteAsync(
            "/api/users/me/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAvatar_WhenUserIsActive_DeletesAvatar()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "delete-avatar-active@test.com");

        await SetUserStatusAsync(user.UserId, UserStatus.Active);

        _client.AuthorizeAs(user.AccessToken);

        using MultipartFormDataContent content = CreateAvatarContent(
            "avatar.png",
            "image/png");

        HttpResponseMessage uploadResponse = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        User uploadedUser = await GetUserFromDatabaseAsync(user.UserId);
        string uploadedFilePath = GetAvatarFilePath(
            user.UserId,
            uploadedUser.AvatarStoredFileName!);
        File.Exists(uploadedFilePath).Should().BeTrue();

        HttpResponseMessage deleteResponse = await _client.DeleteAsync(
            "/api/users/me/avatar");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        User dbUser = await GetUserFromDatabaseAsync(user.UserId);

        dbUser.AvatarUrl.Should().BeNull();
        dbUser.AvatarStoredFileName.Should().BeNull();
        dbUser.AvatarContentType.Should().BeNull();
        dbUser.AvatarSizeBytes.Should().BeNull();
        File.Exists(uploadedFilePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAvatar_WhenUserIsPendingVerification_DeletesAvatar()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "delete-avatar-pending@test.com");

        _client.AuthorizeAs(user.AccessToken);

        using MultipartFormDataContent content = CreateAvatarContent(
            "avatar.webp",
            "image/webp");

        HttpResponseMessage uploadResponse = await _client.PutAsync(
            "/api/users/me/avatar",
            content);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage deleteResponse = await _client.DeleteAsync(
            "/api/users/me/avatar");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        User dbUser = await GetUserFromDatabaseAsync(user.UserId);

        dbUser.AvatarUrl.Should().BeNull();
        dbUser.AvatarStoredFileName.Should().BeNull();
        dbUser.AvatarContentType.Should().BeNull();
        dbUser.AvatarSizeBytes.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAvatar_WhenUserIsDisabled_ReturnsForbidden()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "delete-avatar-disabled@test.com");

        await SetUserStatusAsync(user.UserId, UserStatus.Disabled);

        _client.AuthorizeAs(user.AccessToken);

        HttpResponseMessage response = await _client.DeleteAsync(
            "/api/users/me/avatar");

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            ErrorCodes.AuthorizationAccountDisabled,
            "/api/users/me/avatar");
    }

    [Fact]
    public async Task DeleteAvatar_WhenAvatarDoesNotExist_ReturnsNoContent()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(
            _client,
            email: "delete-avatar-no-avatar@test.com");

        _client.AuthorizeAs(user.AccessToken);

        HttpResponseMessage response = await _client.DeleteAsync(
            "/api/users/me/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        User dbUser = await GetUserFromDatabaseAsync(user.UserId);

        dbUser.AvatarUrl.Should().BeNull();
        dbUser.AvatarStoredFileName.Should().BeNull();
        dbUser.AvatarContentType.Should().BeNull();
        dbUser.AvatarSizeBytes.Should().BeNull();
    }

    private static MultipartFormDataContent CreateAvatarContent(
        string fileName,
        string contentType,
        byte[]? bytes = null)
    {
        bytes ??= [1, 2, 3, 4];

        var content = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        content.Add(fileContent, "file", fileName);

        return content;
    }

    private static async Task AssertAvatarValidationAsync(
        HttpResponseMessage response,
        string errorCode)
    {
        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            errorCode,
            "/api/users/me/avatar",
            validationKey: "file");
    }

    private async Task<User> GetUserFromDatabaseAsync(Guid userId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        return await dbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Id == userId);
    }

    private string GetAvatarFilePath(Guid userId, string storedFileName)
    {
        LocalFileStorageOptions options = _factory.Services
            .GetRequiredService<IOptions<LocalFileStorageOptions>>()
            .Value;

        return Path.Combine(
            options.RootPath,
            "users",
            userId.ToString(),
            "avatar",
            storedFileName);
    }
}
