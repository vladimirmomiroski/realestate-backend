using FluentAssertions;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task CreateAgency_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        await DisableUserAsync(user.UserId);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyRequest();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/agencies",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateAgency_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await DisableUserAsync(owner.UserId);

        _httpClient.AuthorizeAs(owner.AccessToken);

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
    public async Task GetAgencyMembers_ShouldReturnForbidden_WhenCurrentUserIsDisabled()
    {
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(owner);

        await DisableUserAsync(owner.UserId);

        _httpClient.AuthorizeAs(owner.AccessToken);

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
