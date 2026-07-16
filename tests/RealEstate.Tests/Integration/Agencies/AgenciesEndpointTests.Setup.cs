using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Tests.Integration.Listings;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public AgenciesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
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
     decimal price = 99000m,
     string currency = "EUR")
    {
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            object request =
                ListingTestHelpers.CreateValidListingRequest(
                    price: price,
                    agencyId: agencyId,
                    currency: currency);

            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync(
                    "/api/listings",
                    request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement json =
                await response.Content.ReadFromJsonAsync<JsonElement>();

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