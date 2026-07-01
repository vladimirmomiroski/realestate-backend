using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Agencies;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingImagesEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public ListingImagesEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
    }

    private async Task<JsonElement> UploadImageAsync(
          Guid listingId,
          AuthenticatedTestUser owner,
           string fileName = "test-image.png",
          string contentType = "image/png")
    {
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            using var form = new MultipartFormDataContent();

            var imageBytes = new byte[]
            {
                     0x89, 0x50, 0x4E, 0x47
            };

            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            form.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                form);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private static MultipartFormDataContent CreateImageUploadContent(
        string fileName = "test-image.jpg")
    {
        var content = new MultipartFormDataContent();

        byte[] imageBytes =
        [
            0xFF, 0xD8, 0xFF, 0xE0,
                0x00, 0x10, 0x4A, 0x46,
                0x49, 0x46, 0x00, 0x01
        ];

        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        content.Add(fileContent, "file", fileName);

        return content;
    }

    private async Task<Guid> UploadImageAsAsync(
        Guid listingId,
        AuthenticatedTestUser user)
    {
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            using MultipartFormDataContent content = CreateImageUploadContent();

            HttpResponseMessage response = await _httpClient.PostAsync(
                $"/api/listings/{listingId}/images",
                content);

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
        Guid agencyMemberUserId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();

        var agency = AgencyTestHelpers.CreateAgency();

        agency.AddMember(ownerUserId, AgencyMemberRole.Owner);
        agency.AddMember(agencyMemberUserId, AgencyMemberRole.Agent);

        dbContext.Agencies.Add(agency);

        await dbContext.SaveChangesAsync();

        return agency.Id;
    }

    private async Task<Guid> CreateAgencyListingAsAsync(
        AuthenticatedTestUser owner,
        Guid agencyId)
    {
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            var request = ListingTestHelpers.CreateValidListingRequest(
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
}
