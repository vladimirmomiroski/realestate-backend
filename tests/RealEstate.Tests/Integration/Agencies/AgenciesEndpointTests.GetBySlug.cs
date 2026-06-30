using FluentAssertions;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
    [Fact]
    public async Task GetAgencyBySlug_WithExistingAgency_ReturnsAgency()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        string slug = $"dom-real-estate-{Guid.NewGuid():N}";

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyRequest(slug);

            HttpResponseMessage createResponse = await _httpClient.PostAsJsonAsync(
                "/api/agencies",
                request);

            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/by-slug/{slug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("name").GetString().Should().Be("Dom Real Estate");
        json.GetProperty("slug").GetString().Should().Be(slug);
        json.GetProperty("status").GetString().Should().Be("PendingVerification");
    }

    [Fact]
    public async Task GetAgencyBySlug_WithMissingAgency_ReturnsNotFound()
    {
        string missingSlug = $"missing-agency-{Guid.NewGuid():N}";

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/by-slug/{missingSlug}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string error = await response.Content.ReadAsStringAsync();

        error.Should().Contain("Agency was not found.");
    }

    [Fact]
    public async Task GetAgencyBySlug_WithUppercaseSlug_ReturnsAgency()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        string slug = $"dom-real-estate-{Guid.NewGuid():N}";

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyRequest(slug);

            HttpResponseMessage createResponse = await _httpClient.PostAsJsonAsync(
                "/api/agencies",
                request);

            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/by-slug/{slug.ToUpperInvariant()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("slug").GetString().Should().Be(slug);
    }
}