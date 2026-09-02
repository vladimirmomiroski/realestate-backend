using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RealEstate.Application.Common;
using RealEstate.Domain.Enums;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed partial class ListingsEndpointTests
{
    [Fact]
    public async Task UpdateListing_WithoutToken_ReturnsCanonicalUnauthorized()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        _httpClient.ClearAuthorization();

        HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
            $"/api/listings/{listingId}",
            CreateValidUpdatePayload());

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            ErrorCodes.AuthenticationRequired,
            $"/api/listings/{listingId}",
            bearerChallenge: true);
    }

    [Fact]
    public async Task UpdateListing_WhenListingDoesNotExist_ReturnsCanonicalNotFound()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(user.AccessToken);
        Guid listingId = Guid.NewGuid();

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.NotFound,
                ErrorCodes.ResourceNotFound,
                $"/api/listings/{listingId}");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.PendingVerification)]
    public async Task UpdateListing_WhenPersonalCreatorCanManageDraft_ReturnsOk(
        UserStatus userStatus)
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, userStatus);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            JsonObject payload = CreateValidUpdatePayload();
            payload["price"] = 123_456m;

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                payload);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement body =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("price").GetDecimal().Should().Be(123_456m);
            body.GetProperty("createdByUserId").GetGuid().Should().Be(owner.UserId);
            body.GetProperty("status").GetString().Should().Be("Draft");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenPersonalCallerIsNotCreator_ReturnsForbidden()
    {
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        AuthenticatedTestUser nonowner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(nonowner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                $"/api/listings/{listingId}");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenPersonalCreatorIsDisabled_ReturnsForbidden()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetUserStatusAsync(owner.UserId, UserStatus.Disabled);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationAccountDisabled,
                $"/api/listings/{listingId}");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenAgencyOwnerMembershipIsActive_ReturnsOk()
    {
        (Guid listingId, Guid agencyId, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement body =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("agencyId").GetGuid().Should().Be(agencyId);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenAgencyAgentMembershipIsActive_ReturnsOk()
    {
        (Guid listingId, Guid agencyId, _) =
            await CreateAgencyListingWithOwnerAsync();
        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(agent.UserId, UserStatus.Active);
        await AddAgencyMemberAsync(
            agencyId,
            agent.UserId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Active);
        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(AgencyMemberRole.Manager, AgencyMemberStatus.Active)]
    [InlineData(AgencyMemberRole.Owner, AgencyMemberStatus.Pending)]
    [InlineData(AgencyMemberRole.Agent, AgencyMemberStatus.Pending)]
    [InlineData(AgencyMemberRole.Owner, AgencyMemberStatus.Disabled)]
    [InlineData(AgencyMemberRole.Agent, AgencyMemberStatus.Disabled)]
    public async Task UpdateListing_WhenAgencyMembershipCannotAuthor_ReturnsForbidden(
        AgencyMemberRole role,
        AgencyMemberStatus membershipStatus)
    {
        (Guid listingId, Guid agencyId, _) =
            await CreateAgencyListingWithOwnerAsync();
        AuthenticatedTestUser member =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(member.UserId, UserStatus.Active);
        await AddAgencyMemberAsync(
            agencyId,
            member.UserId,
            role,
            membershipStatus);
        _httpClient.AuthorizeAs(member.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                $"/api/listings/{listingId}");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenAgencyCallerIsNotMember_ReturnsForbidden()
    {
        (Guid listingId, _, _) = await CreateAgencyListingWithOwnerAsync();
        AuthenticatedTestUser nonmember =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(nonmember.UserId, UserStatus.Active);
        _httpClient.AuthorizeAs(nonmember.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenPendingVerificationAgencyAgent_ReturnsOk()
    {
        (Guid listingId, Guid agencyId, _) =
            await CreateAgencyListingWithOwnerAsync();
        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await SetUserStatusAsync(agent.UserId, UserStatus.PendingVerification);
        await AddAgencyMemberAsync(
            agencyId,
            agent.UserId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Active);
        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(AgencyStatus.PendingVerification)]
    [InlineData(AgencyStatus.Disabled)]
    [InlineData(AgencyStatus.Rejected)]
    public async Task UpdateListing_WhenAgencyIsInactive_DoesNotBlockOwner(
        AgencyStatus agencyStatus)
    {
        (Guid listingId, Guid agencyId, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();
        await SetAgencyStatusAsync(agencyId, agencyStatus);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenAgencyAgentAccountIsDisabled_ReturnsForbidden()
    {
        (Guid listingId, Guid agencyId, _) =
            await CreateAgencyListingWithOwnerAsync();
        AuthenticatedTestUser agent =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await AddAgencyMemberAsync(
            agencyId,
            agent.UserId,
            AgencyMemberRole.Agent,
            AgencyMemberStatus.Active);
        await SetUserStatusAsync(agent.UserId, UserStatus.Disabled);
        _httpClient.AuthorizeAs(agent.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationAccountDisabled,
                $"/api/listings/{listingId}");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(ListingStatus.Active)]
    [InlineData(ListingStatus.Archived)]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Rented)]
    public async Task UpdateListing_WhenAuthorizedListingIsNotDraft_ReturnsConflict(
        ListingStatus listingStatus)
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetListingStatusAsync(listingId, listingStatus);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictResourceState,
                $"/api/listings/{listingId}");
            (await GetListingStatusAsync(listingId)).Should().Be(listingStatus);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenUnauthorizedAndNonDraft_ReturnsForbiddenBeforeConflict()
    {
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetListingStatusAsync(listingId, ListingStatus.Active);
        AuthenticatedTestUser nonowner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(nonowner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                CreateValidUpdatePayload());

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenUnauthorizedAndSemanticallyInvalid_ReturnsForbiddenWithoutValidationDetails()
    {
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        AuthenticatedTestUser nonowner =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(nonowner.AccessToken);
        JsonObject payload = CreateValidUpdatePayload();
        payload["price"] = 0;

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                payload);

            JsonElement problem = await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                $"/api/listings/{listingId}");
            problem.TryGetProperty("errors", out _).Should().BeFalse();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenAuthorizedNonDraftAndSemanticallyInvalid_ReturnsConflictBeforeValidation()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await SetListingStatusAsync(listingId, ListingStatus.Active);
        _httpClient.AuthorizeAs(owner.AccessToken);
        JsonObject payload = CreateValidUpdatePayload();
        payload["price"] = 0;

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                payload);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictResourceState,
                $"/api/listings/{listingId}");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenAuthorizedDraftIsSemanticallyInvalid_ReturnsCanonicalValidationProblem()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        _httpClient.AuthorizeAs(owner.AccessToken);
        JsonObject payload = CreateValidUpdatePayload();
        payload["price"] = 0;

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                payload);

            await ApiFailureAssertions.AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                ErrorCodes.ValidationFailed,
                $"/api/listings/{listingId}",
                validationKey: "price");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_WhenRequiredJsonMemberIsOmitted_ReturnsCanonicalModelBindingProblem()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        _httpClient.AuthorizeAs(owner.AccessToken);
        JsonObject payload = CreateValidUpdatePayload();
        payload.Remove("price").Should().BeTrue();

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                payload);

            string responseText = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(
                HttpStatusCode.BadRequest,
                "the response body was {0}",
                responseText);
            response.Content.Headers.ContentType!.MediaType.Should()
                .Be("application/problem+json");

            using JsonDocument document = JsonDocument.Parse(responseText);
            JsonElement problem = document.RootElement;
            problem.GetProperty("code").GetString()
                .Should().Be(ErrorCodes.ValidationFailed);
            problem.GetProperty("instance").GetString()
                .Should().Be($"/api/listings/{listingId}");
            string traceId = problem.GetProperty("traceId").GetString()!;
            traceId.Should().NotBeNullOrWhiteSpace();
            response.Headers.GetValues("X-Request-ID")
                .Should().ContainSingle().Which.Should().Be(traceId);
            JsonElement errors = problem.GetProperty("errors");
            errors.EnumerateObject().Should().ContainSingle();
            errors.GetProperty("request").GetArrayLength().Should().BeGreaterThan(0);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_SameApartmentSubtype_UpdatesThroughHttp()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            JsonElement before = await GetManagementJsonAsync(listingId);
            JsonObject payload = CreateWritablePayload(before);
            payload["apartmentDetails"]!["floor"] = 7;

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                payload);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement body =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("propertyType").GetString().Should().Be("Apartment");
            body.GetProperty("apartmentDetails")
                .GetProperty("floor")
                .GetInt32()
                .Should()
                .Be(7);
            body.GetProperty("houseDetails").ValueKind
                .Should().Be(JsonValueKind.Null);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task UpdateListing_ManagementGetPutGet_RoundTripsCompleteReplacement()
    {
        (Guid listingId, Guid agencyId, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();
        Guid imageId = await AddListingImageAsync(listingId);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            JsonElement before = await GetManagementJsonAsync(listingId);
            JsonElement beforeEnglish = before.GetProperty("translations")
                .EnumerateArray()
                .Single(translation =>
                    translation.GetProperty("languageCode").GetString() == "en");
            Guid retainedEnglishId = beforeEnglish.GetProperty("id").GetGuid();
            Guid omittedMacedonianId = before.GetProperty("translations")
                .EnumerateArray()
                .Single(translation =>
                    translation.GetProperty("languageCode").GetString() == "mk")
                .GetProperty("id")
                .GetGuid();

            JsonObject payload = CreateWritablePayload(before);
            payload["listingType"] = "Rent";
            payload["propertyType"] = "House";
            payload["price"] = 222_000m;
            payload["currency"] = " usd ";
            payload["areaSquareMeters"] = 140m;
            payload["rooms"] = null;
            payload.Remove("bathrooms").Should().BeTrue();
            payload.Remove("heatingType").Should().BeTrue();
            payload.Remove("furnishingStatus").Should().BeTrue();
            payload.Remove("condition").Should().BeTrue();
            payload.Remove("orientation").Should().BeTrue();
            payload["apartmentDetails"] = null;
            payload["houseDetails"] = new JsonObject
            {
                ["houseType"] = "Villa",
                ["numberOfFloors"] = 3,
                ["yardAreaSquareMeters"] = 410m
            };

            JsonObject retainedEnglish = payload["translations"]!
                .AsArray()
                .OfType<JsonObject>()
                .Single(translation =>
                    translation["languageCode"]!.GetValue<string>() == "en")
                .DeepClone()
                .AsObject();
            retainedEnglish["languageCode"] = " EN ";
            retainedEnglish["title"] = " Replaced English title ";
            retainedEnglish["description"] = null;
            retainedEnglish.Remove("city").Should().BeTrue();

            payload["translations"] = new JsonArray
            {
                retainedEnglish,
                new JsonObject
                {
                    ["languageCode"] = " DE ",
                    ["title"] = " Neues Haus ",
                    ["description"] = " Neue Beschreibung ",
                    ["addressLine"] = " Neue Adresse ",
                    ["city"] = " Berlin ",
                    ["municipality"] = " Mitte ",
                    ["neighborhood"] = " Zentrum "
                }
            };

            HttpResponseMessage updateResponse = await _httpClient.PutAsJsonAsync(
                $"/api/listings/{listingId}",
                payload);

            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement updated =
                await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
            JsonElement after = await GetManagementJsonAsync(listingId);

            updated.GetProperty("id").GetGuid()
                .Should().Be(after.GetProperty("id").GetGuid());
            updated.GetProperty("price").GetDecimal()
                .Should().Be(after.GetProperty("price").GetDecimal());
            updated.GetProperty("areaSquareMeters").GetDecimal()
                .Should().Be(after.GetProperty("areaSquareMeters").GetDecimal());
            updated.GetProperty("houseDetails")
                .GetProperty("yardAreaSquareMeters")
                .GetDecimal()
                .Should().Be(after.GetProperty("houseDetails")
                    .GetProperty("yardAreaSquareMeters")
                    .GetDecimal());
            updated.GetProperty("translations").EnumerateArray()
                .Select(translation => new
                {
                    Id = translation.GetProperty("id").GetGuid(),
                    LanguageCode = translation.GetProperty("languageCode").GetString(),
                    Title = translation.GetProperty("title").GetString()
                })
                .Should().BeEquivalentTo(after.GetProperty("translations")
                    .EnumerateArray()
                    .Select(translation => new
                    {
                        Id = translation.GetProperty("id").GetGuid(),
                        LanguageCode = translation.GetProperty("languageCode").GetString(),
                        Title = translation.GetProperty("title").GetString()
                    }));
            updated.GetProperty("images").EnumerateArray()
                .Select(image => image.GetProperty("id").GetGuid())
                .Should().Equal(after.GetProperty("images")
                    .EnumerateArray()
                    .Select(image => image.GetProperty("id").GetGuid()));
            (updated.GetProperty("modifiedAtUtc").GetDateTime() -
                after.GetProperty("modifiedAtUtc").GetDateTime())
                .Duration()
                .Should().BeLessThan(TimeSpan.FromMilliseconds(1));

            after.GetProperty("id").GetGuid().Should().Be(listingId);
            after.GetProperty("createdByUserId").GetGuid().Should().Be(owner.UserId);
            after.GetProperty("agencyId").GetGuid().Should().Be(agencyId);
            after.GetProperty("status").GetString().Should().Be("Draft");
            after.GetProperty("listingType").GetString().Should().Be("Rent");
            after.GetProperty("propertyType").GetString().Should().Be("House");
            after.GetProperty("price").GetDecimal().Should().Be(222_000m);
            after.GetProperty("currency").GetString().Should().Be("USD");
            after.GetProperty("areaSquareMeters").GetDecimal().Should().Be(140m);
            after.GetProperty("rooms").ValueKind.Should().Be(JsonValueKind.Null);
            after.GetProperty("bathrooms").ValueKind.Should().Be(JsonValueKind.Null);
            after.GetProperty("heatingType").GetString().Should().Be("Unknown");
            after.GetProperty("furnishingStatus").GetString().Should().Be("Unknown");
            after.GetProperty("condition").GetString().Should().Be("Unknown");
            after.GetProperty("orientation").GetString().Should().Be("Unknown");
            after.GetProperty("apartmentDetails").ValueKind
                .Should().Be(JsonValueKind.Null);
            after.GetProperty("houseDetails")
                .GetProperty("houseType")
                .GetString()
                .Should()
                .Be("Villa");

            JsonElement[] translations = after.GetProperty("translations")
                .EnumerateArray()
                .ToArray();
            translations.Select(translation =>
                    translation.GetProperty("languageCode").GetString())
                .Should().Equal("de", "en");
            JsonElement english = translations.Single(translation =>
                translation.GetProperty("languageCode").GetString() == "en");
            english.GetProperty("id").GetGuid().Should().Be(retainedEnglishId);
            english.GetProperty("title").GetString()
                .Should().Be("Replaced English title");
            english.GetProperty("description").ValueKind
                .Should().Be(JsonValueKind.Null);
            english.GetProperty("city").ValueKind.Should().Be(JsonValueKind.Null);
            translations.Should().NotContain(translation =>
                translation.GetProperty("id").GetGuid() == omittedMacedonianId);
            translations.Single(translation =>
                    translation.GetProperty("languageCode").GetString() == "de")
                .GetProperty("id")
                .GetGuid()
                .Should()
                .NotBeEmpty()
                .And.NotBe(retainedEnglishId)
                .And.NotBe(omittedMacedonianId);

            JsonElement image = after.GetProperty("images").EnumerateArray().Single();
            image.GetProperty("id").GetGuid().Should().Be(imageId);
            image.GetProperty("sortOrder").GetInt32().Should().Be(0);
            image.GetProperty("isPrimary").GetBoolean().Should().BeTrue();
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<JsonElement> GetManagementJsonAsync(Guid listingId)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings/{listingId}/management");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static JsonObject CreateWritablePayload(JsonElement management)
    {
        JsonObject payload = JsonNode.Parse(management.GetRawText())!.AsObject();

        foreach (string serverOwnedMember in new[]
        {
            "id",
            "createdByUserId",
            "agencyId",
            "status",
            "latitude",
            "longitude",
            "images",
            "createdAtUtc",
            "modifiedAtUtc"
        })
        {
            payload.Remove(serverOwnedMember);
        }

        foreach (JsonNode? translation in payload["translations"]!.AsArray())
        {
            translation!.AsObject().Remove("id");
        }

        return payload;
    }

    private static JsonObject CreateValidUpdatePayload()
    {
        return JsonNode.Parse(
            """
            {
              "listingType": "Sale",
              "propertyType": "Apartment",
              "price": 120000,
              "currency": "EUR",
              "areaSquareMeters": 60,
              "apartmentDetails": {
                "apartmentType": "Standard",
                "floor": 4,
                "totalFloors": 8,
                "hasElevator": true
              },
              "translations": [
                {
                  "languageCode": "en",
                  "title": "Valid replacement title",
                  "description": null,
                  "city": null
                }
              ]
            }
            """)!
            .AsObject();
    }
}
