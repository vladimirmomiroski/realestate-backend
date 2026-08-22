using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Commands.ConfirmListingLocation;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Application.Listings.Queries.SearchLocationCandidates;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Api;
using RealEstate.Tests.Integration.Auth;

namespace RealEstate.Tests.Integration.Listings;

public sealed class ListingGeocodingEndpointTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTimeOffset InitialUtc =
        new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly CustomWebApplicationFactory _factory;

    public ListingGeocodingEndpointTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Candidates_UsesStoredSelectedTranslationAndReturnsOpaqueContract()
    {
        var geocoder = new StubListingGeocoder();
        using WebApplicationFactory<Program> application = CreateApplication(geocoder);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);
        client.AuthorizeAs(owner.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/listings/{listingId}/location/candidates",
            new SearchLocationCandidatesRequest(" EN "));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/json");
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        JsonElement[] candidates = document.RootElement.EnumerateArray().ToArray();
        candidates.Should().HaveCount(2);
        candidates[0].EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(
                "label",
                "previewLatitude",
                "previewLongitude",
                "precision",
                "confirmationToken");
        candidates[0].GetProperty("label").GetString()
            .Should().Be("Resolved address");
        candidates[0].GetProperty("previewLatitude").GetDecimal()
            .Should().Be(41.9981m);
        candidates[0].GetProperty("previewLongitude").GetDecimal()
            .Should().Be(21.4254m);
        candidates[0].GetProperty("precision").GetString()
            .Should().Be("ExactAddress");
        string token = candidates[0].GetProperty("confirmationToken").GetString()!;
        token.Should().NotBeNullOrWhiteSpace()
            .And.NotContain("geoapify")
            .And.NotContain("place-1");
        candidates[1].GetProperty("label").GetString()
            .Should().Be("Broad result");
        candidates[1].GetProperty("precision").GetString()
            .Should().Be("Approximate");

        geocoder.SearchCallCount.Should().Be(1);
        GeocodingSearchInput input = geocoder.LastSearchInput!;
        input.Location.LanguageCode.Should().Be("en");
        input.Location.City.Should().Be("Skopje");
        input.Location.Municipality.Should().Be("Centar");
        input.Location.AddressLine.Should().Be("Center");
        input.Location.Neighborhood.Should().Be("Center");
    }

    [Theory]
    [InlineData(GeocodingSearchOutcome.RateLimited, HttpStatusCode.TooManyRequests,
        ErrorCodes.RateLimitGeocodingExceeded)]
    [InlineData(GeocodingSearchOutcome.Unavailable, HttpStatusCode.ServiceUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    [InlineData(GeocodingSearchOutcome.PermanentFailure, HttpStatusCode.ServiceUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    [InlineData(GeocodingSearchOutcome.MalformedResponse, HttpStatusCode.ServiceUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    public async Task Candidates_MapsProviderFailuresToCanonicalProblems(
        GeocodingSearchOutcome outcome,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        var geocoder = new StubListingGeocoder
        {
            SearchResult = GeocodingSearchResult.Failure(outcome)
        };
        using WebApplicationFactory<Program> application = CreateApplication(geocoder);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);
        client.AuthorizeAs(owner.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/listings/{listingId}/location/candidates",
            new SearchLocationCandidatesRequest("en"));

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            expectedStatus,
            expectedCode,
            $"/api/listings/{listingId}/location/candidates");
    }

    [Fact]
    public async Task Candidates_AnonymousAndUnauthorizedStateNeverCallProvider()
    {
        var geocoder = new StubListingGeocoder();
        using WebApplicationFactory<Program> application = CreateApplication(geocoder);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);

        HttpResponseMessage anonymous = await client.PostAsJsonAsync(
            $"/api/listings/{listingId}/location/candidates",
            new SearchLocationCandidatesRequest("en"));
        await ApiFailureAssertions.AssertProblemAsync(
            anonymous,
            HttpStatusCode.Unauthorized,
            ErrorCodes.AuthenticationRequired,
            $"/api/listings/{listingId}/location/candidates",
            bearerChallenge: true);

        AuthenticatedTestUser other = await AuthTestHelpers.RegisterAndLoginAsync(client);
        client.AuthorizeAs(other.AccessToken);
        HttpResponseMessage forbidden = await client.PostAsJsonAsync(
            $"/api/listings/{listingId}/location/candidates",
            new SearchLocationCandidatesRequest("en"));
        await ApiFailureAssertions.AssertProblemAsync(
            forbidden,
            HttpStatusCode.Forbidden,
            ErrorCodes.AuthorizationForbidden,
            $"/api/listings/{listingId}/location/candidates");
        geocoder.SearchCallCount.Should().Be(0);
        owner.UserId.Should().NotBe(other.UserId);
    }

    [Fact]
    public async Task Candidates_NonDraftReturnsConflictBeforeProvider()
    {
        var geocoder = new StubListingGeocoder();
        using WebApplicationFactory<Program> application = CreateApplication(geocoder);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);
        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);
        client.AuthorizeAs(owner.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/listings/{listingId}/location/candidates",
            new SearchLocationCandidatesRequest("en"));

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            ErrorCodes.ConflictResourceState,
            $"/api/listings/{listingId}/location/candidates");
        geocoder.SearchCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Confirmation_AcceptsTokenOnlyAndUsesResolvedSnapshot()
    {
        var geocoder = new StubListingGeocoder();
        using WebApplicationFactory<Program> application = CreateApplication(geocoder);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);
        client.AuthorizeAs(owner.AccessToken);
        string token = await GetFirstCandidateTokenAsync(client, listingId);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/listings/{listingId}/location",
            new
            {
                confirmationToken = token,
                latitude = -12.34m,
                longitude = -56.78m,
                providerKey = "client-provider"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement;
        body.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(
                "latitude",
                "longitude",
                "locationPrecision",
                "geocodedDisplayName",
                "locationConfirmedAtUtc");
        body.GetProperty("latitude").GetDecimal().Should().Be(41.9981m);
        body.GetProperty("longitude").GetDecimal().Should().Be(21.4254m);
        body.GetProperty("locationPrecision").GetString()
            .Should().Be("ExactAddress");
        body.GetProperty("geocodedDisplayName").GetString()
            .Should().Be("Resolved address");
        body.GetProperty("locationConfirmedAtUtc").GetDateTime()
            .Kind.Should().Be(DateTimeKind.Utc);
        geocoder.LastReference.Should().Be(
            new GeocodingReference("geoapify", "place-1"));
        (await response.Content.ReadAsStringAsync()).Should()
            .NotContain("providerKey")
            .And.NotContain("place-1");
    }

    [Fact]
    public async Task Confirmation_InvalidTokenUsesCanonicalValidationProblem()
    {
        var geocoder = new StubListingGeocoder();
        using WebApplicationFactory<Program> application = CreateApplication(geocoder);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);
        client.AuthorizeAs(owner.AccessToken);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/listings/{listingId}/location",
            new ConfirmListingLocationRequest("not-a-protected-token"));

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            $"/api/listings/{listingId}/location",
            "confirmationToken");
        geocoder.ResolveCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Confirmation_ExpiredTokenFailsBeforeProvider()
    {
        var geocoder = new StubListingGeocoder();
        var timeProvider = new ManualTimeProvider(InitialUtc);
        using WebApplicationFactory<Program> application = CreateApplication(
            geocoder,
            timeProvider: timeProvider);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);
        client.AuthorizeAs(owner.AccessToken);
        string token = await GetFirstCandidateTokenAsync(client, listingId);
        timeProvider.Advance(TimeSpan.FromMinutes(10));

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/listings/{listingId}/location",
            new ConfirmListingLocationRequest(token));

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            $"/api/listings/{listingId}/location",
            "confirmationToken");
        geocoder.ResolveCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Confirmation_StaleFingerprintFailsBeforeProvider()
    {
        var geocoder = new StubListingGeocoder();
        using WebApplicationFactory<Program> application = CreateApplication(geocoder);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);
        client.AuthorizeAs(owner.AccessToken);
        string token = await GetFirstCandidateTokenAsync(client, listingId);
        await ChangeAddressAsync(listingId);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/listings/{listingId}/location",
            new ConfirmListingLocationRequest(token));

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            ErrorCodes.ConflictResourceSetChanged,
            $"/api/listings/{listingId}/location");
        geocoder.ResolveCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Confirmation_TokenForAnotherListingIsRejected()
    {
        var geocoder = new StubListingGeocoder();
        using WebApplicationFactory<Program> application = CreateApplication(geocoder);
        using HttpClient client = application.CreateClient();
        AuthenticatedTestUser owner = await AuthTestHelpers.RegisterAndLoginAsync(client);
        Guid firstListing = await ListingTestHelpers.CreateListingAsAsync(client, owner);
        Guid secondListing = await ListingTestHelpers.CreateListingAsAsync(client, owner);
        client.AuthorizeAs(owner.AccessToken);
        string token = await GetFirstCandidateTokenAsync(client, firstListing);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/listings/{secondListing}/location",
            new ConfirmListingLocationRequest(token));

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            $"/api/listings/{secondListing}/location",
            "confirmationToken");
        geocoder.ResolveCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(GeocodingResolutionOutcome.NotFound, HttpStatusCode.Conflict,
        ErrorCodes.ConflictResourceSetChanged)]
    [InlineData(GeocodingResolutionOutcome.Stale, HttpStatusCode.Conflict,
        ErrorCodes.ConflictResourceSetChanged)]
    [InlineData(GeocodingResolutionOutcome.RateLimited, HttpStatusCode.TooManyRequests,
        ErrorCodes.RateLimitGeocodingExceeded)]
    [InlineData(GeocodingResolutionOutcome.Unavailable, HttpStatusCode.ServiceUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    [InlineData(GeocodingResolutionOutcome.PermanentFailure, HttpStatusCode.ServiceUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    [InlineData(GeocodingResolutionOutcome.MalformedResponse, HttpStatusCode.ServiceUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable)]
    public async Task Confirmation_MapsProviderOutcomes(
        GeocodingResolutionOutcome outcome,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        var geocoder = new StubListingGeocoder();
        using WebApplicationFactory<Program> application = CreateApplication(geocoder);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);
        client.AuthorizeAs(owner.AccessToken);
        string token = await GetFirstCandidateTokenAsync(client, listingId);
        geocoder.ResolutionResultFactory = _ =>
            GeocodingResolutionResult.Failure(outcome);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/listings/{listingId}/location",
            new ConfirmListingLocationRequest(token));

        await ApiFailureAssertions.AssertProblemAsync(
            response,
            expectedStatus,
            expectedCode,
            $"/api/listings/{listingId}/location");
    }

    [Fact]
    public async Task NamedPolicy_IsSharedByCandidatesAndConfirmation()
    {
        var geocoder = new StubListingGeocoder();
        using WebApplicationFactory<Program> application = CreateApplication(
            geocoder,
            permitLimit: 2,
            windowSeconds: 60);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);

        HttpResponseMessage anonymous = await client.PostAsJsonAsync(
            $"/api/listings/{listingId}/location/candidates",
            new SearchLocationCandidatesRequest("en"));
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.AuthorizeAs(owner.AccessToken);
        string token = await GetFirstCandidateTokenAsync(client, listingId);
        (await client.PutAsJsonAsync(
            $"/api/listings/{listingId}/location",
            new ConfirmListingLocationRequest(token)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage exhausted = await client.PostAsJsonAsync(
            $"/api/listings/{listingId}/location/candidates",
            new SearchLocationCandidatesRequest("en"));
        await ApiFailureAssertions.AssertProblemAsync(
            exhausted,
            HttpStatusCode.TooManyRequests,
            ErrorCodes.RateLimitGeocodingExceeded,
            $"/api/listings/{listingId}/location/candidates");
        exhausted.Headers.GetValues("Retry-After")
            .Should().ContainSingle().Which.Should().Be("60");
        geocoder.SearchCallCount.Should().Be(1);
        geocoder.ResolveCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Clear_IsNotRateLimitedAndReturnsNullableUnresolvedState()
    {
        var geocoder = new StubListingGeocoder();
        using WebApplicationFactory<Program> application = CreateApplication(
            geocoder,
            permitLimit: 1,
            windowSeconds: 60);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);
        client.AuthorizeAs(owner.AccessToken);

        await GetFirstCandidateTokenAsync(client, listingId);
        (await client.PostAsJsonAsync(
            $"/api/listings/{listingId}/location/candidates",
            new SearchLocationCandidatesRequest("en")))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        HttpResponseMessage response = await client.DeleteAsync(
            $"/api/listings/{listingId}/location");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement;
        foreach (string property in new[]
        {
            "latitude",
            "longitude",
            "locationPrecision",
            "geocodedDisplayName",
            "locationConfirmedAtUtc"
        })
        {
            body.GetProperty(property).ValueKind.Should().Be(JsonValueKind.Null);
        }
        geocoder.ResolveCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Clear_MapsNotFoundAndNonDraftWithoutProvider()
    {
        var geocoder = new StubListingGeocoder();
        using WebApplicationFactory<Program> application = CreateApplication(geocoder);
        using HttpClient client = application.CreateClient();
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(client);
        client.AuthorizeAs(owner.AccessToken);

        HttpResponseMessage missing = await client.DeleteAsync(
            $"/api/listings/{Guid.NewGuid()}/location");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await ListingTestHelpers.SetListingStatusAsync(
            _factory,
            listingId,
            ListingStatus.Active);
        HttpResponseMessage nonDraft = await client.DeleteAsync(
            $"/api/listings/{listingId}/location");
        await ApiFailureAssertions.AssertProblemAsync(
            nonDraft,
            HttpStatusCode.Conflict,
            ErrorCodes.ConflictResourceState,
            $"/api/listings/{listingId}/location");
        geocoder.SearchCallCount.Should().Be(0);
        geocoder.ResolveCallCount.Should().Be(0);
    }

    private WebApplicationFactory<Program> CreateApplication(
        StubListingGeocoder geocoder,
        ManualTimeProvider? timeProvider = null,
        int? permitLimit = null,
        int windowSeconds = 60)
    {
        string connectionString;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            connectionString = scope.ServiceProvider
                .GetRequiredService<RealEstateDbContext>()
                .Database.GetConnectionString()
                ?? throw new InvalidOperationException(
                    "The integration-test connection string is unavailable.");
        }

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString
                };

                if (permitLimit.HasValue)
                {
                    values["GeocodingRateLimit:PermitLimit"] =
                        permitLimit.Value.ToString();
                    values["GeocodingRateLimit:WindowSeconds"] =
                        windowSeconds.ToString();
                }

                configuration.AddInMemoryCollection(values);
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IListingGeocoder>();
                services.AddSingleton<IListingGeocoder>(geocoder);

                if (timeProvider is not null)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(timeProvider);
                }
            });
        });
    }

    private static async Task<string> GetFirstCandidateTokenAsync(
        HttpClient client,
        Guid listingId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/listings/{listingId}/location/candidates",
            new SearchLocationCandidatesRequest("en"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        return document.RootElement[0]
            .GetProperty("confirmationToken")
            .GetString()!;
    }

    private async Task ChangeAddressAsync(Guid listingId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        await dbContext.Set<ListingTranslation>()
            .Where(translation => translation.ListingId == listingId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                translation => translation.AddressLine,
                translation => translation.AddressLine + " changed"));
    }

    private sealed class StubListingGeocoder : IListingGeocoder
    {
        private int _searchCallCount;
        private int _resolveCallCount;

        public GeocodingSearchResult SearchResult { get; set; } =
            GeocodingSearchResult.Success(
            [
                new GeocodingCandidate(
                    "geoapify",
                    "place-1",
                    "Resolved address",
                    41.9981m,
                    21.4254m,
                    LocationPrecision.ExactAddress),
                new GeocodingCandidate(
                    "geoapify",
                    "place-2",
                    "Broad result",
                    41.9m,
                    21.4m,
                    LocationPrecision.Approximate)
            ]);

        public Func<GeocodingReference, GeocodingResolutionResult>
            ResolutionResultFactory { get; set; } = reference =>
                GeocodingResolutionResult.Success(
                    new ResolvedGeocodingSnapshot(
                        reference.ProviderKey,
                        reference.ResultReference,
                        41.9981m,
                        21.4254m,
                        LocationPrecision.ExactAddress,
                        "Resolved address"));

        public int SearchCallCount => Volatile.Read(ref _searchCallCount);
        public int ResolveCallCount => Volatile.Read(ref _resolveCallCount);
        public GeocodingSearchInput? LastSearchInput { get; private set; }
        public GeocodingReference? LastReference { get; private set; }

        public Task<GeocodingSearchResult> SearchAsync(
            GeocodingSearchInput input,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _searchCallCount);
            LastSearchInput = input;
            return Task.FromResult(SearchResult);
        }

        public Task<GeocodingResolutionResult> ResolveAsync(
            GeocodingReference reference,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _resolveCallCount);
            LastReference = reference;
            return Task.FromResult(ResolutionResultFactory(reference));
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }
}
