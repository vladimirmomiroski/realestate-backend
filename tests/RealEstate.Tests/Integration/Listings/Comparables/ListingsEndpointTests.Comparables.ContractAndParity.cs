using FluentAssertions;
using RealEstate.Application.Listings.Queries.GetComparableListings;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RealEstate.Tests.Integration.Auth;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Agencies;
using System.Text.Json.Nodes;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{

    [Fact]
    public async Task GetComparables_UsesDefaultAndExplicitLimit()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid sourceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        for (int index = 0; index < 8; index++)
        {
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m + index,
                areaSquareMeters: 100m);
        }

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage fullResponse =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        HttpResponseMessage defaultResponse =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en");

        HttpResponseMessage explicitResponse =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=3");

        Guid[] fullIds =
            await ReadComparableIdsAsync(fullResponse);

        Guid[] defaultIds =
            await ReadComparableIdsAsync(defaultResponse);

        Guid[] explicitIds =
            await ReadComparableIdsAsync(explicitResponse);

        // Assert
        fullIds.Should().HaveCount(8);

        defaultIds.Should().Equal(
            fullIds.Take(6));

        explicitIds.Should().Equal(
            fullIds.Take(3));
    }

    [Fact]
    public async Task GetComparables_AcceptsBoundaryLimitsAndCapsResults()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid sourceId =
            await CreateActiveComparableAsync(
                owner,
                currency,
                price: 100_000m,
                areaSquareMeters: 100m);

        DateTime baseTimestamp =
            new(
                2026,
                2,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        var candidateIdsInCreationOrder =
            new List<Guid>();

        for (int index = 0; index < 13; index++)
        {
            Guid candidateId =
                await CreateActiveComparableAsync(
                    owner,
                    currency,
                    price: 100_000m,
                    areaSquareMeters: 100m,
                    createdAtUtc:
                        baseTimestamp.AddMinutes(index));

            candidateIdsInCreationOrder.Add(
                candidateId);
        }

        Guid[] expectedOrder =
            candidateIdsInCreationOrder
                .AsEnumerable()
                .Reverse()
                .ToArray();

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage limitOneResponse =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=1");

        HttpResponseMessage limitTwelveResponse =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        Guid[] limitOneIds =
            await ReadComparableIdsAsync(
                limitOneResponse);

        Guid[] limitTwelveIds =
            await ReadComparableIdsAsync(
                limitTwelveResponse);

        // Assert
        limitOneIds.Should().Equal(
            expectedOrder.Take(1));

        limitTwelveIds.Should().Equal(
            expectedOrder.Take(12));

        limitOneIds.Should().HaveCount(1);
        limitTwelveIds.Should().HaveCount(12);

        limitTwelveIds.Should().NotContain(
            expectedOrder[12]);
    }

    [Fact]
    public async Task GetComparables_IgnoresCoordinatesRoomsAndApartmentDetails()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        DateTime olderTimestamp =
            new(
                2026,
                3,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        DateTime newerTimestamp =
            olderTimestamp.AddDays(1);

        object sourceRequest =
            ListingTestHelpers.CreateValidListingRequest(
                price: 100_000m,
                currency: currency,
                areaSquareMeters: 100m,
                rooms: null,
                latitude: null,
                longitude: null);

        Guid sourceId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                sourceRequest,
                title: "Ignored fields source",
                createdAtUtc: olderTimestamp);

        object nullFieldsCandidateRequest =
            ListingTestHelpers.CreateValidListingRequest(
                price: 100_000m,
                currency: currency,
                areaSquareMeters: 100m,
                rooms: null,
                latitude: null,
                longitude: null);

        Guid nullFieldsCandidateId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                nullFieldsCandidateRequest,
                title: "Null ignored fields candidate",
                createdAtUtc: olderTimestamp);

        JsonObject divergentCandidateRequest =
            JsonSerializer.SerializeToNode(
                ListingTestHelpers.CreateValidListingRequest(
                    price: 100_000m,
                    currency: currency,
                    areaSquareMeters: 100m,
                    rooms: 7m,
                    latitude: -45.123456m,
                    longitude: 120.654321m))!
                .AsObject();

        JsonObject divergentApartmentDetails =
            divergentCandidateRequest[
                "apartmentDetails"]!
                .AsObject();

        divergentApartmentDetails["floor"] = 19;
        divergentApartmentDetails["totalFloors"] = 20;
        divergentApartmentDetails["hasElevator"] = false;

        Guid divergentFieldsCandidateId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                divergentCandidateRequest,
                title: "Divergent ignored fields candidate",
                createdAtUtc: newerTimestamp);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        Guid[] returnedIds =
            await ReadComparableIdsAsync(response);

        // Assert
        returnedIds.Should().Equal(
            divergentFieldsCandidateId,
            nullFieldsCandidateId);
    }

    [Fact]
    public async Task GetComparables_PersonalAndAgencyCandidatesCompeteTogetherAndPreserveResponseShape()
    {
        // Arrange
        string currency =
            CreateUniqueCurrency();

        AuthenticatedTestUser owner =
            await AuthTestHelpers.RegisterAndLoginAsync(
                _httpClient);

        Guid agencyId =
            await CreateComparableAgencyWithOwnerAsync(
                owner.UserId);

        DateTime olderTimestamp =
            new(
                2026,
                4,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        DateTime newerTimestamp =
            olderTimestamp.AddDays(1);

        object sourceRequest =
            ListingTestHelpers.CreateValidListingRequest(
                price: 100_000m,
                currency: currency,
                areaSquareMeters: 100m,
                latitude: 41.998123m,
                longitude: 21.425456m);

        Guid sourceId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                sourceRequest,
                title: "Comparable source",
                createdAtUtc: olderTimestamp);

        object personalRequest =
            ListingTestHelpers.CreateValidListingRequest(
                price: 100_000m,
                currency: currency,
                areaSquareMeters: 100m,
                latitude: 41.900001m,
                longitude: 21.400001m);

        Guid personalCandidateId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                personalRequest,
                title: "Personal comparable",
                createdAtUtc: olderTimestamp);

        object agencyRequest =
            ListingTestHelpers.CreateValidListingRequest(
                price: 100_000m,
                agencyId: agencyId,
                currency: currency,
                areaSquareMeters: 100m,
                latitude: 42.100001m,
                longitude: 22.100001m);

        Guid agencyCandidateId =
            await CreateActiveComparableFromRequestAsync(
                owner,
                agencyRequest,
                title: "Agency comparable",
                createdAtUtc: newerTimestamp);

        _httpClient.ClearAuthorization();

        // Act
        HttpResponseMessage response =
            await _httpClient.GetAsync(
                $"/api/listings/{sourceId}/comparables?lang=en&limit=12");

        JsonElement json =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.OK);

        json.ValueKind.Should().Be(
            JsonValueKind.Array);

        json.GetArrayLength().Should().Be(2);

        Guid[] returnedIds =
            json.EnumerateArray()
                .Select(item =>
                    item.GetProperty("id").GetGuid())
                .ToArray();

        returnedIds.Should().Equal(
            agencyCandidateId,
            personalCandidateId);

        JsonElement agencyItem =
            json.EnumerateArray()
                .Single(item =>
                    item.GetProperty("id").GetGuid() ==
                    agencyCandidateId);

        JsonElement personalItem =
            json.EnumerateArray()
                .Single(item =>
                    item.GetProperty("id").GetGuid() ==
                    personalCandidateId);

        AssertSelectedTranslation(
            agencyItem,
            languageCode: "en",
            title: "Agency comparable",
            city: "Skopje",
            municipality: "Centar",
            neighborhood: "Center");

        AssertSelectedTranslation(
            personalItem,
            languageCode: "en",
            title: "Personal comparable",
            city: "Skopje",
            municipality: "Centar",
            neighborhood: "Center");

        agencyItem.GetProperty("agencyId")
            .GetGuid()
            .Should()
            .Be(agencyId);

        personalItem.GetProperty("agencyId")
            .ValueKind.Should()
            .Be(JsonValueKind.Null);

        agencyItem.GetProperty("latitude")
            .GetDecimal()
            .Should()
            .Be(42.100001m);

        agencyItem.GetProperty("longitude")
            .GetDecimal()
            .Should()
            .Be(22.100001m);

        personalItem.GetProperty("latitude")
            .GetDecimal()
            .Should()
            .Be(41.900001m);

        personalItem.GetProperty("longitude")
            .GetDecimal()
            .Should()
            .Be(21.400001m);

        foreach (JsonElement item in json.EnumerateArray())
        {
            item.GetProperty("apartmentDetails")
                .ValueKind.Should()
                .Be(JsonValueKind.Object);

            item.GetProperty("houseDetails")
                .ValueKind.Should()
                .Be(JsonValueKind.Null);

            item.GetProperty("images")
                .ValueKind.Should()
                .Be(JsonValueKind.Array);

            item.GetProperty("images")
                .GetArrayLength()
                .Should()
                .Be(0);

            item.GetProperty("primaryImageUrl")
                .ValueKind.Should()
                .Be(JsonValueKind.Null);

            item.TryGetProperty(
                    "score",
                    out _)
                .Should()
                .BeFalse();

            item.TryGetProperty(
                    "rank",
                    out _)
                .Should()
                .BeFalse();

            item.TryGetProperty(
                    "ranking",
                    out _)
                .Should()
                .BeFalse();

            item.TryGetProperty(
                    "comparableScore",
                    out _)
                .Should()
                .BeFalse();
        }
    }
}
