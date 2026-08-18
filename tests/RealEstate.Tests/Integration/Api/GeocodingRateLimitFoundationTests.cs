using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstate.Api.Errors;
using RealEstate.Api.RateLimiting;
using RealEstate.Application.Common;

namespace RealEstate.Tests.Integration.Api;

public sealed class GeocodingRateLimitFoundationTests
{
    private static readonly DateTimeOffset InitialUtc =
        new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DependencyUnavailableProbe_UsesCanonicalSanitizedContract()
    {
        await using ProbeApplication probe = await ProbeApplication.StartAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/probe/geocoding-unavailable");
        request.Headers.TryAddWithoutValidation(
            RequestIdentifierMiddleware.HeaderName,
            "client-selected-id");

        HttpResponseMessage response = await probe.Client.SendAsync(request);

        JsonElement body = await AssertProblemAsync(
            response,
            HttpStatusCode.ServiceUnavailable,
            ErrorCodes.DependencyGeocodingUnavailable,
            "Geocoding unavailable",
            "Location search and confirmation are temporarily unavailable.",
            "/api/probe/geocoding-unavailable");
        string requestId = response.Headers
            .GetValues(RequestIdentifierMiddleware.HeaderName)
            .Should().ContainSingle().Which;
        requestId.Should().NotBe("client-selected-id");
        body.GetProperty("traceId").GetString().Should().Be(requestId);

        AssertNoSensitiveGeocodingData(body, actorId: null);
    }

    [Fact]
    public async Task NamedPolicy_PreservesAuthenticationAndIsolatesActorsAndReplenishes()
    {
        var timeProvider = new ManualTimeProvider(InitialUtc);
        await using ProbeApplication probe = await ProbeApplication.StartAsync(
            timeProvider,
            permitLimit: 2,
            windowSeconds: 60,
            includeRetryAfter: true);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            HttpResponseMessage anonymous = await probe.Client.PostAsync(
                "/api/probe/candidates",
                content: null);
            anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            anonymous.Headers.Contains("Retry-After").Should().BeFalse();
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            HttpResponseMessage invalid = await SendAsActorAsync(
                probe.Client,
                "/api/probe/candidates",
                "not-a-guid");
            invalid.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            invalid.Headers.Contains("Retry-After").Should().BeFalse();
        }

