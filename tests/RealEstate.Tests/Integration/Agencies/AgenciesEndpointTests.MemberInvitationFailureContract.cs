using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using RealEstate.Application.Common;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Theory]
    [InlineData("member-list")]
    [InlineData("member-disable")]
    [InlineData("member-role")]
    [InlineData("invitation-create")]
    [InlineData("invitation-list")]
    [InlineData("invitation-accept")]
    [InlineData("invitation-cancel")]
    public async Task MemberInvitationAction_WithNonGuidSubject_ReturnsInvalidPrincipal(
        string operation)
    {
        string token = AuthTestHelpers.CreateSignedToken(
            _factory,
            "not-a-guid",
            DateTime.UtcNow.AddMinutes(5));
        Guid agencyId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        _httpClient.AuthorizeAs(token);

        try
        {
            using HttpResponseMessage response =
                await SendMemberInvitationActionAsync(
                    operation,
                    agencyId,
                    targetId);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Unauthorized,
                ErrorCodes.AuthenticationInvalidPrincipal,
                GetMemberInvitationPath(operation, agencyId, targetId),
                bearerChallenge: true);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("member-list")]
    [InlineData("member-disable")]
    [InlineData("member-role")]
    [InlineData("invitation-create")]
    [InlineData("invitation-list")]
    [InlineData("invitation-accept")]
    [InlineData("invitation-cancel")]
    public async Task MemberInvitationAction_WithDeletedUser_ReturnsInvalidPrincipal(
        string operation)
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await AuthTestHelpers.DeleteUserAsync(_factory, user.UserId);
        Guid agencyId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            using HttpResponseMessage response =
                await SendMemberInvitationActionAsync(
                    operation,
                    agencyId,
                    targetId);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Unauthorized,
                ErrorCodes.AuthenticationInvalidPrincipal,
                GetMemberInvitationPath(operation, agencyId, targetId),
                bearerChallenge: true);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("member-list")]
    [InlineData("member-disable")]
    [InlineData("member-role")]
    [InlineData("invitation-create")]
    [InlineData("invitation-list")]
    [InlineData("invitation-accept")]
    [InlineData("invitation-cancel")]
    public async Task MemberInvitationAction_WithDisabledUser_ReturnsAccountDisabled(
        string operation)
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await AuthTestHelpers.SetUserStatusAsync(
            _factory,
            user.UserId,
            UserStatus.Disabled);
        Guid agencyId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            using HttpResponseMessage response =
                await SendMemberInvitationActionAsync(
                    operation,
                    agencyId,
                    targetId);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationAccountDisabled,
                GetMemberInvitationPath(operation, agencyId, targetId));
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("member-list")]
    [InlineData("member-disable")]
    [InlineData("member-role")]
    [InlineData("invitation-create")]
    [InlineData("invitation-list")]
    [InlineData("invitation-cancel")]
    public async Task MemberInvitationAdminAction_WithNonMember_ReturnsGenericForbidden(
        string operation)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser outsider =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        Guid targetId = Guid.NewGuid();
        _httpClient.AuthorizeAs(outsider.AccessToken);

        try
        {
            using HttpResponseMessage response =
                await SendMemberInvitationActionAsync(
                    operation,
                    agencyId,
                    targetId);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                GetMemberInvitationPath(operation, agencyId, targetId));
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptInvitation_WithRecipientMismatch_ReturnsGenericForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser invitedUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        AcceptInvitationSeed seed = await CreateInvitationForAcceptAsync(
            agencyId,
            owner.UserId,
            invitedUser.Email);
        const string path = "/api/agencies/invitations/accept";
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                path,
                new { token = seed.Token });

            JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                path);
            body.GetProperty("detail").GetString().Should()
                .NotContain(invitedUser.Email);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task ListMembers_WithMissingAgency_ReturnsCanonicalNotFound()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = Guid.NewGuid();
        string path = $"/api/agencies/{agencyId}/members";
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(path);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                ErrorCodes.ResourceNotFound,
                path);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task ChangeMemberRole_WithNonassignableRole_ReturnsCanonicalValidation()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active);
        Guid memberId = await GetAgencyMemberIdAsync(agencyId, agent.UserId);
        string path = $"/api/agencies/{agencyId}/members/{memberId}/role";
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                path,
                new { role = AgencyMemberRole.Manager });

            JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed,
                path,
                validationKey: "role");
            body.GetProperty("errors").GetProperty("role")[0]
                .GetString().Should()
                .Be("Agency member role must be Owner or Agent.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("member-role")]
    [InlineData("invitation-create")]
    [InlineData("invitation-accept")]
    public async Task MemberInvitationAction_WithMissingBody_ReturnsRequestValidation(
        string operation)
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active);
        Guid memberId = await GetAgencyMemberIdAsync(agencyId, agent.UserId);
        string path = GetMemberInvitationPath(operation, agencyId, memberId);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = new StringContent(
                    "null",
                    Encoding.UTF8,
                    "application/json")
            };

            if (operation == "invitation-create")
            {
                request.Method = HttpMethod.Post;
            }

            using HttpResponseMessage response = await _httpClient.SendAsync(request);

            await AssertMissingBodyValidationAsync(response, path);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateInvitation_WithInvalidEmail_ReturnsCanonicalValidation()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        string path = $"/api/agencies/{agencyId}/invitations";
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                path,
                new { email = "invalid", role = AgencyMemberRole.Agent });

            JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed,
                path,
                validationKey: "email");
            body.GetProperty("errors").GetProperty("email")[0]
                .GetString().Should().Be("Invitation email is invalid.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptInvitation_WithBlankToken_ReturnsCanonicalValidation()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        const string path = "/api/agencies/invitations/accept";
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                path,
                new { token = " " });

            JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed,
                path,
                validationKey: "token");
            body.GetProperty("errors").GetProperty("token")[0]
                .GetString().Should().Be("Invitation token is required.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task ListInvitations_WithInvalidStatus_ReturnsCanonicalValidation()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid agencyId = await CreateAgencyAsAsync(owner);
        string path = $"/api/agencies/{agencyId}/invitations";
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                $"{path}?status=invalid&secret=value");

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed,
                path,
                validationKey: "status");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task AcceptInvitation_WithUnknownToken_ReturnsCanonicalNotFound()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        const string path = "/api/agencies/invitations/accept";
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                path,
                new { token = $"unknown-{Guid.NewGuid():N}" });

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                ErrorCodes.ResourceNotFound,
                path);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<HttpResponseMessage> SendMemberInvitationActionAsync(
        string operation,
        Guid agencyId,
        Guid targetId)
    {
        return operation switch
        {
            "member-list" => await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/members"),
            "member-disable" => await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/members/{targetId}/disable",
                content: null),
            "member-role" => await _httpClient.PutAsJsonAsync(
                $"/api/agencies/{agencyId}/members/{targetId}/role",
                new { role = AgencyMemberRole.Agent }),
            "invitation-create" => await _httpClient.PostAsJsonAsync(
                $"/api/agencies/{agencyId}/invitations",
                new
                {
                    email = $"invite-{Guid.NewGuid():N}@test.com",
                    role = AgencyMemberRole.Agent
                }),
            "invitation-list" => await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/invitations"),
            "invitation-accept" => await _httpClient.PutAsJsonAsync(
                "/api/agencies/invitations/accept",
                new { token = $"unknown-{Guid.NewGuid():N}" }),
            "invitation-cancel" => await _httpClient.PutAsync(
                $"/api/agencies/{agencyId}/invitations/{targetId}/cancel",
                content: null),
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Unknown member/invitation operation.")
        };
    }

    private static string GetMemberInvitationPath(
        string operation,
        Guid agencyId,
        Guid targetId)
    {
        return operation switch
        {
            "member-list" => $"/api/agencies/{agencyId}/members",
            "member-disable" =>
                $"/api/agencies/{agencyId}/members/{targetId}/disable",
            "member-role" =>
                $"/api/agencies/{agencyId}/members/{targetId}/role",
            "invitation-create" or "invitation-list" =>
                $"/api/agencies/{agencyId}/invitations",
            "invitation-accept" => "/api/agencies/invitations/accept",
            "invitation-cancel" =>
                $"/api/agencies/{agencyId}/invitations/{targetId}/cancel",
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Unknown member/invitation operation.")
        };
    }

    private static Task<JsonElement> AssertResourceStateConflictAsync(
        HttpResponseMessage response,
        string expectedPath)
    {
        return ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            ErrorCodes.ConflictResourceState,
            expectedPath);
    }

    private static Task<JsonElement> AssertResourceNotFoundAsync(
        HttpResponseMessage response,
        string expectedPath)
    {
        return ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.ResourceNotFound,
            expectedPath);
    }

    private static async Task AssertMissingBodyValidationAsync(
        HttpResponseMessage response,
        string expectedPath)
    {
        string responseText = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/problem+json");

        using JsonDocument document = JsonDocument.Parse(responseText);
        JsonElement body = document.RootElement;
        string traceId = body.GetProperty("traceId").GetString()!;

        body.GetProperty("type").GetString().Should()
            .Be("urn:realestate:error:validation.failed");
        body.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("instance").GetString().Should().Be(expectedPath);
        body.GetProperty("code").GetString().Should()
            .Be(ErrorCodes.ValidationFailed);
        traceId.Should().NotBeNullOrWhiteSpace();
        response.Headers.GetValues("X-Request-ID").Should()
            .ContainSingle().Which.Should().Be(traceId);
        response.Headers.WwwAuthenticate.Should().BeEmpty();

        JsonElement errors = body.GetProperty("errors");
        errors.EnumerateObject().Should().ContainSingle();
        JsonElement messages = errors.GetProperty("request");
        messages.GetArrayLength().Should().BeGreaterThan(0);
        messages.EnumerateArray().Select(message => message.GetString())
            .Should().OnlyContain(message => !string.IsNullOrWhiteSpace(message));
    }
}
