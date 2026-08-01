using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Application.Common;
using RealEstate.Tests.Integration.Api;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
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
            Guid createdAgencyId = json.GetProperty("id").GetGuid();
            response.Headers.Location.Should().NotBeNull();
            response.Headers.Location!.IsAbsoluteUri.Should().BeFalse();
            response.Headers.Location.OriginalString.Should()
                .Be($"/api/agencies/{createdAgencyId}");
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
    public async Task CreateAgency_WithDuplicateSlug_ReturnsCanonicalConflict()
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

            await ApiFailureAssertions.AssertProblemAsync(
                secondResponse,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictAgencySlugAlreadyExists,
                "/api/agencies");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }
}
