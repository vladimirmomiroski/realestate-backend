using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RealEstate.Api.Errors;
using RealEstate.Application.Common.Health;

namespace RealEstate.Tests.Integration.Api;

[Collection(HealthEndpointTestCollection.Name)]
public sealed class HealthEndpointTests
{
    private const string ReadinessPath =
        "/api/health/readiness";

    private const string DatabaseAliasPath =
        "/api/health/database";

    private const string LivenessPath =
        "/api/health";

    private const string ReadinessLogCategory =
        "RealEstate.Api.Health.DatabaseReadiness";

    private const string CompletionLogCategory =
        "RealEstate.Api.Errors." +
        "ApiRequestCompletionLoggingMiddleware";

    private const string ExceptionLogCategory =
        "RealEstate.Api.Errors.ApiExceptionHandler";

    private const string RequestIdentifierHeader =
        "X-Request-ID";

    private const string UnavailableConnectionSecret =
        "health_password_sentinel";

    private const string UnavailableConnectionString =
        "Host=127.0.0.1;" +
        "Port=1;" +
        "Database=health_unavailable;" +
        "Username=health_user;" +
        "Password=" + UnavailableConnectionSecret + ";" +
        "Timeout=1;" +
        "Command Timeout=1;" +
        "Pooling=false";

    private readonly CustomWebApplicationFactory _baseFactory;

    public HealthEndpointTests(
        CustomWebApplicationFactory baseFactory)
    {
        _baseFactory = baseFactory;
    }

    [Fact]
    public async Task DatabaseReadiness_WithPostgreSqlAvailable_ReturnsExactAnonymousCorrelatedContractForBothRoutes()
    {
        var logs = new CapturingLoggerProvider();

        using WebApplicationFactory<Program> factory =
            _baseFactory.WithWebHostBuilder(builder =>
                builder.ConfigureLogging(logging =>
                    logging.AddProvider(logs)));

        using HttpClient client = factory.CreateClient();

        logs.Clear();

        using HttpResponseMessage readinessResponse =
            await client.GetAsync(ReadinessPath);

        string readinessBody =
            await AssertDatabaseResponseAsync(
                readinessResponse,
                HttpStatusCode.OK,
                "ok");

        AssertSuccessfulReadinessLogging(
            logs,
            readinessResponse,
            ReadinessPath);

        logs.Clear();

        using HttpResponseMessage aliasResponse =
            await client.GetAsync(DatabaseAliasPath);

        string aliasBody =
            await AssertDatabaseResponseAsync(
                aliasResponse,
                HttpStatusCode.OK,
                "ok");

        aliasBody.Should().Be(readinessBody);

        AssertSuccessfulReadinessLogging(
            logs,
            aliasResponse,
            DatabaseAliasPath);
    }

    [Fact]
    public async Task DatabaseUnavailable_LeavesLivenessHealthyAndReturnsSanitizedReadinessUnavailable()
    {
        var logs = new CapturingLoggerProvider();

        using var factory =
            new HealthTestWebApplicationFactory(
                logs,
                probe: null);

        using HttpClient client = factory.CreateClient();

        logs.Clear();

        using HttpResponseMessage livenessResponse =
            await client.GetAsync(LivenessPath);

        await AssertLivenessResponseAsync(livenessResponse);
        GetReadinessWarnings(logs).Should().BeEmpty();

        logs.Clear();

        using HttpResponseMessage readinessResponse =
            await client.GetAsync(ReadinessPath);

        string readinessBody =
            await AssertDatabaseResponseAsync(
                readinessResponse,
                HttpStatusCode.ServiceUnavailable,
                "unavailable");

        AssertFailedReadinessLogging(
            logs,
            readinessResponse,
            ReadinessPath,
            "False");

        AssertSanitized(
            readinessBody,
            GetSingleReadinessWarning(logs),
            UnavailableConnectionSecret,
            "127.0.0.1",
            "health_unavailable",
            "health_user",
            "Host=");

        logs.Clear();

        using HttpResponseMessage aliasResponse =
            await client.GetAsync(DatabaseAliasPath);

        string aliasBody =
            await AssertDatabaseResponseAsync(
                aliasResponse,
                HttpStatusCode.ServiceUnavailable,
                "unavailable");

        aliasBody.Should().Be(readinessBody);

        AssertFailedReadinessLogging(
            logs,
            aliasResponse,
            DatabaseAliasPath,
            "False");

        AssertSanitized(
            aliasBody,
            GetSingleReadinessWarning(logs),
            UnavailableConnectionSecret,
            "127.0.0.1",
            "health_unavailable",
            "health_user",
            "Host=");
    }

