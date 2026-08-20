using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealEstate.Application.Listings.Geocoding;
using RealEstate.Infrastructure.Geocoding.Geoapify;
using RealEstate.Tests.Integration.Api;

namespace RealEstate.Tests.Unit.Infrastructure.Geocoding;

public sealed class GeoapifyResilienceTests
{
    private const string ApiKey = "I6_SECRET_API_KEY";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderTimeoutMapsToUnavailableForBothOperations(
        bool resolve)
    {
        var handler = new AsyncStubHttpMessageHandler((_, token) =>
            Task.FromException<HttpResponseMessage>(
                new TaskCanceledException(
                    "Simulated provider timeout.",
                    null,
                    token)));
        GeoapifyListingGeocoder sut = CreateSut(handler);

        if (resolve)
        {
            GeocodingResolutionResult result = await sut.ResolveAsync(
                new GeocodingReference("geoapify", "reference"),
                CancellationToken.None);

            result.Outcome.Should().Be(GeocodingResolutionOutcome.Unavailable);
        }
        else
        {
            GeocodingSearchResult result = await sut.SearchAsync(
                SearchInput(),
                CancellationToken.None);

            result.Outcome.Should().Be(GeocodingSearchOutcome.Unavailable);
        }

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ConfiguredOperationTimeoutCancelsHungTransport()
    {
        var handler = new AsyncStubHttpMessageHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Unreachable after cancellation.");
        });
        GeoapifyListingGeocoder sut = CreateSut(
            handler,
            operationTimeoutSeconds: 1);

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.Unavailable);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task TransientServerFailureRetriesOnceAndCanRecover()
    {
        int responseCount = 0;
        var handler = new AsyncStubHttpMessageHandler((_, _) =>
            Task.FromResult(++responseCount == 1
                ? Response("{}", HttpStatusCode.ServiceUnavailable)
                : Response(SearchPayload("result"))));

        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.Success);
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task TransportFailureRetriesOnceAndResolutionCanRecover()
    {
        int responseCount = 0;
        var handler = new AsyncStubHttpMessageHandler((_, _) =>
        {
            if (++responseCount == 1)
            {
                return Task.FromException<HttpResponseMessage>(
                    new HttpRequestException("Simulated network failure."));
            }

            return Task.FromResult(Response(PlaceDetailsPayload("reference")));
        });
        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingResolutionResult result = await sut.ResolveAsync(
            new GeocodingReference("geoapify", "reference"),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingResolutionOutcome.Success);
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task RetryExhaustionReturnsUnavailableAtExactAttemptBound()
    {
        var handler = new AsyncStubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException("Simulated network failure.")));
        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.Unavailable);
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task PermanentClientFailureIsNotRetried()
    {
        var handler = AsyncStubHttpMessageHandler.Returning(
            "{}",
            HttpStatusCode.BadRequest);
        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.PermanentFailure);
        handler.CallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotImplemented)]
    [InlineData(HttpStatusCode.HttpVersionNotSupported)]
    public async Task NonTransientServerFailureIsNotRetried(
        HttpStatusCode statusCode)
    {
        var handler = AsyncStubHttpMessageHandler.Returning("{}", statusCode);
        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.Unavailable);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task MalformedSuccessfulPayloadIsNotRetried()
    {
        var handler = AsyncStubHttpMessageHandler.Returning("not-json");
        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.MalformedResponse);
        handler.CallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderRateLimitIsNeverRetried(bool includeRetryAfter)
    {
        var handler = new AsyncStubHttpMessageHandler((_, _) =>
        {
            HttpResponseMessage response = Response(
                "{}",
                HttpStatusCode.TooManyRequests);

            if (includeRetryAfter)
            {
                response.Headers.RetryAfter =
                    new System.Net.Http.Headers.RetryConditionHeaderValue(
                        TimeSpan.FromSeconds(1));
            }

            return Task.FromResult(response);
        });
        GeoapifyListingGeocoder sut = CreateSut(handler);

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.RateLimited);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task CallerCancellationBeforeRetryDelayPropagatesImmediately()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new AsyncStubHttpMessageHandler((_, _) =>
        {
            cancellation.Cancel();
            return Task.FromResult(Response(
                "{}",
                HttpStatusCode.ServiceUnavailable));
        });
        GeoapifyListingGeocoder sut = CreateSut(
            handler,
            retryDelayMilliseconds: 1000);

        Func<Task> act = () => sut.SearchAsync(
            SearchInput(),
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RetriedOperationEmitsExactlyOneStructuredTerminalEvent()
    {
        using var logs = new CapturingLoggerProvider();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddProvider(logs));
        int responseCount = 0;
        var handler = new AsyncStubHttpMessageHandler((_, _) =>
            Task.FromResult(++responseCount == 1
                ? Response("{}", HttpStatusCode.InternalServerError)
                : Response(SearchPayload("result"))));
        GeoapifyListingGeocoder sut = CreateSut(
            handler,
            loggerFactory.CreateLogger<GeoapifyListingGeocoder>());

        GeocodingSearchResult result = await sut.SearchAsync(
            SearchInput(),
            CancellationToken.None);

        result.Outcome.Should().Be(GeocodingSearchOutcome.Success);
        CapturedLogEntry terminal = logs.Entries.Should().ContainSingle()
            .Which;
        terminal.EventId.Id.Should().Be(13600);
        terminal.Level.Should().Be(LogLevel.Information);
        terminal.Exception.Should().BeNull();
        terminal.Properties["ProviderKey"].Should().Be("geoapify");
        terminal.Properties["Operation"].Should().Be("search");
        terminal.Properties["Outcome"].Should().Be("Success");
        terminal.Properties["AttemptCount"].Should().Be(2);
        terminal.Properties["ElapsedMilliseconds"].Should().BeOfType<double>();
        terminal.Properties.Keys.Should().BeEquivalentTo(
            "ProviderKey",
            "Operation",
            "Outcome",
            "AttemptCount",
            "ElapsedMilliseconds",
            "{OriginalFormat}");
    }

    [Fact]
    public async Task TypedClientSuppressesSensitiveDefaultHttpLogsOnlyForGeoapify()
    {
        const string address = "I6 SECRET ADDRESS";
        const string reference = "I6_SECRET_REFERENCE";
        const string rawResponseValue = "I6_RAW_RESPONSE_SECRET";
        using var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Geoapify:BaseUri"] = "https://unit.geoapify.test/",
                ["Geoapify:ApiKey"] = ApiKey,
                ["Geoapify:MaxRetryAttempts"] = "0",
                ["Geoapify:RetryDelayMilliseconds"] = "0"
            })
            .Build();
        var geoapifyHandler = new AsyncStubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.Contains(
                    "place-details",
                    StringComparison.Ordinal))
            {
                return Task.FromResult(Response(PlaceDetailsPayload(
                    reference,
                    rawResponseValue)));
            }

            return Task.FromResult(Response(SearchPayload(
                "candidate",
                rawResponseValue)));
        });
        var unrelatedHandler = AsyncStubHttpMessageHandler.Returning("{}");

        services.AddLogging(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(logs));
        services.AddGeoapifyGeocoding(configuration);
        services
            .AddHttpClient<IListingGeocoder, GeoapifyListingGeocoder>()
            .ConfigurePrimaryHttpMessageHandler(() => geoapifyHandler);
        services
            .AddHttpClient("unrelated", client =>
                client.BaseAddress = new Uri("https://unrelated.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => unrelatedHandler);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IListingGeocoder geocoder = provider.GetRequiredService<IListingGeocoder>();

        await geocoder.SearchAsync(
            SearchInput(address),
            CancellationToken.None);
        await geocoder.ResolveAsync(
            new GeocodingReference("geoapify", reference),
            CancellationToken.None);

        IHttpClientFactory clientFactory =
            provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient unrelated = clientFactory.CreateClient("unrelated");
        using HttpResponseMessage _ = await unrelated.GetAsync(
            "probe",
            CancellationToken.None);

        string combinedLogs = string.Join(
            Environment.NewLine,
            logs.Entries.Select(entry => entry.Message));
        combinedLogs.Should().NotContain(ApiKey);
        combinedLogs.Should().NotContain(address);
        combinedLogs.Should().NotContain(reference);
        combinedLogs.Should().NotContain(rawResponseValue);
        combinedLogs.Should().NotContain("42.1234");
        logs.Entries.Count(entry => entry.EventId.Id == 13600)
            .Should().Be(2);
        logs.Entries.Should().NotContain(entry =>
            entry.Category.StartsWith(
                "System.Net.Http.HttpClient.IListingGeocoder",
                StringComparison.Ordinal));
        logs.Entries.Should().Contain(entry =>
            entry.Category.StartsWith(
                "System.Net.Http.HttpClient.unrelated",
                StringComparison.Ordinal));
    }

    private static GeoapifyListingGeocoder CreateSut(
        HttpMessageHandler handler,
        ILogger<GeoapifyListingGeocoder>? logger = null,
        int retryDelayMilliseconds = 0,
        int operationTimeoutSeconds = 10)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://unit.geoapify.test/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
        var options = Options.Create(new GeoapifyOptions
        {
            BaseUri = client.BaseAddress.AbsoluteUri,
            ApiKey = ApiKey,
            CandidateLimit = 5,
            OperationTimeoutSeconds = operationTimeoutSeconds,
            MaxRetryAttempts = 1,
            RetryDelayMilliseconds = retryDelayMilliseconds
        });

        return new GeoapifyListingGeocoder(
            client,
            options,
            logger ?? NullLogger<GeoapifyListingGeocoder>.Instance);
    }

    private static GeocodingSearchInput SearchInput(
        string addressLine = "Македонија 10")
    {
        CanonicalListingLocation location = CanonicalListingLocation.From(
        [
            new CanonicalListingLocationInput(
                "mk",
                "Скопје",
                "Центар",
                addressLine,
                null)
        ]);

        return new GeocodingSearchInput(location.Translations.Single());
    }

    private static string SearchPayload(
        string reference,
        string formatted = "Display")
    {
        return JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new
                {
                    datasource = new { sourcename = "openstreetmap" },
                    place_id = reference,
                    formatted,
                    lat = 42.1234m,
                    lon = 21.4321m,
                    result_type = "unknown"
                }
            }
        });
    }

    private static string PlaceDetailsPayload(
        string reference,
        string formatted = "Display")
    {
        return JsonSerializer.Serialize(new
        {
            features = new[]
            {
                new
                {
                    properties = new
                    {
                        feature_type = "details",
                        place_id = reference,
                        formatted,
                        lat = 42.1234m,
                        lon = 21.4321m
                    }
                }
            }
        });
    }

    private static HttpResponseMessage Response(
        string payload,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload)
        };
    }

    private sealed class AsyncStubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
            response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public static AsyncStubHttpMessageHandler Returning(
            string payload,
            HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new AsyncStubHttpMessageHandler((_, _) =>
                Task.FromResult(Response(payload, statusCode)));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return response(request, cancellationToken);
        }
    }
}
