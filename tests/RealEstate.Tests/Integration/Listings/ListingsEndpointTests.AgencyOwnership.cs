using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Agencies;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task CreateListing_WithAgencyAsActiveMember_ReturnsCreated()
    {
        // Arrange
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var agency = AgencyTestHelpers.CreateAgency();

            agency.AddMember(user.UserId, AgencyMemberRole.Owner);

            dbContext.Agencies.Add(agency);

            await dbContext.SaveChangesAsync();

            agencyId = agency.Id;
        }

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = ListingTestHelpers.CreateValidListingRequest(
                agencyId: agencyId);

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/listings",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

            Guid listingId = json.GetProperty("id").GetGuid();

            using IServiceScope scope = _factory.Services.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var listing = await dbContext.Listings.SingleAsync(
                listing => listing.Id == listingId);

            listing.CreatedByUserId.Should().Be(user.UserId);
            listing.AgencyId.Should().Be(agencyId);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_WithMissingAgency_ReturnsNotFound()
    {
        // Arrange
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = ListingTestHelpers.CreateValidListingRequest(
                agencyId: Guid.NewGuid());

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/listings",
                request);

            // Assert
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
    public async Task CreateListing_WithAgencyAsNonMember_ReturnsForbidden()
    {
        // Arrange
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var agency = AgencyTestHelpers.CreateAgency();

            dbContext.Agencies.Add(agency);

            await dbContext.SaveChangesAsync();

            agencyId = agency.Id;
        }

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var request = ListingTestHelpers.CreateValidListingRequest(
                agencyId: agencyId);

            // Act
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/listings",
                request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetListings_WithAgencyIdFilter_ReturnsOnlyMatchingAgencyListings()
    {
        // Arrange
        AuthenticatedTestUser user = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid agencyId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

            var agency = AgencyTestHelpers.CreateAgency();

            agency.AddMember(user.UserId, AgencyMemberRole.Owner);

            dbContext.Agencies.Add(agency);

            await dbContext.SaveChangesAsync();

            agencyId = agency.Id;
        }

        Guid agencyListingId;
        Guid personalListingId;

        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            var agencyRequest = ListingTestHelpers.CreateValidListingRequest(
                agencyId: agencyId);

            HttpResponseMessage agencyCreateResponse =
                await _httpClient.PostAsJsonAsync("/api/listings", agencyRequest);

            agencyCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement agencyCreateJson =
                await agencyCreateResponse.Content.ReadFromJsonAsync<JsonElement>();

            agencyListingId = agencyCreateJson.GetProperty("id").GetGuid();
            agencyCreateJson.GetProperty("agencyId").GetGuid().Should().Be(agencyId);

            var personalRequest = ListingTestHelpers.CreateValidListingRequest(
                price: 125000);

            HttpResponseMessage personalCreateResponse =
                await _httpClient.PostAsJsonAsync("/api/listings", personalRequest);

            personalCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            JsonElement personalCreateJson =
                await personalCreateResponse.Content.ReadFromJsonAsync<JsonElement>();

            personalListingId = personalCreateJson.GetProperty("id").GetGuid();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            agencyListingId,
            ListingStatus.Active);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            personalListingId,
            ListingStatus.Active);

        // Act
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings?lang=en&agencyId={agencyId}&page=1&pageSize=20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("totalCount").GetInt32().Should().Be(1);

        JsonElement items = json.GetProperty("items");

        items.GetArrayLength().Should().Be(1);

        JsonElement firstListing = items[0];

        firstListing.GetProperty("id").GetGuid().Should().Be(agencyListingId);
        firstListing.GetProperty("id").GetGuid().Should().NotBe(personalListingId);
        firstListing.GetProperty("agencyId").GetGuid().Should().Be(agencyId);
    }
}
