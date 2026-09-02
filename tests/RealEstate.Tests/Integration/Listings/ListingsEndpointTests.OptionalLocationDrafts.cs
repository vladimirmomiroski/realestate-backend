using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task DraftCreateAndUpdate_OmittedOptionalLocalizedLocationFieldsRemainNull()
    {
        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            JsonObject createPayload = JsonSerializer.SerializeToNode(
                ListingTestHelpers.CreateValidListingRequest(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!
                .AsObject();
            RemoveOptionalLocalizedLocationMembers(createPayload);

            HttpResponseMessage createResponse =
                await _httpClient.PostAsJsonAsync(
                    "/api/listings",
                    createPayload);

            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            JsonElement created = await createResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            Guid listingId = created.GetProperty("id").GetGuid();
            AssertOptionalLocalizedLocationIsNull(created);

            JsonObject updatePayload = CreateWritablePayload(
                await GetManagementJsonAsync(listingId));
            RemoveOptionalLocalizedLocationMembers(updatePayload);
            updatePayload["price"] = 654_321m;

            HttpResponseMessage updateResponse =
                await _httpClient.PutAsJsonAsync(
                    $"/api/listings/{listingId}",
                    updatePayload);

            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement updated = await updateResponse.Content
                .ReadFromJsonAsync<JsonElement>();
            updated.GetProperty("price").GetDecimal().Should().Be(654_321m);

            foreach (JsonElement translation in updated
                         .GetProperty("translations")
                         .EnumerateArray())
            {
                AssertOptionalLocalizedLocationIsNull(translation);
            }

            await using AsyncServiceScope scope =
                _factory.Services.CreateAsyncScope();
            RealEstateDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>();
            var persisted = await dbContext.Listings
                .AsNoTracking()
                .Where(listing => listing.Id == listingId)
                .SelectMany(listing => listing.Translations)
                .Select(translation => new
                {
                    translation.AddressLine,
                    translation.Municipality,
                    translation.Neighborhood
                })
                .ToListAsync();

            persisted.Should().NotBeEmpty();
            persisted.Should().OnlyContain(translation =>
                translation.AddressLine == null &&
                translation.Municipality == null &&
                translation.Neighborhood == null);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private static void RemoveOptionalLocalizedLocationMembers(
        JsonObject payload)
    {
        foreach (JsonNode? translation in payload["translations"]!.AsArray())
        {
            JsonObject translationObject = translation!.AsObject();
            translationObject.Remove("addressLine");
            translationObject.Remove("municipality");
            translationObject.Remove("neighborhood");
        }
    }

    private static void AssertOptionalLocalizedLocationIsNull(
        JsonElement translation)
    {
        translation.GetProperty("addressLine").ValueKind
            .Should().Be(JsonValueKind.Null);
        translation.GetProperty("municipality").ValueKind
            .Should().Be(JsonValueKind.Null);
        translation.GetProperty("neighborhood").ValueKind
            .Should().Be(JsonValueKind.Null);
    }
}