        Guid actorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid actorB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        (await SendAsActorAsync(
                probe.Client,
                "/api/probe/candidates",
                actorA.ToString()))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsActorAsync(
                probe.Client,
                "/api/probe/confirmation",
                actorA.ToString()))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage exhausted = await SendAsActorAsync(
            probe.Client,
            "/api/probe/candidates",
            actorA.ToString());

        JsonElement body = await AssertProblemAsync(
            exhausted,
            HttpStatusCode.TooManyRequests,
            ErrorCodes.RateLimitGeocodingExceeded,
            "Too many geocoding requests",
            "Too many location requests were made. Try again later.",
            "/api/probe/candidates");
        exhausted.Headers.GetValues("Retry-After")
            .Should().ContainSingle().Which.Should().Be("60");
        AssertNoSensitiveGeocodingData(body, actorA);

        (await SendAsActorAsync(
                probe.Client,
                "/api/probe/candidates",
                actorB.ToString()))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        timeProvider.Advance(TimeSpan.FromSeconds(60));

        (await SendAsActorAsync(
                probe.Client,
                "/api/probe/confirmation",
                actorA.ToString()))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RejectionWithoutRetryMetadata_DoesNotFabricateRetryAfter()
    {
        await using ProbeApplication probe = await ProbeApplication.StartAsync(
            new ManualTimeProvider(InitialUtc),
            permitLimit: 1,
            windowSeconds: 60,
            includeRetryAfter: false);
        string actor = Guid.NewGuid().ToString();

        (await SendAsActorAsync(
                probe.Client,
                "/api/probe/candidates",
                actor))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        HttpResponseMessage exhausted = await SendAsActorAsync(
            probe.Client,
            "/api/probe/candidates",
            actor);

        exhausted.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        exhausted.Headers.Contains("Retry-After").Should().BeFalse();
    }

    private static Task<HttpResponseMessage> SendAsActorAsync(
        HttpClient client,
        string path,
        string actor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.ActorHeader,
            actor);
        return client.SendAsync(request);
    }

    private static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        string title,
        string detail,
        string instance)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.ToString()
            .Should().Be(ApiFailureService.ContentType);

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement;
        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(
                "type",
                "title",
                "status",
                "detail",
                "instance",
                "code",
                "traceId");
        body.GetProperty("type").GetString()
            .Should().Be($"urn:realestate:error:{code}");
        body.GetProperty("title").GetString().Should().Be(title);
        body.GetProperty("status").GetInt32().Should().Be((int)status);
        body.GetProperty("detail").GetString().Should().Be(detail);
        body.GetProperty("instance").GetString().Should().Be(instance);
        body.GetProperty("code").GetString().Should().Be(code);

        string requestId = response.Headers
            .GetValues(RequestIdentifierMiddleware.HeaderName)
            .Should().ContainSingle().Which;
        body.GetProperty("traceId").GetString().Should().Be(requestId);
        return body.Clone();
    }

    private static void AssertNoSensitiveGeocodingData(
        JsonElement body,
        Guid? actorId)
    {
        string json = body.GetRawText();
        foreach (string forbidden in new[]
        {
            "geoapify",
            "address",
            "api key",
            "provider reference",
            "confirmation token"
        })
        {
            json.Should().NotContainEquivalentOf(forbidden);
        }

        if (actorId.HasValue)
        {
            json.Should().NotContainEquivalentOf(actorId.Value.ToString());
        }
    }

    private sealed class ProbeApplication : IAsyncDisposable
    {
        private readonly WebApplication _application;

        private ProbeApplication(WebApplication application, HttpClient client)
        {
            _application = application;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<ProbeApplication> StartAsync(
            ManualTimeProvider? timeProvider = null,
            int permitLimit = 2,
            int windowSeconds = 60,
            bool includeRetryAfter = true)
        {
            timeProvider ??= new ManualTimeProvider(InitialUtc);
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{GeocodingRateLimitOptions.SectionName}:PermitLimit"] =
                        permitLimit.ToString(),
                    [$"{GeocodingRateLimitOptions.SectionName}:WindowSeconds"] =
                        windowSeconds.ToString()
                });

            builder.Services.AddControllers();
            builder.Services.AddSingleton<ApiFailureService>();
            builder.Services.AddGeocodingRateLimiting(builder.Configuration);
            builder.Services.Replace(
                ServiceDescriptor.Singleton<IGeocodingActorRateLimiterFactory>(
                    new ManualRateLimiterFactory(
                        timeProvider,
                        permitLimit,
                        TimeSpan.FromSeconds(windowSeconds),
                        includeRetryAfter)));
            builder.Services
                .AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<
                    AuthenticationSchemeOptions,
                    TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        _ => { });
            builder.Services.AddAuthorization();

            WebApplication app = builder.Build();
            app.UseMiddleware<RequestIdentifierMiddleware>();
            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();

            app.MapPost(
                    "/api/probe/candidates",
                    HandleAuthenticatedProbeAsync)
                .RequireAuthorization()
                .RequireRateLimiting(GeocodingRateLimitPolicy.Name);
            app.MapPost(
                    "/api/probe/confirmation",
                    HandleAuthenticatedProbeAsync)
                .RequireAuthorization()
                .RequireRateLimiting(GeocodingRateLimitPolicy.Name);
            app.MapGet(
                "/api/probe/geocoding-unavailable",
                async (HttpContext context) =>
                {
                    ApiFailureService failureService = context.RequestServices
                        .GetRequiredService<ApiFailureService>();
                    await failureService.TryWriteAsync(
                        context,
                        failureService.Create(
                            context,
                            ApiFailureDescriptor.GeocodingUnavailable));
                });

            await app.StartAsync();
            return new ProbeApplication(app, app.GetTestClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _application.DisposeAsync();
        }

        private static async Task HandleAuthenticatedProbeAsync(
            HttpContext context)
        {
            string? actorClaim = context.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(actorClaim, out Guid actorId) ||
                actorId == Guid.Empty)
            {
                ApiFailureService failureService = context.RequestServices
                    .GetRequiredService<ApiFailureService>();
                await failureService.TryWriteAsync(
                    context,
                    failureService.Create(
                        context,
                        ApiFailureDescriptor.AuthenticationInvalidPrincipal));
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
        }
    }

    private sealed class TestAuthenticationHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestActor";
        public const string ActorHeader = "X-Test-Actor";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(
                    ActorHeader,
                    out Microsoft.Extensions.Primitives.StringValues actor))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            Claim[] claims =
            [
                new Claim(ClaimTypes.NameIdentifier, actor.ToString())
            ];
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override async Task HandleChallengeAsync(
            AuthenticationProperties properties)
        {
            Response.Headers.WWWAuthenticate = SchemeName;
            ApiFailureService failureService = Context.RequestServices
                .GetRequiredService<ApiFailureService>();
            await failureService.TryWriteAsync(
                Context,
                failureService.Create(
                    Context,
                    ApiFailureDescriptor.AuthenticationRequired));
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }

    private sealed class ManualRateLimiterFactory
        : IGeocodingActorRateLimiterFactory
    {
        private readonly TimeProvider _timeProvider;
        private readonly int _permitLimit;
        private readonly TimeSpan _window;
        private readonly bool _includeRetryAfter;

        public ManualRateLimiterFactory(
            TimeProvider timeProvider,
            int permitLimit,
            TimeSpan window,
            bool includeRetryAfter)
        {
            _timeProvider = timeProvider;
            _permitLimit = permitLimit;
            _window = window;
            _includeRetryAfter = includeRetryAfter;
        }

        public RateLimiter Create()
        {
            return new ManualFixedWindowRateLimiter(
                _timeProvider,
                _permitLimit,
                _window,
                _includeRetryAfter);
        }
    }

    private sealed class ManualFixedWindowRateLimiter : RateLimiter
    {
        private readonly Lock _lock = new();
        private readonly TimeProvider _timeProvider;
        private readonly int _permitLimit;
        private readonly TimeSpan _window;
        private readonly bool _includeRetryAfter;
        private DateTimeOffset _windowStartedAtUtc;
        private int _remainingPermits;

        public ManualFixedWindowRateLimiter(
            TimeProvider timeProvider,
            int permitLimit,
            TimeSpan window,
            bool includeRetryAfter)
        {
            _timeProvider = timeProvider;
            _permitLimit = permitLimit;
            _window = window;
            _includeRetryAfter = includeRetryAfter;
            _windowStartedAtUtc = timeProvider.GetUtcNow();
            _remainingPermits = permitLimit;
        }

        public override TimeSpan? IdleDuration => null;

        public override RateLimiterStatistics? GetStatistics()
        {
            lock (_lock)
            {
                ReplenishIfRequired();
                return new RateLimiterStatistics
                {
                    CurrentAvailablePermits = _remainingPermits,
                    CurrentQueuedCount = 0,
                    TotalFailedLeases = 0,
                    TotalSuccessfulLeases = 0
                };
            }
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            lock (_lock)
            {
                ReplenishIfRequired();

                if (permitCount <= _remainingPermits)
                {
                    _remainingPermits -= permitCount;
                    return new ManualLease(isAcquired: true, retryAfter: null);
                }

                TimeSpan retryAfter = _window -
                    (_timeProvider.GetUtcNow() - _windowStartedAtUtc);
                return new ManualLease(
                    isAcquired: false,
                    _includeRetryAfter ? retryAfter : null);
            }
        }

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(
            int permitCount,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(AttemptAcquireCore(permitCount));
        }

        private void ReplenishIfRequired()
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (now - _windowStartedAtUtc < _window)
            {
                return;
            }

            _windowStartedAtUtc = now;
            _remainingPermits = _permitLimit;
        }
    }

    private sealed class ManualLease : RateLimitLease
    {
        private readonly TimeSpan? _retryAfter;

        public ManualLease(bool isAcquired, TimeSpan? retryAfter)
        {
            IsAcquired = isAcquired;
            _retryAfter = retryAfter;
        }

        public override bool IsAcquired { get; }

        public override IEnumerable<string> MetadataNames =>
            _retryAfter.HasValue
                ? [MetadataName.RetryAfter.Name]
                : [];

        public override bool TryGetMetadata(
            string metadataName,
            out object? metadata)
        {
            if (_retryAfter.HasValue &&
                metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = _retryAfter.Value;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