    [Fact]
    public async Task Liveness_DoesNotExecuteReadinessProbe()
    {
        var logs = new CapturingLoggerProvider();
        var probe = new ThrowingReadinessProbe(
            "readiness must not execute");

        using var factory =
            new HealthTestWebApplicationFactory(
                logs,
                probe);

        using HttpClient client = factory.CreateClient();

        logs.Clear();

        using HttpResponseMessage response =
            await client.GetAsync(LivenessPath);

        await AssertLivenessResponseAsync(response);
        probe.CallCount.Should().Be(0);
        GetReadinessWarnings(logs).Should().BeEmpty();
        GetHandledExceptionErrors(logs).Should().BeEmpty();
    }

    [Fact]
    public async Task DatabaseReadiness_WhenProbeReturnsFalse_ReturnsSanitizedUnavailableOnce()
    {
        var logs = new CapturingLoggerProvider();
        var probe = new FalseReadinessProbe();

        using var factory =
            new HealthTestWebApplicationFactory(
                logs,
                probe);

        using HttpClient client = factory.CreateClient();

        logs.Clear();

        using HttpResponseMessage response =
            await client.GetAsync(ReadinessPath);

        string body = await AssertDatabaseResponseAsync(
            response,
            HttpStatusCode.ServiceUnavailable,
            "unavailable");

        probe.CallCount.Should().Be(1);

        AssertFailedReadinessLogging(
            logs,
            response,
            ReadinessPath,
            "False");

        AssertSanitized(
            body,
            GetSingleReadinessWarning(logs),
            "false_probe_secret");
    }

    [Fact]
    public async Task DatabaseReadiness_WhenProbeThrows_ReturnsSanitizedUnavailableOnce()
    {
        const string ExceptionSecret =
            "Host=sentinel;Password=throwing_probe_secret;" +
            "Npgsql provider stack";

        var logs = new CapturingLoggerProvider();
        var probe = new ThrowingReadinessProbe(
            ExceptionSecret);

        using var factory =
            new HealthTestWebApplicationFactory(
                logs,
                probe);

        using HttpClient client = factory.CreateClient();

        logs.Clear();

        using HttpResponseMessage response =
            await client.GetAsync(ReadinessPath);

        string body = await AssertDatabaseResponseAsync(
            response,
            HttpStatusCode.ServiceUnavailable,
            "unavailable");

        probe.CallCount.Should().Be(1);

        AssertFailedReadinessLogging(
            logs,
            response,
            ReadinessPath,
            "Exception");

        AssertSanitized(
            body,
            GetSingleReadinessWarning(logs),
            ExceptionSecret,
            "throwing_probe_secret",
            "Npgsql provider stack");
    }

    [Fact]
    public async Task DatabaseReadiness_WhenInternalTimeoutFires_ReturnsSanitizedUnavailableOnce()
    {
        var logs = new CapturingLoggerProvider();
        var probe = new BlockingReadinessProbe();

        using var factory =
            new HealthTestWebApplicationFactory(
                logs,
                probe);

        using HttpClient client = factory.CreateClient();
        using var outerSafetyCancellation =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(10));

        logs.Clear();

        using HttpResponseMessage response =
            await client.GetAsync(
                ReadinessPath,
                outerSafetyCancellation.Token);

        string body = await AssertDatabaseResponseAsync(
            response,
            HttpStatusCode.ServiceUnavailable,
            "unavailable");

        outerSafetyCancellation.IsCancellationRequested
            .Should().BeFalse();
        probe.CallCount.Should().Be(1);

        await probe.CancellationObservedTask.WaitAsync(
            TimeSpan.FromSeconds(2));

        probe.CancellationObserved.Should().BeTrue();

        AssertFailedReadinessLogging(
            logs,
            response,
            ReadinessPath,
            "Timeout");

