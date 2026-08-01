using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
    public async Task CreateListing_WithMalformedJson_ReturnsCanonicalValidation()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            using var content = new StringContent(
                "{",
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(
                "/api/listings?ignored=secret",
                content);

            await AssertFrameworkValidationAsync(
                response,
                "/api/listings",
                "request");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_WithMissingBody_ReturnsCanonicalValidation()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            using var content = new ByteArrayContent(Array.Empty<byte>());
            content.Headers.ContentType = new MediaTypeHeaderValue(
                "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(
                "/api/listings",
                content);

            await AssertFrameworkValidationAsync(
                response,
                "/api/listings",
                "request");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task CreateListing_WithInvalidEnum_ReturnsCanonicalValidation()
    {
        JsonObject request = CreateRequestNode();
        request["listingType"] = "not-a-listing-type";

        HttpResponseMessage response = await PostListingAsNewUserAsync(request);

        await AssertFrameworkValidationAsync(
            response,
            "/api/listings",
            "listingType");
    }

    [Fact]
    public async Task CreateListing_WithInvalidPrice_ReturnsKeyedValidation()
    {
        JsonObject request = CreateRequestNode();
        request["price"] = 0;

        HttpResponseMessage response = await PostListingAsNewUserAsync(request);

        await AssertValidationAsync(response, "/api/listings", "price");
    }

    [Fact]
    public async Task CreateListing_WithCoordinateDependency_ReturnsRequestValidation()
    {
        JsonObject request = CreateRequestNode();
        request["longitude"] = null;

        HttpResponseMessage response = await PostListingAsNewUserAsync(request);

        await AssertValidationAsync(response, "/api/listings", "request");
    }

    [Fact]
    public async Task CreateListing_WithMissingNestedTranslationTitle_ReturnsJsonFacingKey()
    {
        JsonObject request = CreateRequestNode();
        JsonArray translations = request["translations"]!.AsArray();
        translations[1]!.AsObject()["title"] = "";

        HttpResponseMessage response = await PostListingAsNewUserAsync(request);

        await AssertValidationAsync(
            response,
            "/api/listings",
            "translations[1].title");
    }

    [Fact]
    public async Task GetListings_WithInvalidSearchField_ReturnsQueryKey()
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            "/api/listings?q=a&ignored=secret");

        await AssertValidationAsync(response, "/api/listings", "q");
    }

    [Fact]
    public async Task GetListings_WithInvalidRange_ReturnsRequestKey()
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            "/api/listings?minPrice=200&maxPrice=100&currency=EUR");

        await AssertValidationAsync(response, "/api/listings", "request");
    }

    [Fact]
    public async Task GetComparableListings_WithInvalidLimit_ReturnsLimitKey()
    {
        Guid listingId = Guid.NewGuid();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings/{listingId}/comparables?limit=13&ignored=secret");

        await AssertValidationAsync(
            response,
            $"/api/listings/{listingId}/comparables",
            "limit");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("my")]
    [InlineData("publish")]
    [InlineData("unpublish")]
    [InlineData("archive")]
    public async Task UserDependentListingEndpoint_WithNonGuidSubject_ReturnsInvalidPrincipal(
        string operation)
    {
        string token = AuthTestHelpers.CreateSignedToken(
            _factory,
            "not-a-guid",
            DateTime.UtcNow.AddMinutes(5));

        _httpClient.AuthorizeAs(token);

        try
        {
            Guid listingId = Guid.NewGuid();
            HttpResponseMessage response = await SendUserDependentRequestAsync(
                operation,
                listingId);

            await AssertFailureAsync(
                response,
                HttpStatusCode.Unauthorized,
                ErrorCodes.AuthenticationInvalidPrincipal,
                GetOperationPath(operation, listingId),
                bearerChallenge: true);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("create")]
    [InlineData("my")]
    [InlineData("publish")]
    [InlineData("unpublish")]
    [InlineData("archive")]
    public async Task UserDependentListingEndpoint_WithDeletedUser_ReturnsInvalidPrincipal(
        string operation)
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid listingId = IsLifecycleOperation(operation)
            ? await ListingTestHelpers.CreateListingAsync(_httpClient)
            : Guid.NewGuid();

        await AuthTestHelpers.DeleteUserAsync(_factory, user.UserId);
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            HttpResponseMessage response = await SendUserDependentRequestAsync(
                operation,
                listingId);

            await AssertFailureAsync(
                response,
                HttpStatusCode.Unauthorized,
                ErrorCodes.AuthenticationInvalidPrincipal,
                GetOperationPath(operation, listingId),
                bearerChallenge: true);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("create")]
    [InlineData("publish")]
    [InlineData("unpublish")]
    [InlineData("archive")]
    public async Task ProhibitedListingOperation_WithDisabledUser_ReturnsAccountDisabled(
        string operation)
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);

        Guid listingId = IsLifecycleOperation(operation)
            ? await ListingTestHelpers.CreateListingAsAsync(_httpClient, user)
            : Guid.NewGuid();

        await AuthTestHelpers.SetUserStatusAsync(
            _factory,
            user.UserId,
            UserStatus.Disabled);
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            HttpResponseMessage response = await SendUserDependentRequestAsync(
                operation,
                listingId);

            await AssertFailureAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationAccountDisabled,
                GetOperationPath(operation, listingId));
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task GetMyListings_WithDisabledUser_PreservesSuccess()
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        Guid listingId = await ListingTestHelpers.CreateListingAsAsync(
            _httpClient,
            user);

        await AuthTestHelpers.SetUserStatusAsync(
            _factory,
            user.UserId,
            UserStatus.Disabled);
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                "/api/listings/my?lang=en&page=1&pageSize=20");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid())
                .Should().Contain(listingId);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_WithPendingVerificationUser_ReturnsGenericForbidden()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish",
                null);

            await AssertFailureAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                $"/api/listings/{listingId}/publish");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Fact]
    public async Task PublishListing_WithPersonalNonOwner_ReturnsGenericForbidden()
    {
        (Guid listingId, _) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        AuthenticatedTestUser other =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await AuthTestHelpers.SetUserStatusAsync(
            _factory,
            other.UserId,
            UserStatus.Active);
        _httpClient.AuthorizeAs(other.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish",
                null);

            await AssertFailureAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                $"/api/listings/{listingId}/publish");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("nonmember")]
    [InlineData("inactive-member")]
    [InlineData("manager")]
    [InlineData("inactive-agency")]
    public async Task PublishAgencyListing_WithPermissionDenial_ReturnsGenericForbidden(
        string denial)
    {
        (Guid listingId, Guid agencyId, AuthenticatedTestUser owner) =
            await CreateAgencyListingWithOwnerAsync();
        AuthenticatedTestUser actor = owner;

        if (denial != "inactive-agency")
        {
            actor = await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
            await AuthTestHelpers.SetUserStatusAsync(
                _factory,
                actor.UserId,
                UserStatus.Active);
        }

        if (denial == "inactive-member")
        {
            await AddAgencyMemberAsync(
                agencyId,
                actor.UserId,
                AgencyMemberRole.Agent,
                AgencyMemberStatus.Pending);
        }
        else if (denial == "manager")
        {
            await AddAgencyMemberAsync(
                agencyId,
                actor.UserId,
                AgencyMemberRole.Manager,
                AgencyMemberStatus.Active);
        }
        else if (denial == "inactive-agency")
        {
            await SetAgencyStatusAsync(agencyId, AgencyStatus.Disabled);
        }

        _httpClient.AuthorizeAs(actor.AccessToken);

        try
        {
            HttpResponseMessage response = await _httpClient.PutAsync(
                $"/api/listings/{listingId}/publish",
                null);

            await AssertFailureAsync(
                response,
                HttpStatusCode.Forbidden,
                ErrorCodes.AuthorizationForbidden,
                $"/api/listings/{listingId}/publish");
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Archived)]
    public async Task GetListingById_WithHiddenStatus_ReturnsCanonicalNotFound(
        ListingStatus status)
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);
        await ListingTestHelpers.SetListingStatusAsync(_factory, listingId, status);

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings/{listingId}");

        await AssertFailureAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.ResourceNotFound,
            $"/api/listings/{listingId}");
    }

    [Fact]
    public async Task GetListingById_WithMissingListing_ReturnsCanonicalNotFound()
    {
        Guid listingId = Guid.NewGuid();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings/{listingId}");

        await AssertFailureAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.ResourceNotFound,
            $"/api/listings/{listingId}");
    }

    [Fact]
    public async Task GetComparableListings_WithHiddenSource_ReturnsCanonicalNotFound()
    {
        Guid listingId = await ListingTestHelpers.CreateListingAsync(_httpClient);

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings/{listingId}/comparables");

        await AssertFailureAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.ResourceNotFound,
            $"/api/listings/{listingId}/comparables");
    }

    [Fact]
    public async Task GetComparableListings_WithMissingSource_ReturnsCanonicalNotFound()
    {
        Guid listingId = Guid.NewGuid();

        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/listings/{listingId}/comparables");

        await AssertFailureAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.ResourceNotFound,
            $"/api/listings/{listingId}/comparables");
    }

    [Theory]
    [InlineData("publish")]
    [InlineData("unpublish")]
    [InlineData("archive")]
    public async Task LifecycleOperation_WithMissingListing_ReturnsCanonicalNotFound(
        string operation)
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        await AuthTestHelpers.SetUserStatusAsync(
            _factory,
            user.UserId,
            UserStatus.Active);
        Guid listingId = Guid.NewGuid();
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            HttpResponseMessage response = await SendUserDependentRequestAsync(
                operation,
                listingId);

            await AssertFailureAsync(
                response,
                HttpStatusCode.NotFound,
                ErrorCodes.ResourceNotFound,
                GetOperationPath(operation, listingId));
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    [Theory]
    [InlineData("publish", ListingStatus.Reserved)]
    [InlineData("publish", ListingStatus.Sold)]
    [InlineData("publish", ListingStatus.Rented)]
    [InlineData("publish", ListingStatus.Archived)]
    [InlineData("unpublish", ListingStatus.Reserved)]
    [InlineData("unpublish", ListingStatus.Sold)]
    [InlineData("unpublish", ListingStatus.Rented)]
    [InlineData("unpublish", ListingStatus.Archived)]
    [InlineData("archive", ListingStatus.Reserved)]
    [InlineData("archive", ListingStatus.Sold)]
    [InlineData("archive", ListingStatus.Rented)]
    public async Task LifecycleOperation_WithInvalidCurrentState_ReturnsConflictWithoutMutation(
        string operation,
        ListingStatus initialStatus)
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_httpClient);
        await AuthTestHelpers.SetUserStatusAsync(
            _factory,
            owner.UserId,
            UserStatus.Active);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            initialStatus);
        _httpClient.AuthorizeAs(owner.AccessToken);

        try
        {
            HttpResponseMessage response = await SendUserDependentRequestAsync(
                operation,
                listingId);

            await AssertFailureAsync(
                response,
                HttpStatusCode.Conflict,
                ErrorCodes.ConflictResourceState,
                GetOperationPath(operation, listingId));
            (await GetListingStatusAsync(listingId)).Should().Be(initialStatus);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<HttpResponseMessage> PostListingAsNewUserAsync(
        JsonObject request)
    {
        AuthenticatedTestUser user =
            await AuthTestHelpers.RegisterAndLoginAsync(_httpClient);
        _httpClient.AuthorizeAs(user.AccessToken);

        try
        {
            return await _httpClient.PostAsJsonAsync("/api/listings", request);
        }
        finally
        {
            _httpClient.ClearAuthorization();
        }
    }

    private async Task<HttpResponseMessage> SendUserDependentRequestAsync(
        string operation,
        Guid listingId)
    {
        return operation switch
        {
            "create" => await _httpClient.PostAsJsonAsync(
                "/api/listings",
                ListingTestHelpers.CreateValidListingRequest()),
            "my" => await _httpClient.GetAsync("/api/listings/my"),
            "publish" or "unpublish" or "archive" =>
                await _httpClient.PutAsync(
                    GetOperationPath(operation, listingId),
                    null),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private static string GetOperationPath(string operation, Guid listingId)
    {
        return operation switch
        {
            "create" => "/api/listings",
            "my" => "/api/listings/my",
            "publish" or "unpublish" or "archive" =>
                $"/api/listings/{listingId}/{operation}",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private static bool IsLifecycleOperation(string operation)
    {
        return operation is "publish" or "unpublish" or "archive";
    }

    private static JsonObject CreateRequestNode()
    {
        return JsonSerializer.SerializeToNode(
            ListingTestHelpers.CreateValidListingRequest(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            .AsObject();
    }

    private static async Task<JsonElement> AssertValidationAsync(
        HttpResponseMessage response,
        string instance,
        string key)
    {
        JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            instance,
            validationKey: key);

        body.GetProperty("title").GetString().Should().Be("Validation failed");
        body.GetProperty("detail").GetString().Should()
            .Be("One or more validation errors occurred.");
        return body;
    }

    private static async Task<JsonElement> AssertFrameworkValidationAsync(
        HttpResponseMessage response,
        string instance,
        string requiredKey)
    {
        string responseText = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "the response body was {0}",
            responseText);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/problem+json");

        using JsonDocument document = JsonDocument.Parse(responseText);
        JsonElement body = document.RootElement;
        string traceId = body.GetProperty("traceId").GetString()!;

        body.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(
                "type",
                "title",
                "status",
                "detail",
                "instance",
                "code",
                "traceId",
                "errors");
        body.GetProperty("type").GetString().Should()
            .Be("urn:realestate:error:validation.failed");
        body.GetProperty("title").GetString().Should().Be("Validation failed");
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("detail").GetString().Should()
            .Be("One or more validation errors occurred.");
        body.GetProperty("instance").GetString().Should().Be(instance);
        body.GetProperty("code").GetString().Should()
            .Be(ErrorCodes.ValidationFailed);
        body.GetProperty("errors").TryGetProperty(
            requiredKey,
            out JsonElement messages).Should().BeTrue();
        messages.GetArrayLength().Should().BeGreaterThan(0);
        response.Headers.GetValues("X-Request-ID").Should()
            .ContainSingle().Which.Should().Be(traceId);
        response.Headers.WwwAuthenticate.Should().BeEmpty();

        return body.Clone();
    }

    private static async Task<JsonElement> AssertFailureAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        string instance,
        bool bearerChallenge = false)
    {
        JsonElement body = await ApiFailureAssertions.AssertProblemAsync(
            response,
            status,
            code,
            instance,
            bearerChallenge: bearerChallenge);

        (string title, string detail) = code switch
        {
            ErrorCodes.AuthenticationInvalidPrincipal => (
                "Invalid authenticated principal",
                "The authenticated user could not be resolved."),
            ErrorCodes.AuthorizationAccountDisabled => (
                "Account disabled",
                "This account cannot perform this action."),
            ErrorCodes.AuthorizationForbidden => (
                "Forbidden",
                "You do not have permission to perform this action."),
            ErrorCodes.ResourceNotFound => (
                "Resource not found",
                "The requested resource was not found."),
            ErrorCodes.ConflictResourceState => (
                "Conflict",
                "The request conflicts with the current resource state."),
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
        };

        body.GetProperty("title").GetString().Should().Be(title);
        body.GetProperty("detail").GetString().Should().Be(detail);
        return body;
    }
}
