using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Domain.Enums;
using RealEstate.Infrastructure.Geocoding.Geoapify;

namespace RealEstate.Tests.Unit.Infrastructure.Geocoding;

public sealed class GeoapifyListingGeocoderTests
{
    private const string ApiKey = "server-side-test-key";
    private const string OpenStreetMapAttribution =
        "© OpenStreetMap contributors";

    [Fact]
    public async Task SearchAsync_BuildsExactEncodedRequestAndMapsOrderedCandidate()
    {
        const string placeId = "opaque-place-id";
        string payload = SearchPayload(Result(
            placeId: placeId,
            formatted: "Партизански Одреди 10, Скопје",
            latitude: 42.0041,
            longitude: 21.4036,
            resultType: "building",
            street: "Партизански Одреди",
            houseNumber: "10",
            matchType: "full_match",
            confidence: 0.99,
            buildingConfidence: 0.98));

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            string searchText =
                "Партизански Одреди 10, Тафталиџе, Скопје, Карпош";
            string expected =
                "https://unit.geoapify.test/v1/geocode/search" +
                $"?text={Uri.EscapeDataString(searchText)}" +
                "&lang=mk&limit=5&format=json" +
                $"&filter={Uri.EscapeDataString("countrycode:mk")}" +
                $"&apiKey={ApiKey}";

            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri!.AbsoluteUri.Should().Be(expected);

            return JsonResponse(payload);
        });

        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(
                languageCode: "mk",
                city: "Скопје",
                municipality: "Карпош",
                addressLine: "Партизански Одреди 10",
                neighborhood: "Тафталиџе"),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.Success);
        result.Candidates.Should().ContainSingle().Which.Should().Be(
            new GeocodingCandidate(
                GeoapifyListingGeocoder.ProviderKey,
                placeId,
                "Партизански Одреди 10, Скопје",
                42.0041m,
                21.4036m,
                LocationPrecision.ExactAddress));
    }

    [Fact]
    public async Task SearchAsync_PreservesProviderOrderAndAppliesConfiguredCap()
    {
        string payload = SearchPayload(
            Result("first", "First", 41, 21),
            Result("second", "Second", 42, 22),
            Result("third", "Third", 43, 23));
        var handler = StubHttpMessageHandler.Returning(payload);
        GeoapifyListingGeocoder sut = CreateSut(handler, candidateLimit: 2);

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Candidates.Select(candidate => candidate.ResultReference)
            .Should().Equal("first", "second");
    }

    [Fact]
    public async Task SearchAsync_EmptyResultsReturnsSuccessfulEmptyCollection()
    {
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(SearchPayload()));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.Success);
        result.Candidates.Should().BeEmpty();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"results\":null}")]
    public async Task SearchAsync_MalformedPayloadFailsSafely(string payload)
    {
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.MalformedResponse);
        result.Candidates.Should().BeEmpty();
    }

    [Theory]
    [InlineData("place_id")]
    [InlineData("formatted")]
    [InlineData("lat")]
    [InlineData("lon")]
    [InlineData("datasource")]
    public async Task SearchAsync_MissingRequiredProviderFieldFailsSafely(
        string missingField)
    {
        Dictionary<string, object?> providerResult = Result(
            "place",
            "Display",
            42,
            21);
        providerResult.Remove(missingField);
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(SearchPayload(providerResult)));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.MalformedResponse);
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_NullResultEntryFailsSafely()
    {
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning("{\"results\":[null]}"));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.MalformedResponse);
    }

    [Theory]
    [InlineData(-90.0001, 21)]
    [InlineData(90.0001, 21)]
    [InlineData(42, -180.0001)]
    [InlineData(42, 180.0001)]
    public async Task SearchAsync_OutOfRangeCoordinatesFailSafely(
        double latitude,
        double longitude)
    {
        string payload = SearchPayload(
            Result("place", "Display", latitude, longitude));
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.MalformedResponse);
    }

    [Fact]
    public async Task SearchAsync_RejectsOnlyOversizedReferenceWithoutTruncation()
    {
        string oversized = new('a', 513);
        string payload = SearchPayload(
            Result(oversized, "Oversized", 42, 21),
            Result("accepted", "Accepted", 42, 21));
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Candidates.Should().ContainSingle()
            .Which.ResultReference.Should().Be("accepted");
    }

    [Fact]
    public async Task SearchAsync_CountsSupplementaryPlaneReferenceByUnicodeScalar()
    {
        string reference = string.Concat(Enumerable.Repeat("😀", 512));
        string payload = SearchPayload(Result(reference, "Display", 42, 21));
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Candidates.Should().ContainSingle()
            .Which.ResultReference.Should().Be(reference);
    }

    [Fact]
    public async Task SearchAsync_RejectsSupplementaryPlaneReferenceOverScalarLimit()
    {
        string reference = string.Concat(Enumerable.Repeat("😀", 513));
        string payload = SearchPayload(
            Result(reference, "Too long", 42, 21),
            Result("accepted", "Accepted", 42, 21));
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Candidates.Should().ContainSingle()
            .Which.ResultReference.Should().Be("accepted");
    }

    [Fact]
    public async Task SearchAsync_RejectsCandidateWhoseDisplayCannotFitCompleteValue()
    {
        string payload = SearchPayload(
            Result("too-long", new string('x', 501), 42, 21),
            Result("accepted", "Accepted", 42, 21));
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Candidates.Should().ContainSingle()
            .Which.ResultReference.Should().Be("accepted");
    }

    [Fact]
    public async Task SearchAsync_UnsupportedDatasourceFailsSafely()
    {
        Dictionary<string, object?> providerResult = Result(
            "place",
            "Display",
            42,
            21);
        providerResult["datasource"] = new Dictionary<string, object?>
        {
            ["sourcename"] = "geonames",
            ["attribution"] = "Different attribution"
        };

        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(SearchPayload(providerResult)));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.MalformedResponse);
        result.Candidates.Should().BeEmpty();
    }

    [Theory]
    [InlineData(OpenStreetMapAttribution)]
    [InlineData(null)]
    [InlineData("OpenStreetMap contributors — see source license")]
    public async Task SearchAsync_ApprovedDatasourceDoesNotDependOnAttributionText(
        string? attribution)
    {
        Dictionary<string, object?> providerResult = Result(
            "place",
            "Display",
            42,
            21);
        var datasource = new Dictionary<string, object?>
        {
            ["sourcename"] = "openstreetmap"
        };

        if (attribution is not null)
        {
            datasource["attribution"] = attribution;
        }

        providerResult["datasource"] = datasource;
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(SearchPayload(providerResult)));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.Success);
        result.Candidates.Should().ContainSingle()
            .Which.ResultReference.Should().Be("place");
    }

    [Theory]
    [InlineData("building", "full_match", 0.95, 0.95, 0.95, null, "Street", "10", null, null, LocationPrecision.ExactAddress)]
    [InlineData("building", "full_match", 0.95, 0.94, 0.95, null, "Street", "10", null, null, LocationPrecision.Street)]
    [InlineData("street", "match_by_street", 0.95, null, 0.95, null, "Street", null, null, null, LocationPrecision.Street)]
    [InlineData("suburb", null, 0.95, null, null, null, null, null, "Debar Maalo", null, LocationPrecision.Neighborhood)]
    [InlineData("city", "full_match", 0.95, null, null, 0.95, null, null, null, "Скопје", LocationPrecision.City)]
    [InlineData("district", "full_match", 1d, 1d, 1d, 1d, "Street", "10", "Suburb", "City", LocationPrecision.Approximate)]
    [InlineData("county", "full_match", 1d, 1d, 1d, 1d, "Street", "10", "Suburb", "City", LocationPrecision.Approximate)]
    [InlineData("future_type", "full_match", 1d, 1d, 1d, 1d, "Street", "10", "Suburb", "City", LocationPrecision.Approximate)]
    public async Task SearchAsync_MapsPrecisionConservatively(
        string resultType,
        string? matchType,
        double? confidence,
        double? buildingConfidence,
        double? streetConfidence,
        double? cityConfidence,
        string? street,
        string? houseNumber,
        string? suburb,
        string? city,
        LocationPrecision expected)
    {
        string payload = SearchPayload(Result(
            "place",
            "Display",
            42,
            21,
            resultType,
            street,
            houseNumber,
            suburb,
            city,
            matchType,
            confidence,
            buildingConfidence,
            streetConfidence,
            cityConfidence));
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Candidates.Should().ContainSingle()
            .Which.Precision.Should().Be(expected);
        result.Candidates.Should().NotContain(candidate =>
            candidate.Precision == LocationPrecision.Municipality);
    }

    [Fact]
    public async Task ResolveAsync_BuildsExactLookupAndPreservesOpaqueReference()
    {
        const string reference = "opaque/+?=🚀";
        string payload = PlaceDetailsPayload(Result(
            reference,
            "Resolved display",
            41.9981,
            21.4254,
            resultType: "street",
            street: "Македонија",
            matchType: "full_match",
            streetConfidence: 0.99));

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            string expected =
                "https://unit.geoapify.test/v2/place-details" +
                $"?id={Uri.EscapeDataString(reference)}&apiKey={ApiKey}";

            request.RequestUri!.AbsoluteUri.Should().Be(expected);
            request.RequestUri.Query.Should().NotContain("features=");

            return JsonResponse(payload);
        });
        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference(
                GeoapifyListingGeocoder.ProviderKey,
                reference),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingResolutionOutcome.Success);
        result.Snapshot.Should().Be(
            new ResolvedGeocodingSnapshot(
                GeoapifyListingGeocoder.ProviderKey,
                reference,
                41.9981m,
                21.4254m,
                LocationPrecision.Street,
                "Resolved display"));
    }

    [Fact]
    public async Task ResolveAsync_EmptyFeaturesReturnsNotFound()
    {
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning("{\"features\":[]}"));

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference("geoapify", "missing"),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingResolutionOutcome.NotFound);
    }

    [Fact]
    public async Task ResolveAsync_HttpNotFoundReturnsNotFound()
    {
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(
                "{}",
                HttpStatusCode.NotFound));

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference("geoapify", "missing"),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingResolutionOutcome.NotFound);
    }

    [Fact]
    public async Task ResolveAsync_ChangedReferenceReturnsStale()
    {
        string payload = PlaceDetailsPayload(
            Result("changed", "Display", 42, 21));
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference("geoapify", "selected"),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingResolutionOutcome.Stale);
        result.Snapshot.Should().BeNull();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"features\":null}")]
    [InlineData("{\"features\":[null]}")]
    [InlineData("{\"features\":[{\"properties\":null}]}")]
    public async Task ResolveAsync_MalformedPayloadFailsSafely(string payload)
    {
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference("geoapify", "place"),
            CancellationToken.None);

        result.Outcome.Should().Be(
            GeocodingResolutionOutcome.MalformedResponse);
        result.Snapshot.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_OutOfRangeCoordinatesFailSafely()
    {
        string payload = PlaceDetailsPayload(
            Result("place", "Display", 91, 21));
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference("geoapify", "place"),
            CancellationToken.None);

        result.Outcome.Should().Be(
            GeocodingResolutionOutcome.MalformedResponse);
    }

    [Fact]
    public async Task ResolveAsync_UnsupportedReturnedDatasourceFailsSafely()
    {
        Dictionary<string, object?> providerResult = Result(
            "place",
            "Display",
            42,
            21);
        providerResult["datasource"] = new Dictionary<string, object?>
        {
            ["sourcename"] = "geonames",
            ["attribution"] = "Different attribution"
        };
        string payload = PlaceDetailsPayload(
            providerResult,
            omitDatasource: false);
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference("geoapify", "place"),
            CancellationToken.None);

        result.Outcome.Should().Be(
            GeocodingResolutionOutcome.MalformedResponse);
    }

    [Theory]
    [InlineData(OpenStreetMapAttribution)]
    [InlineData(null)]
    [InlineData("OpenStreetMap contributors — see source license")]
    public async Task ResolveAsync_ApprovedDatasourceDoesNotDependOnAttributionText(
        string? attribution)
    {
        Dictionary<string, object?> providerResult = Result(
            "place",
            "Display",
            42,
            21);
        var datasource = new Dictionary<string, object?>
        {
            ["sourcename"] = "openstreetmap"
        };

        if (attribution is not null)
        {
            datasource["attribution"] = attribution;
        }

        providerResult["datasource"] = datasource;
        string payload = PlaceDetailsPayload(
            providerResult,
            omitDatasource: false);
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference("geoapify", "place"),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingResolutionOutcome.Success);
        result.Snapshot!.ResultReference.Should().Be("place");
    }

    [Fact]
    public async Task ResolveAsync_OverlongDisplayMapsToNullWithoutTruncation()
    {
        string payload = PlaceDetailsPayload(
            Result("place", new string('x', 501), 42, 21));
        GeoapifyListingGeocoder sut = CreateSut(
            StubHttpMessageHandler.Returning(payload));

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference("geoapify", "place"),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingResolutionOutcome.Success);
        result.Snapshot!.DisplayName.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_OverlengthInputReferenceIsRejectedWithoutHttpCall()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP must not be called."));
        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference("geoapify", new string('x', 513)),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingResolutionOutcome.PermanentFailure);
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ResolveAsync_IllFormedInputReferenceIsRejectedWithoutHttpCall()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP must not be called."));
        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference("geoapify", "invalid\uD800reference"),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingResolutionOutcome.PermanentFailure);
        handler.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, GeocodingSearchOutcome.PermanentFailure, 1)]
    [InlineData(HttpStatusCode.Unauthorized, GeocodingSearchOutcome.PermanentFailure, 1)]
    [InlineData(HttpStatusCode.TooManyRequests, GeocodingSearchOutcome.RateLimited, 1)]
    [InlineData(HttpStatusCode.InternalServerError, GeocodingSearchOutcome.Unavailable, 2)]
    public async Task SearchAsync_MapsDeterministicHttpStatusWithinRetryPolicy(
        HttpStatusCode statusCode,
        GeocodingSearchOutcome expected,
        int expectedAttempts)
    {
        var handler = StubHttpMessageHandler.Returning("{}", statusCode);
        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(expected);
        handler.CallCount.Should().Be(expectedAttempts);
    }

    [Fact]
    public async Task SearchAsync_CallerCancellationPropagates()
    {
        var handler = new StubHttpMessageHandler((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return JsonResponse(SearchPayload());
        });
        GeoapifyListingGeocoder sut = CreateSut(handler);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        Func<Task> act = () => sut.SearchAsync(
            SearchInput(),
            cancellationSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ResolveAsync_CallerCancellationPropagates()
    {
        var handler = new StubHttpMessageHandler((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return JsonResponse("{\"features\":[]}");
        });
        GeoapifyListingGeocoder sut = CreateSut(handler);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        Func<Task> act = () => sut.ResolveAsync(
            new GeocodingReference("geoapify", "place"),
            cancellationSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static GeoapifyListingGeocoder CreateSut(
        HttpMessageHandler handler,
        int candidateLimit = 5)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://unit.geoapify.test/")
        };
        var options = Options.Create(new GeoapifyOptions
        {
            BaseUri = httpClient.BaseAddress.AbsoluteUri,
            ApiKey = ApiKey,
            CandidateLimit = candidateLimit
        });

        return new GeoapifyListingGeocoder(
            httpClient,
            options,
            NullLogger<GeoapifyListingGeocoder>.Instance);
    }

    private static GeocodingSearchInput SearchInput(
        string languageCode = "mk",
        string? city = "Скопје",
        string? municipality = "Центар",
        string? addressLine = "Македонија 10",
        string? neighborhood = null)
    {
        CanonicalListingLocation location = CanonicalListingLocation.From(
        [
            new CanonicalListingLocationInput(
                languageCode,
                city,
                municipality,
                addressLine,
                neighborhood)
        ]);

        return new GeocodingSearchInput(location.Translations.Single());
    }

    private static Dictionary<string, object?> Result(
        string placeId,
        string formatted,
        double latitude,
        double longitude,
        string? resultType = "unknown",
        string? street = null,
        string? houseNumber = null,
        string? suburb = null,
        string? city = null,
        string? matchType = null,
        double? confidence = null,
        double? buildingConfidence = null,
        double? streetConfidence = null,
        double? cityConfidence = null)
    {
        return new Dictionary<string, object?>
        {
            ["place_id"] = placeId,
            ["formatted"] = formatted,
            ["lat"] = latitude,
            ["lon"] = longitude,
            ["result_type"] = resultType,
            ["street"] = street,
            ["housenumber"] = houseNumber,
            ["suburb"] = suburb,
            ["city"] = city,
            ["rank"] = new Dictionary<string, object?>
            {
                ["match_type"] = matchType,
                ["confidence"] = confidence,
                ["confidence_building_level"] = buildingConfidence,
                ["confidence_street_level"] = streetConfidence,
                ["confidence_city_level"] = cityConfidence
            },
            ["datasource"] = new Dictionary<string, object?>
            {
                ["sourcename"] = "openstreetmap",
                ["attribution"] = OpenStreetMapAttribution
            }
        };
    }

    private static string SearchPayload(
        params Dictionary<string, object?>[] results)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["results"] = results
        });
    }

    private static string PlaceDetailsPayload(
        Dictionary<string, object?> result,
        bool omitDatasource = true)
    {
        var details = new Dictionary<string, object?>(result)
        {
            ["feature_type"] = "details"
        };

        if (omitDatasource)
        {
            details.Remove("datasource");
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["features"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["properties"] = details
                }
            }
        });
    }

    private static HttpResponseMessage JsonResponse(
        string payload,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload)
        };
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> response)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public static StubHttpMessageHandler Returning(
            string payload,
            HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new StubHttpMessageHandler((_, _) =>
                JsonResponse(payload, statusCode));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(response(request, cancellationToken));
        }
    }
}