        AssertSanitized(
            body,
            GetSingleReadinessWarning(logs),
            "timeout_probe_secret");
    }

    [Fact]
    public async Task DatabaseReadiness_WhenClientAborts_EmitsNoSyntheticUnavailableWarningOrGenericError()
    {
        var logs = new CapturingLoggerProvider();
        var probe = new BlockingReadinessProbe();

        using var factory =
            new HealthTestWebApplicationFactory(
                logs,
                probe);

        using HttpClient client = factory.CreateClient();
        using var requestCancellation =
            new CancellationTokenSource();

        logs.Clear();

        Task<HttpResponseMessage> request = client.GetAsync(
            ReadinessPath,
            requestCancellation.Token);

        await probe.Entered.WaitAsync(
            TimeSpan.FromSeconds(5));

        requestCancellation.Cancel();

        Func<Task> act = async () =>
            await request;

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        probe.CallCount.Should().Be(1);

        await probe.CancellationObservedTask.WaitAsync(
            TimeSpan.FromSeconds(2));

        probe.CancellationObserved.Should().BeTrue();
        GetReadinessWarnings(logs).Should().BeEmpty();
        GetHandledExceptionErrors(logs).Should().BeEmpty();
    }

    private static async Task<string>
        AssertDatabaseResponseAsync(
            HttpResponseMessage response,
            HttpStatusCode expectedStatusCode,
            string expectedStatus)
    {
        response.StatusCode.Should().Be(expectedStatusCode);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/json");
        GetRequestIdentifier(response)
            .Should().NotBeNullOrWhiteSpace();

        string body = await response.Content
            .ReadAsStringAsync();

        using JsonDocument document =
            JsonDocument.Parse(body);

        JsonElement json = document.RootElement;

        json.EnumerateObject()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                "status",
                "database");

        json.EnumerateObject().Should().HaveCount(2);
        json.GetProperty("status").GetString()
            .Should().Be(expectedStatus);
        json.GetProperty("database").GetString()
            .Should().Be("PostgreSQL");

        return body;
    }

    private static async Task AssertLivenessResponseAsync(
        HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/json");
        GetRequestIdentifier(response)
            .Should().NotBeNullOrWhiteSpace();

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        JsonElement json = document.RootElement;

        json.EnumerateObject()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                "status",
                "app");

        json.EnumerateObject().Should().HaveCount(2);
        json.GetProperty("status").GetString()
            .Should().Be("ok");
        json.GetProperty("app").GetString()
            .Should().Be("RealEstate.Api");
    }

    private static void AssertSuccessfulReadinessLogging(
        CapturingLoggerProvider logs,
        HttpResponseMessage response,
        string route)
    {
        GetReadinessWarnings(logs).Should().BeEmpty();
        GetHandledExceptionErrors(logs).Should().BeEmpty();

        CapturedLogEntry completion =
            GetSingleCompletion(logs);

        AssertCompletion(
            completion,
            response,
            route,
            StatusCodes.Status200OK);
    }

    private static void AssertFailedReadinessLogging(
        CapturingLoggerProvider logs,
        HttpResponseMessage response,
        string route,
        string expectedReason)
    {
        CapturedLogEntry warning =
            GetSingleReadinessWarning(logs);

        warning.EventId.Id.Should().Be(12003);
        warning.Exception.Should().BeNull();
        warning.Properties.Keys.Should().BeEquivalentTo(
            "Reason",
            "{OriginalFormat}");
        warning.Properties["Reason"].Should()
            .Be(expectedReason);
        warning.ScopeProperties["RequestId"].Should()
            .Be(GetRequestIdentifier(response));

        GetHandledExceptionErrors(logs).Should().BeEmpty();

        CapturedLogEntry completion =
            GetSingleCompletion(logs);

        AssertCompletion(
            completion,
            response,
            route,
            StatusCodes.Status503ServiceUnavailable);
    }

    private static void AssertCompletion(
        CapturedLogEntry completion,
        HttpResponseMessage response,
        string route,
        int statusCode)
    {
        completion.Level.Should().Be(LogLevel.Information);
        completion.Properties["Method"].Should().Be("GET");
        completion.Properties["Route"].Should().Be(route);
        completion.Properties["StatusCode"].Should()
            .Be(statusCode);
        completion.Properties["RequestId"].Should()
            .Be(GetRequestIdentifier(response));
        completion.ScopeProperties["RequestId"].Should()
            .Be(GetRequestIdentifier(response));
    }

    private static CapturedLogEntry
        GetSingleReadinessWarning(
            CapturingLoggerProvider logs)
    {
        return GetReadinessWarnings(logs)
            .Should().ContainSingle().Subject;
    }

    private static IReadOnlyList<CapturedLogEntry>
        GetReadinessWarnings(
            CapturingLoggerProvider logs)
    {
        return logs.Entries
            .Where(entry =>
                entry.Category == ReadinessLogCategory &&
                entry.Level == LogLevel.Warning)
            .ToArray();
    }

    private static CapturedLogEntry GetSingleCompletion(
        CapturingLoggerProvider logs)
    {
        return logs.Entries
            .Where(entry =>
                entry.Category == CompletionLogCategory &&
                entry.EventId ==
                    ApiRequestCompletionLoggingMiddleware
                        .CompletionEvent)
            .Should().ContainSingle().Subject;
    }

    private static IReadOnlyList<CapturedLogEntry>
        GetHandledExceptionErrors(
            CapturingLoggerProvider logs)
    {
        return logs.Entries
            .Where(entry =>
                entry.Category == ExceptionLogCategory &&
                entry.EventId ==
                    ApiExceptionHandler
                        .HandledExceptionEvent)
            .ToArray();
    }

    private static void AssertSanitized(
        string body,
        CapturedLogEntry warning,
        params string[] forbiddenValues)
    {
        foreach (string forbiddenValue in forbiddenValues)
        {
            body.Should().NotContain(
                forbiddenValue);

            warning.Message.Should().NotContain(
                forbiddenValue);

            warning.Properties.Values.Any(value =>
                    value is not null &&
                    value.ToString()!.Contains(
                        forbiddenValue,
                        StringComparison.Ordinal))
                .Should().BeFalse();

            warning.ScopeProperties.Values.Any(value =>
                    value is not null &&
                    value.ToString()!.Contains(
                        forbiddenValue,
                        StringComparison.Ordinal))
                .Should().BeFalse();
        }

        warning.Exception.Should().BeNull();
    }

    private static string GetRequestIdentifier(
        HttpResponseMessage response)
    {
        response.Headers.TryGetValues(
                RequestIdentifierHeader,
                out IEnumerable<string>? values)
            .Should().BeTrue();

        return values.Should()
            .ContainSingle().Subject;
    }

    private sealed class HealthTestWebApplicationFactory(
        CapturingLoggerProvider logs,
        IDatabaseReadinessProbe? probe)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                UnavailableConnectionString);
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] =
                                UnavailableConnectionString
                        }));
            builder.ConfigureLogging(logging =>
                logging.AddProvider(logs));

            if (probe is null)
            {
                return;
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<
                    IDatabaseReadinessProbe>();
                services.AddSingleton<
                    IDatabaseReadinessProbe>(probe);
            });
        }
    }

    private sealed class FalseReadinessProbe
        : IDatabaseReadinessProbe
    {
        private int _callCount;

        public int CallCount =>
            Volatile.Read(ref _callCount);

        public Task<bool> CanConnectAsync(
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(false);
        }
    }

    private sealed class ThrowingReadinessProbe(
        string message) : IDatabaseReadinessProbe
    {
        private int _callCount;

        public int CallCount =>
            Volatile.Read(ref _callCount);

        public Task<bool> CanConnectAsync(
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            throw new InvalidOperationException(message);
        }
    }

    private sealed class BlockingReadinessProbe
        : IDatabaseReadinessProbe
    {
        private int _callCount;
        private int _cancellationObserved;

        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource
            _cancellationObservedSignal =
                new(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

        public Task Entered =>
            _entered.Task;

        public Task CancellationObservedTask =>
            _cancellationObservedSignal.Task;

        public int CallCount =>
            Volatile.Read(ref _callCount);

        public bool CancellationObserved =>
            Volatile.Read(ref _cancellationObserved) == 1;

        public async Task<bool> CanConnectAsync(
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            _entered.TrySetResult();

            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Exchange(
                    ref _cancellationObserved,
                    1);
                _cancellationObservedSignal.TrySetResult();
                throw;
            }

            return true;
        }
    }
}
