using FluentAssertions;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task GetAgencyById_WithExistingAgency_ReturnsAgency()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId;

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyRequest();

            HttpResponseMessage createResponse = await _httpClient.PostAsJsonAsync(
                "/api/agencies",
                request);

            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement createJson =
                await createResponse.Content.ReadFromJsonAsync<JsonElement>();

            agencyId = createJson.GetProperty("id").GetGuid();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{agencyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("id").GetGuid().Should().Be(agencyId);
        json.GetProperty("name").GetString().Should().Be("Dom Real Estate");
        json.GetProperty("slug").GetString().Should().StartWith("dom-real-estate-");
        json.GetProperty("status").GetString().Should().Be("PendingVerification");
    }

    [Fact]
    public async Task GetAgencyById_WithMissingAgency_ReturnsNotFound()
    {
        Guid missingAgencyId = Guid.NewGuid();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{missingAgencyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string error = await response.Content.ReadAsStringAsync();

        error.Should().Contain("Agency was not found.");
    }
}
