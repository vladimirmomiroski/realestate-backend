using FluentAssertions;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task GetMyAgencies_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.GetAsync(
            "/api/agencies/my");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyAgencies_WhenUserHasNoAgencies_ReturnsEmptyArray()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                "/api/agencies/my");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.ValueKind.Should().Be(JsonValueKind.Array);
            json.GetArrayLength().Should().Be(0);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetMyAgencies_WhenUserBelongsToAgency_ReturnsAgencyWithMembershipData()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(user);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                "/api/agencies/my");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetArrayLength().Should().Be(1);

            JsonElement agency = json[0];

            agency.GetProperty("agencyId").GetGuid().Should().Be(agencyId);
            agency.GetProperty("name").GetString().Should().Be("Dom Real Estate");
            agency.GetProperty("slug").GetString().Should().StartWith("dom-real-estate-");
            agency.GetProperty("agencyStatus").GetString().Should().Be("PendingVerification");
            agency.GetProperty("memberRole").GetString().Should().Be("Owner");
            agency.GetProperty("memberStatus").GetString().Should().Be("Active");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
    [Fact]
    public async Task GetMyAgencies_ReturnsOnlyCurrentUsersAgencies()
    {
        AuthenticatedTestUser firstUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser secondUser =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid firstUserAgencyId = await CreateAgencyAsAsync(firstUser);
        Guid secondUserAgencyId = await CreateAgencyAsAsync(secondUser);

        _httpClient.AuthorizeAs(firstUser.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                "/api/agencies/my");

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            List<Guid> agencyIds = json
                .EnumerateArray()
                .Select(agency => agency.GetProperty("agencyId").GetGuid())
                .ToList();

            agencyIds.Should().Contain(firstUserAgencyId);
            agencyIds.Should().NotContain(secondUserAgencyId);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}
