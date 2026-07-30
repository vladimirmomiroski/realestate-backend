using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RealEstate.Application.Common;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    private const int MaximumAgencyLogoBytes = 5 * 1024 * 1024;

    [Fact]
    public async Task CreateAgency_WithInvalidName_ReturnsCanonicalValidation()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            object request = new
            {
                name = "",
                slug = $"agency-{Guid.NewGuid():N}"
            };

            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/agencies?ignored=secret",
                request);

            JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed,
                "/api/agencies",
                validationKey: "name");
            body.GetProperty("errors").GetProperty("name")[0]
                .GetString().Should().Be("Agency name is required.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateAgency_WithInvalidEmail_ReturnsCanonicalValidation()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            object request = new { name = "Agency", email = "invalid" };
            using HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/agencies/{agencyId}",
                request);

            JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed,
                $"/api/agencies/{agencyId}",
                validationKey: "email");
            body.GetProperty("errors").GetProperty("email")[0]
                .GetString().Should().Be("Agency email is invalid.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublicAgencyListings_WithInvalidSort_ReturnsCanonicalValidation()
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{Guid.NewGuid()}/listings?sort=invalid&ignored=secret");

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            response.RequestMessage!.RequestUri!.AbsolutePath,
            validationKey: "sort");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("my")]
    [InlineData("update")]
    [InlineData("dashboard-listings")]
    [InlineData("dashboard-summary")]
    [InlineData("logo-upload")]
    [InlineData("logo-delete")]
    public async Task ProtectedAgencyAction_WithNonGuidSubject_ReturnsInvalidPrincipal(
        string operation)
    {
        string token = AuthTestHelpers.CreateSignedToken(
            _factory,
            "not-a-guid",
            DateTime.UtcNow.AddMinutes(5));
        Guid agencyId = Guid.NewGuid();
        _httpClient.AuthorizeAs(token);

        try
        {
            using HttpResponseMessage response =
                await SendProtectedAgencyActionAsync(operation, agencyId);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Unauthorized,
                ErrorCodes.AuthenticationInvalidPrincipal,
                GetProtectedAgencyPath(operation, agencyId),
                bearerChallenge: true);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("create")]
    [InlineData("my")]
    [InlineData("update")]
    [InlineData("dashboard-listings")]
    [InlineData("dashboard-summary")]
    [InlineData("logo-upload")]
    [InlineData("logo-delete")]
    public async Task ProtectedAgencyAction_WithDeletedUser_ReturnsInvalidPrincipal(
        string operation)
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await AuthTestHelpers.DeleteUserAsync(_factory, user.UserId);
        Guid agencyId = Guid.NewGuid();
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            using HttpResponseMessage response =
                await SendProtectedAgencyActionAsync(operation, agencyId);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Unauthorized,
                ErrorCodes.AuthenticationInvalidPrincipal,
                GetProtectedAgencyPath(operation, agencyId),
                bearerChallenge: true);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("dashboard-listings")]
    [InlineData("dashboard-summary")]
    [InlineData("logo-upload")]
    [InlineData("logo-delete")]
    public async Task ProhibitedAgencyAction_WithDisabledUser_ReturnsAccountDisabled(
        string operation)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        await AuthTestHelpers.SetUserStatusAsync(
            _factory,
            owner.UserId,
            UserStatus.Disabled);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using HttpResponseMessage response =
                await SendProtectedAgencyActionAsync(operation, agencyId);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationAccountDisabled,
                GetProtectedAgencyPath(operation, agencyId));
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetMyAgencies_WithDisabledUser_RemainsSuccessful()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        await AuthTestHelpers.SetUserStatusAsync(
            _factory,
            owner.UserId,
            UserStatus.Disabled);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                "/api/agencies/my");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.EnumerateArray().Select(item => item.GetProperty("agencyId").GetGuid())
                .Should().Contain(agencyId);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateAgency_WithNonMember_ReturnsCanonicalForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser nonMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/agencies/{agencyId}",
                CreateValidUpdateAgencyRequest());

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                $"/api/agencies/{agencyId}");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("id")]
    [InlineData("slug")]
    public async Task PublicAgencyProfile_WithMissingAgency_ReturnsCanonicalNotFound(
        string lookup)
    {
        string path = lookup == "id"
            ? $"/api/agencies/{Guid.NewGuid()}"
            : $"/api/agencies/by-slug/missing-{Guid.NewGuid():N}";

        using HttpResponseMessage response = await _httpClient.GetAsync(path);

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.ResourceNotFound,
            path);
    }

    [Theory]
    [InlineData(AgencyStatus.PendingVerification)]
    [InlineData(AgencyStatus.Active)]
    [InlineData(AgencyStatus.Rejected)]
    [InlineData(AgencyStatus.Disabled)]
    public async Task PublicAgencyProfiles_RemainVisibleForEveryAgencyStatus(
        AgencyStatus status)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        string slug = $"public-status-{status.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}";
        Guid agencyId = await CreateAgencyAsAsync(owner, slug);
        await SetAgencyStatusForDashboardTestAsync(agencyId, status);
        _httpClient.ClearAuthorization();

        using HttpResponseMessage byId = await _httpClient.GetAsync(
            $"/api/agencies/{agencyId}");
        using HttpResponseMessage bySlug = await _httpClient.GetAsync(
            $"/api/agencies/by-slug/{slug}");

        byId.StatusCode.Should().Be(HttpStatusCode.OK);
        bySlug.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("missing", ErrorCodes.ValidationFileRequired, "Logo file is required.")]
    [InlineData("empty", ErrorCodes.ValidationFileEmpty, "Logo file is empty.")]
    [InlineData("oversized", ErrorCodes.ValidationFileTooLarge, "Logo file cannot be larger than 5 MB.")]
    [InlineData("extension", ErrorCodes.ValidationFileTypeNotSupported, "Only JPG, JPEG, PNG, and WEBP images are allowed.")]
    [InlineData("mime", ErrorCodes.ValidationFileTypeNotSupported, "Only JPG, JPEG, PNG, and WEBP images are allowed.")]
    public async Task UploadAgencyLogo_WithInvalidFile_ReturnsCanonicalValidation(
        string scenario,
        string expectedCode,
        string expectedMessage)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using MultipartFormDataContent content = CreateInvalidLogoContent(scenario);
            using HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/logo?ignored=secret",
                content);

            JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                expectedCode,
                $"/api/agencies/{agencyId}/logo",
                validationKey: "file");
            body.GetProperty("errors").GetProperty("file")[0]
                .GetString().Should().Be(expectedMessage);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<HttpResponseMessage> SendProtectedAgencyActionAsync(
        string operation,
        Guid agencyId)
    {
        return operation switch
        {
            "create" => await _httpClient.PostAsJsonAsync(
                "/api/agencies",
                CreateValidCreateAgencyRequest()),
            "my" => await _httpClient.GetAsync("/api/agencies/my"),
            "update" => await _httpClient.PutAsJsonAsync(
                $"/api/agencies/{agencyId}",
                CreateValidUpdateAgencyRequest()),
            "dashboard-listings" => await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/dashboard/listings"),
            "dashboard-summary" => await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/dashboard/summary"),
            "logo-upload" => await SendLogoUploadAsync(agencyId),
            "logo-delete" => await _httpClient.DeleteAsync(
                $"/api/agencies/{agencyId}/logo"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    private async Task<HttpResponseMessage> SendLogoUploadAsync(Guid agencyId)
    {
        using MultipartFormDataContent content = CreateAgencyLogoContent(
            "logo.png",
            "image/png",
            [1, 2, 3]);
        return await _httpClient.PutAsync(
            $"/api/agencies/{agencyId}/logo",
            content);
    }

    private static string GetProtectedAgencyPath(string operation, Guid agencyId)
    {
        return operation switch
        {
            "create" => "/api/agencies",
            "my" => "/api/agencies/my",
            "update" => $"/api/agencies/{agencyId}",
            "dashboard-listings" => $"/api/agencies/{agencyId}/dashboard/listings",
            "dashboard-summary" => $"/api/agencies/{agencyId}/dashboard/summary",
            "logo-upload" or "logo-delete" => $"/api/agencies/{agencyId}/logo",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    private static MultipartFormDataContent CreateInvalidLogoContent(string scenario)
    {
        if (scenario == "missing")
        {
            var missingFileContent = new MultipartFormDataContent();
            missingFileContent.Add(new StringContent("logo requested"), "metadata");
            return missingFileContent;
        }

        return scenario switch
        {
            "empty" => CreateAgencyLogoContent("logo.png", "image/png", []),
            "oversized" => CreateAgencyLogoContent(
                "logo.png",
                "image/png",
                new byte[MaximumAgencyLogoBytes + 1]),
            "extension" => CreateAgencyLogoContent(
                "logo.gif",
                "image/png",
                [1, 2, 3]),
            "mime" => CreateAgencyLogoContent(
                "logo.png",
                "image/gif",
                [1, 2, 3]),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }
}
