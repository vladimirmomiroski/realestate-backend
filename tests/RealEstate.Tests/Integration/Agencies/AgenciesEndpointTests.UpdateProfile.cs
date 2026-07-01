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
    public async Task UpdateAgency_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        var request = CreateValidUpdateAgencyRequest();

        HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
            $"/api/agencies/{Guid.NewGuid()}",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateAgency_WithMissingAgency_ReturnsNotFound()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = CreateValidUpdateAgencyRequest();

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/agencies/{Guid.NewGuid()}",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            string error = await response.Content.ReadAsStringAsync();

            error.Should().Contain("Agency was not found.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateAgency_WithNonMember_ReturnsForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser nonMember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        _httpClient.AuthorizeAs(nonMember.AccessToken);

        try
        {
            var request = CreateValidUpdateAgencyRequest();

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/agencies/{agencyId}",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateAgency_WithActiveAgent_ReturnsForbidden()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            owner.UserId,
            agent.UserId,
            AgencyMemberStatus.Active);

        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            var request = CreateValidUpdateAgencyRequest();

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/agencies/{agencyId}",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateAgency_WithDisabledOwner_ReturnsForbidden()
    {
        AuthenticatedTestUser activeOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        AuthenticatedTestUser disabledOwner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyWithMembersAsync(
            activeOwner.UserId,
            disabledOwner.UserId,
            AgencyMemberStatus.Disabled,
            AgencyMemberRole.Owner);

        _httpClient.AuthorizeAs(disabledOwner.AccessToken);

        try
        {
            var request = CreateValidUpdateAgencyRequest();

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/agencies/{agencyId}",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateAgency_WithActiveOwner_UpdatesAgencyProfile()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        string slug = $"dom-real-estate-{Guid.NewGuid():N}";

        Guid agencyId = await CreateAgencyAsAsync(owner, slug);

        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var request = CreateValidUpdateAgencyRequest();

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/agencies/{agencyId}",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().Be(agencyId);
            json.GetProperty("name").GetString().Should().Be("Updated Agency");
            json.GetProperty("slug").GetString().Should().Be(slug);
            json.GetProperty("description").GetString().Should().Be("Updated agency description.");
            json.GetProperty("phoneNumber").GetString().Should().Be("+38970222222");
            json.GetProperty("email").GetString().Should().Be("updated-agency@test.com");
            json.GetProperty("websiteUrl").GetString().Should().Be("https://updated-agency.test");
            json.GetProperty("addressLine").GetString().Should().Be("Updated Street 1");
            json.GetProperty("city").GetString().Should().Be("Skopje");
            json.GetProperty("municipality").GetString().Should().Be("Karpos");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}