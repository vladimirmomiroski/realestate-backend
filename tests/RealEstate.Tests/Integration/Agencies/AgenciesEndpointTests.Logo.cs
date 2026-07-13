using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using Microsoft.Extensions.Options;
using RealEstate.Infrastructure.Storage;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task UploadAgencyLogo_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Arrange
        _httpClient.ClearAuthorization();

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "logo.png",
                "image/png");

        // Act
        HttpResponseMessage response = await _httpClient.PutAsync(
            $"/api/agencies/{Guid.NewGuid()}/logo",
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UploadAgencyLogo_ShouldReturnNotFound_WhenAgencyDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "logo.png",
                "image/png");

        // Act
        HttpResponseMessage response = await PutAgencyLogoAsync(
            user.AccessToken,
            Guid.NewGuid(),
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadAgencyLogo_ShouldReturnForbidden_WhenCurrentMemberIsAgent()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "logo.png",
                "image/png");

        // Act
        HttpResponseMessage response = await PutAgencyLogoAsync(
            agent.AccessToken,
            agencyId,
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UploadAgencyLogo_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await DisableUserAsync(owner.UserId);

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "logo.png",
                "image/png");

        // Act
        HttpResponseMessage response = await PutAgencyLogoAsync(
            owner.AccessToken,
            agencyId,
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UploadAgencyLogo_ShouldReturnBadRequest_WhenFileIsMissing()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        using var content = new MultipartFormDataContent();

        // Act
        HttpResponseMessage response = await PutAgencyLogoAsync(
            owner.AccessToken,
            agencyId,
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAgencyLogo_ShouldReturnBadRequest_WhenFileIsEmpty()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "logo.png",
                "image/png",
                Array.Empty<byte>());

        // Act
        HttpResponseMessage response = await PutAgencyLogoAsync(
            owner.AccessToken,
            agencyId,
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAgencyLogo_ShouldReturnBadRequest_WhenExtensionIsInvalid()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "logo.gif",
                "image/png");

        // Act
        HttpResponseMessage response = await PutAgencyLogoAsync(
            owner.AccessToken,
            agencyId,
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAgencyLogo_ShouldReturnBadRequest_WhenContentTypeIsInvalid()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "logo.png",
                "application/pdf");

        // Act
        HttpResponseMessage response = await PutAgencyLogoAsync(
            owner.AccessToken,
            agencyId,
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAgencyLogo_ShouldReturnBadRequest_WhenFileIsTooLarge()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        byte[] oversizedFile =
            new byte[(5 * 1024 * 1024) + 1];

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "logo.png",
                "image/png",
                oversizedFile);

        // Act
        HttpResponseMessage response = await PutAgencyLogoAsync(
            owner.AccessToken,
            agencyId,
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAgencyLogo_ShouldUploadAndPersistLogoMetadata()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        byte[] fileBytes = { 1, 2, 3, 4 };

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "agency-logo.png",
                "image/png",
                fileBytes);

        // Act
        HttpResponseMessage response = await PutAgencyLogoAsync(
            owner.AccessToken,
            agencyId,
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        string? logoUrl =
            json.GetProperty("logoUrl").GetString();

        logoUrl.Should().NotBeNullOrWhiteSpace();
        logoUrl.Should().Contain(
            $"/uploads/agencies/{agencyId}/logo/");

        Agency agency =
            await GetAgencyForLogoTestsAsync(agencyId);

        agency.LogoUrl.Should().Be(logoUrl);
        agency.LogoStoredFileName.Should().NotBeNullOrWhiteSpace();
        agency.LogoStoredFileName.Should().EndWith(".png");
        agency.LogoContentType.Should().Be("image/png");
        agency.LogoSizeBytes.Should().Be(fileBytes.Length);
    }

    [Fact]
    public async Task UploadAgencyLogo_ShouldReplaceExistingLogoMetadata()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        using MultipartFormDataContent firstContent =
            CreateAgencyLogoContent(
                "first-logo.png",
                "image/png",
                new byte[] { 1, 2, 3 });

        HttpResponseMessage firstResponse =
            await PutAgencyLogoAsync(
                owner.AccessToken,
                agencyId,
                firstContent);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Agency firstSavedAgency =
            await GetAgencyForLogoTestsAsync(agencyId);

        string firstStoredFileName =
            firstSavedAgency.LogoStoredFileName!;

        string firstLogoUrl =
            firstSavedAgency.LogoUrl!;

        string firstLogoPath = GetAgencyLogoFilePath(
            agencyId,
            firstStoredFileName);

        File.Exists(firstLogoPath).Should().BeTrue();

        using MultipartFormDataContent secondContent =
            CreateAgencyLogoContent(
                "second-logo.webp",
                "image/webp",
                new byte[] { 4, 5, 6, 7, 8 });

        // Act
        HttpResponseMessage secondResponse =
            await PutAgencyLogoAsync(
                owner.AccessToken,
                agencyId,
                secondContent);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Agency secondSavedAgency =
            await GetAgencyForLogoTestsAsync(agencyId);

        string secondStoredFileName =
            secondSavedAgency.LogoStoredFileName!;

        string secondLogoPath = GetAgencyLogoFilePath(
            agencyId,
            secondStoredFileName);

        secondSavedAgency.LogoStoredFileName.Should()
            .NotBe(firstStoredFileName);

        secondSavedAgency.LogoUrl.Should()
            .NotBe(firstLogoUrl);

        secondSavedAgency.LogoStoredFileName.Should()
            .EndWith(".webp");

        secondSavedAgency.LogoContentType.Should()
            .Be("image/webp");

        secondSavedAgency.LogoSizeBytes.Should()
            .Be(5);

        File.Exists(firstLogoPath).Should().BeFalse();
        File.Exists(secondLogoPath).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAgencyLogo_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Arrange
        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.DeleteAsync(
                $"/api/agencies/{Guid.NewGuid()}/logo");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAgencyLogo_ShouldReturnNotFound_WhenAgencyDoesNotExist()
    {
        // Arrange
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        // Act
        HttpResponseMessage response =
            await DeleteAgencyLogoRequestAsync(
                user.AccessToken,
                Guid.NewGuid());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAgencyLogo_ShouldReturnForbidden_WhenCurrentMemberIsAgent()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active,
            AgencyMemberRole.Agent);

        // Act
        HttpResponseMessage response =
            await DeleteAgencyLogoRequestAsync(
                agent.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteAgencyLogo_ShouldClearPersistedLogoMetadata()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "logo.png",
                "image/png");

        HttpResponseMessage uploadResponse =
            await PutAgencyLogoAsync(
                owner.AccessToken,
                agencyId,
                content);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Agency uploadedAgency =
            await GetAgencyForLogoTestsAsync(agencyId);

        string storedFileName =
            uploadedAgency.LogoStoredFileName!;

        string logoFilePath = GetAgencyLogoFilePath(
            agencyId,
            storedFileName);

        File.Exists(logoFilePath).Should().BeTrue();

        // Act
        HttpResponseMessage deleteResponse =
            await DeleteAgencyLogoRequestAsync(
                owner.AccessToken,
                agencyId);

        // Assert
        deleteResponse.StatusCode.Should()
            .Be(HttpStatusCode.NoContent);

        Agency agency =
            await GetAgencyForLogoTestsAsync(agencyId);

        agency.LogoUrl.Should().BeNull();
        agency.LogoStoredFileName.Should().BeNull();
        agency.LogoContentType.Should().BeNull();
        agency.LogoSizeBytes.Should().BeNull();

        File.Exists(logoFilePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAgencyLogo_ShouldBeIdempotent_WhenAgencyHasNoLogo()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        // Act
        HttpResponseMessage firstResponse =
            await DeleteAgencyLogoRequestAsync(
                owner.AccessToken,
                agencyId);

        HttpResponseMessage secondResponse =
            await DeleteAgencyLogoRequestAsync(
                owner.AccessToken,
                agencyId);

        // Assert
        firstResponse.StatusCode.Should()
            .Be(HttpStatusCode.NoContent);

        secondResponse.StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UploadAgencyLogo_ShouldReturnForbidden_WhenUserIsNotAgencyMember()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser nonMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "logo.png",
                "image/png");

        // Act
        HttpResponseMessage response = await PutAgencyLogoAsync(
            nonMember.AccessToken,
            agencyId,
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(AgencyMemberRole.Manager, AgencyMemberStatus.Active)]
    [InlineData(AgencyMemberRole.Owner, AgencyMemberStatus.Pending)]
    [InlineData(AgencyMemberRole.Owner, AgencyMemberStatus.Disabled)]
    public async Task UploadAgencyLogo_ShouldReturnForbidden_WhenCurrentMembershipIsNotAuthorized(
    AgencyMemberRole role,
    AgencyMemberStatus status)
    {
        // Arrange
        AuthenticatedTestUser activeOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser unauthorizedMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            activeOwner.UserId,
            unauthorizedMember.UserId,
            status,
            role);

        using MultipartFormDataContent content =
            CreateAgencyLogoContent(
                "logo.png",
                "image/png");

        // Act
        HttpResponseMessage response = await PutAgencyLogoAsync(
            unauthorizedMember.AccessToken,
            agencyId,
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteAgencyLogo_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        // Arrange
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await DisableUserAsync(owner.UserId);

        // Act
        HttpResponseMessage response =
            await DeleteAgencyLogoRequestAsync(
                owner.AccessToken,
                agencyId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static MultipartFormDataContent CreateAgencyLogoContent(
        string fileName,
        string contentType,
        byte[]? fileBytes = null)
    {
        var content = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(
            fileBytes ?? new byte[] { 1, 2, 3, 4 });

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(contentType);

        content.Add(
            fileContent,
            "file",
            fileName);

        return content;
    }

    private async Task<HttpResponseMessage> PutAgencyLogoAsync(
        string accessToken,
        Guid agencyId,
        MultipartFormDataContent content)
    {
        _httpClient.AuthorizeAs(accessToken);

        try
        {
            return await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/logo",
                content);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<HttpResponseMessage> DeleteAgencyLogoRequestAsync(
        string accessToken,
        Guid agencyId)
    {
        _httpClient.AuthorizeAs(accessToken);

        try
        {
            return await _httpClient.DeleteAsync(
                $"/api/agencies/{agencyId}/logo");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<Agency> GetAgencyForLogoTestsAsync(
        Guid agencyId)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();

        return await dbContext.Agencies
            .AsNoTracking()
            .SingleAsync(agency => agency.Id == agencyId);
    }

    private string GetAgencyLogoFilePath(
        Guid agencyId,
        string storedFileName)
    {
        LocalFileStorageOptions options =
            _factory.Services
                .GetRequiredService<IOptions<LocalFileStorageOptions>>()
                .Value;

        return Path.Combine(
            options.RootPath,
            "agencies",
            agencyId.ToString(),
            "logo",
            Path.GetFileName(storedFileName));
    }
}
