using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Tests.Integration.Listings;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/api/agencies/{Guid.NewGuid()}/members");

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

    [Fact]
    public async Task GetAgencyListings_WithMissingAgency_ReturnsNotFound()
    {
        Guid missingAgencyId = Guid.NewGuid();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{missingAgencyId}/listings?lang=en&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string error = await response.Content.ReadAsStringAsync();

        error.Should().Contain("Agency was not found.");
    }

    [Fact]
    public async Task GetAgencyListings_WithExistingAgencyAndNoListings_ReturnsEmptyPagedResult()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId = await CreateAgencyAsAsync(user);

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{agencyId}/listings?lang=en&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        json.GetProperty("items").GetArrayLength().Should().Be(0);
        json.GetProperty("page").GetInt32().Should().Be(1);
        json.GetProperty("pageSize").GetInt32().Should().Be(20);
        json.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetAgencyListings_ReturnsOnlyListingsForAgency()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid firstAgencyId = await CreateAgencyAsAsync(owner);
        Guid secondAgencyId = await CreateAgencyAsAsync(owner);

        Guid firstAgencyListingId = await CreateAgencyListingAsAsync(
            owner,
            firstAgencyId,
            price: 99000);

        Guid secondAgencyListingId = await CreateAgencyListingAsAsync(
            owner,
            secondAgencyId,
            price: 125000);

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/agencies/{firstAgencyId}/listings?lang=en&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("totalCount").GetInt32().Should().Be(1);

        JsonElement items = json.GetProperty("items");

        items.GetArrayLength().Should().Be(1);

        Guid returnedListingId = items[0].GetProperty("id").GetGuid();

        returnedListingId.Should().Be(firstAgencyListingId);
        returnedListingId.Should().NotBe(secondAgencyListingId);

        items[0].GetProperty("agencyId").GetGuid().Should().Be(firstAgencyId);
    }

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

    private async Task<Guid> CreateAgencyAsAsync(
    AuthenticatedTestUser user,
    string? slug = null)
    {
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = CreateValidCreateAgencyRequest(slug);

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

    private async Task<Guid> CreateAgencyWithMembersAsync(
    Guid ownerUserId,
    Guid secondMemberUserId,
    AgencyMemberStatus secondMemberStatus,
    AgencyMemberRole secondMemberRole = AgencyMemberRole.Agent)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        var agency = AgencyTestHelpers.CreateAgency();

        agency.AddMember(ownerUserId, AgencyMemberRole.Owner);
        agency.AddMember(
            secondMemberUserId,
            secondMemberRole,
            secondMemberStatus);

        dbContext.Agencies.Add(agency);

        await dbContext.SaveChangesAsync();

        return agency.Id;
    }

    private async Task<Guid> CreateAgencyListingAsAsync(
    AuthenticatedTestUser user,
    Guid agencyId,
    decimal price = 99000)
    {
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = ListingTestHelpers.CreateValidListingRequest(
                price: price,
                agencyId: agencyId);

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/listings",
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

    private static object CreateValidUpdateAgencyRequest()
    {
        return new
        {
            name = "Updated Agency",
            description = "Updated agency description.",
            phoneNumber = "+38970222222",
            email = "updated-agency@test.com",
            websiteUrl = "https://updated-agency.test",
            addressLine = "Updated Street 1",
            city = "Skopje",
            municipality = "Karpos"
        };
    }
}
