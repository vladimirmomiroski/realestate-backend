using FluentAssertions;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task GetAgencyMembers_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{Guid.NewGuid()}/members");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAgencyMembers_WithMissingAgency_ReturnsNotFound()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            Guid agencyId = Guid.NewGuid();
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/members");

            await AssertResourceNotFoundAsync(
                response,
                $"/api/agencies/{agencyId}/members");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyMembers_WithNonMember_ReturnsForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser nonMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/members");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyMembers_WithActiveMember_ReturnsMembers()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/members");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.ValueKind.Should().Be(JsonValueKind.Array);
            json.GetArrayLength().Should().Be(2);

            List<Guid> userIds = json
                .EnumerateArray()
                .Select(member => member.GetProperty("userId").GetGuid())
                .ToList();

            userIds.Should().Contain(owner.UserId);
            userIds.Should().Contain(agent.UserId);

            JsonElement ownerMember = json
                .EnumerateArray()
                .Single(member => member.GetProperty("userId").GetGuid() == owner.UserId);

            ownerMember.GetProperty("email").GetString().Should().Be(owner.Email);
            ownerMember.GetProperty("firstName").GetString().Should().NotBeNullOrWhiteSpace();
            ownerMember.GetProperty("lastName").GetString().Should().NotBeNullOrWhiteSpace();
            ownerMember.GetProperty("userStatus").GetString().Should().Be("PendingVerification");
            ownerMember.GetProperty("memberRole").GetString().Should().Be("Owner");
            ownerMember.GetProperty("memberStatus").GetString().Should().Be("Active");
            ownerMember.GetProperty("joinedAtUtc").GetDateTime().Should().NotBe(default);

            JsonElement agentMember = json
                .EnumerateArray()
                .Single(member => member.GetProperty("userId").GetGuid() == agent.UserId);

            agentMember.GetProperty("email").GetString().Should().Be(agent.Email);
            agentMember.GetProperty("memberRole").GetString().Should().Be("Agent");
            agentMember.GetProperty("memberStatus").GetString().Should().Be("Active");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetAgencyMembers_WithDisabledMember_ReturnsForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser disabledMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            disabledMember.UserId,
            AgencyMemberStatus.Disabled);

        _httpClient.AuthorizeAs(disabledMember.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{agencyId}/members");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}
