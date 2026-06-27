using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Agencies;

public sealed class AgenciesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public AgenciesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task CreateAgency_WithValidRequest_ReturnsCreated()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyRequest();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/agencies",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.GetProperty("id").GetGuid().Should().NotBeEmpty();
            json.GetProperty("name").GetString().Should().Be("Dom Real Estate");
            json.GetProperty("slug").GetString().Should().StartWith("dom-real-estate-");
            json.GetProperty("status").GetString().Should().Be("PendingVerification");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateAgency_WithValidRequest_CreatesOwnerMembershipForCurrentUser()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId;

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyRequest();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/agencies",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            agencyId = json.GetProperty("id").GetGuid();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        var agency = await dbContext.Agencies
            .Include(agency => agency.Members)
            .SingleAsync(agency => agency.Id == agencyId);

        agency.Members.Should().ContainSingle();

        var member = agency.Members.Single();

        member.UserId.Should().Be(user.UserId);
        member.Role.Should().Be(AgencyMemberRole.Owner);
        member.Status.Should().Be(AgencyMemberStatus.Active);
    }

    [Fact]
    public async Task CreateAgency_WithoutAccessToken_ReturnsUnauthorized()
    {
        _httpClient.ClearAuthorization();

        var request = CreateValidCreateAgencyRequest();

        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            "/api/agencies",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAgency_WithDuplicateSlug_ReturnsBadRequest()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        string slug = $"dom-real-estate-{Guid.NewGuid():N}";

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var firstRequest = CreateValidCreateAgencyRequest(slug);

            HttpResponseMessage firstResponse = await _httpClient.PostAsJsonAsync(
                "/api/agencies",
                firstRequest);

            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var secondRequest = CreateValidCreateAgencyRequest(slug);

            HttpResponseMessage secondResponse = await _httpClient.PostAsJsonAsync(
                "/api/agencies",
                secondRequest);

            secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            string error = await secondResponse.Content.ReadAsStringAsync();

            error.Should().Contain("Agency slug already exists.");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private static object CreateValidCreateAgencyRequest(string? slug = null)
    {
        return new
        {
            name = "Dom Real Estate",
            slug = slug ?? $"dom-real-estate-{Guid.NewGuid():N}",
            description = "Real estate agency in Skopje.",
            phoneNumber = "+38970123456",
            email = "agency@test.com",
            websiteUrl = "https://agency.test",
            addressLine = "Partizanska 1",
            city = "Skopje",
            municipality = "Centar"
        };
    }

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


    private async Task<Guid> CreateAgencyAsAsync(AuthenticatedTestUser user)
    {
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyRequest();

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/agencies",
                request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            return json.GetProperty("id").GetGuid();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}
