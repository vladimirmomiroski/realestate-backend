using FluentAssertions;
using RealEstate.Tests.Integration.Auth;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Tests.Integration.Agencies;

public sealed partial class AgenciesEndpointTests
{
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
}
