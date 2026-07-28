using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RealEstate.Api.Errors;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Common.Storage;
using RealEstate.Domain.Entities;
using RealEstate.Infrastructure.Persistence;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Tests.Integration.Listings;

namespace RealEstate.Tests.Integration.Api;

public sealed class ApiExceptionAndLoggingTests
    : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string CompletionCategory =
        "RealEstate.Api.Errors.ApiRequestCompletionLoggingMiddleware";
    private const string ExceptionCategory =
        "RealEstate.Api.Errors.ApiExceptionHandler";

    private readonly CapturingLoggerProvider _logs = new();
    private readonly ThrowingFileStorageService _storage = new();
    private readonly Chapter12BTestProbe _probe = new();
    private readonly Chapter12BWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiExceptionAndLoggingTests(
        CustomWebApplicationFactory baseFactory)
    {
        string connectionString = GetConnectionString(baseFactory);

        _factory = new Chapter12BWebApplicationFactory(
            connectionString,
            _logs,
            _storage,
            _probe);
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task SuccessfulApiRequest_LogsOneCompletionWithRouteTemplate()
    {
        _logs.Clear();

        using HttpResponseMessage response =
            await _client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CapturedLogEntry completion = GetSingleCompletion();

        completion.Level.Should().Be(LogLevel.Information);
        completion.Properties["Method"].Should().Be("GET");
        completion.Properties["Route"].Should().Be("/api/health");
        completion.Properties["StatusCode"].Should().Be(200);
        completion.Properties["ElapsedMilliseconds"]
            .Should().BeOfType<double>();
        completion.Properties.Keys.Should().BeEquivalentTo(
            "RequestId",
            "Method",
            "Route",
            "StatusCode",
            "ElapsedMilliseconds",
            "{OriginalFormat}");
        completion.ScopeProperties.Should().ContainKey("RequestId");

        AssertLogRequestIdMatchesResponse(completion, response);
    }

    [Fact]
    public async Task UnmatchedApiRequest_UsesFixedRouteAndDoesNotLogSecrets()
    {
        const string QuerySecret = "query-secret-12b-7f93";
        const string HeaderSecret = "header-secret-12b-a481";
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/chapter-12b-not-found?token={QuerySecret}");
        request.Headers.Add("X-Test-Secret", HeaderSecret);
        _logs.Clear();

        using HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        CapturedLogEntry completion = GetSingleCompletion();
        completion.Properties["Route"].Should()
            .Be(ApiRequestLogContext.UnmatchedRoute);
        completion.Properties["StatusCode"].Should().Be(404);

        AssertCustomLogsExclude(QuerySecret, HeaderSecret);
    }

    [Fact]
    public async Task UnexpectedApplicationFailure_ReturnsCanonical500AndOwnsLogs()
    {
        Guid resourceId = Guid.NewGuid();
        const string QuerySecret = "query-secret-12b-c315";
        const string HeaderSecret = "header-secret-12b-e207";
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/chapter-12b-test/application-failure/{resourceId}" +
            $"?invitationCode={QuerySecret}");
        request.Headers.Add("X-Test-Secret", HeaderSecret);
        _logs.Clear();

        using HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should()
            .Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.ToString().Should()
            .Be(ApiFailureService.ContentType);

        JsonElement body =
            await response.Content.ReadFromJsonAsync<JsonElement>();
        string requestId = GetRequestId(response);

        body.GetProperty("type").GetString().Should()
            .Be("urn:realestate:error:server.unexpected");
        body.GetProperty("title").GetString().Should()
            .Be("Unexpected server error");
        body.GetProperty("status").GetInt32().Should().Be(500);
        body.GetProperty("detail").GetString().Should()
            .Be("An unexpected error occurred.");
        body.GetProperty("instance").GetString().Should()
            .Be($"/api/chapter-12b-test/application-failure/{resourceId}");
        body.GetProperty("code").GetString().Should()
            .Be("server.unexpected");
        body.GetProperty("traceId").GetString().Should().Be(requestId);

        CapturedLogEntry completion = GetSingleCompletion();
        CapturedLogEntry error = GetSingleHandledException();
        const string RouteTemplate =
            "api/chapter-12b-test/application-failure/{id:guid}";

        completion.Properties["Route"].Should().Be(RouteTemplate);
        completion.Properties["StatusCode"].Should().Be(500);
        error.Level.Should().Be(LogLevel.Error);
        error.Properties["Route"].Should().Be(RouteTemplate);
        error.Properties["StatusCode"].Should().Be(500);
        error.Properties.Keys.Should().BeEquivalentTo(
            "RequestId",
            "Method",
            "Route",
            "StatusCode",
            "{OriginalFormat}");
        error.Exception.Should().BeOfType<InjectedApplicationException>();

        completion.Properties["RequestId"].Should().Be(requestId);
        error.Properties["RequestId"].Should().Be(requestId);
        completion.ScopeProperties["RequestId"].Should().Be(requestId);
        error.ScopeProperties["RequestId"].Should().Be(requestId);

        _logs.Entries.Should().NotContain(entry =>
            entry.Category ==
                "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware" &&
            entry.EventId.Id == 1);

        AssertCustomLogsExclude(QuerySecret, HeaderSecret);
    }

    [Fact]
    public async Task StorageFailure_ReturnsCanonical500WithoutPersistingImage()
    {
        (Guid listingId, AuthenticatedTestUser owner) =
            await ListingTestHelpers.CreateListingWithOwnerAsync(_client);
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(
            [0xFF, 0xD8, 0xFF, 0xE0]);
        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "storage-secret-name.jpg");
        _client.AuthorizeAs(owner.AccessToken);
        _logs.Clear();

        try
        {
            using HttpResponseMessage response = await _client.PostAsync(
                $"/api/listings/{listingId}/images",
                form);

            response.StatusCode.Should()
                .Be(HttpStatusCode.InternalServerError);
            JsonElement body =
                await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("code").GetString().Should()
                .Be("server.unexpected");
            body.GetProperty("traceId").GetString().Should()
                .Be(GetRequestId(response));
        }
        finally
        {
            _client.ClearAuthorization();
        }

        _storage.SaveListingImageCallCount.Should().Be(1);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();
        (await dbContext.Set<ListingImage>()
                .AsNoTracking()
                .AnyAsync(image => image.ListingId == listingId))
            .Should().BeFalse();

        GetSingleCompletion().Properties["StatusCode"].Should().Be(500);
        GetSingleHandledException().Exception.Should()
            .BeOfType<InjectedStorageException>();
        AssertCustomLogsExclude("storage-secret-name.jpg");
    }

    [Fact]
    public async Task RequestAbortedCancellation_IsNotConvertedTo500OrError()
    {
        using var cancellation = new CancellationTokenSource();
        _logs.Clear();

        Task<HttpResponseMessage> request = _client.GetAsync(
            "/api/chapter-12b-test/cancellation",
            cancellation.Token);

        await _probe.CancellationStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(10));
        cancellation.Cancel();

        Func<Task> act = async () => await request;
        await act.Should().ThrowAsync<OperationCanceledException>();
        await _probe.CancellationObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(10));

        CapturedLogEntry completion = GetSingleCompletion();
        completion.Properties["Route"].Should()
            .Be("api/chapter-12b-test/cancellation");
        completion.Properties["StatusCode"].Should().Be(499);
        GetHandledExceptions().Should().BeEmpty();
    }

    [Fact]
    public async Task ResponseStartedFailure_IsNotReplacedOrApplicationHandled()
    {
        _logs.Clear();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/chapter-12b-test/response-started");
        using HttpResponseMessage response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.Should().BeNull();

        await using Stream responseStream =
            await response.Content.ReadAsStreamAsync();
        byte[] expectedBytes = Encoding.UTF8.GetBytes("started-response");
        var buffer = new byte[expectedBytes.Length];
        await responseStream.ReadExactlyAsync(buffer);

        buffer.Should().Equal(expectedBytes);

        var remainder = new byte[1];
        Func<Task> readRemainder = async () =>
            await responseStream.ReadExactlyAsync(remainder);
        IOException exception = (await readRemainder.Should()
                .ThrowAsync<IOException>())
            .Which;
        exception.InnerException.Should()
            .BeOfType<InjectedResponseStartedException>();

        GetHandledExceptions().Should().BeEmpty();
        GetCompletions().Should().BeEmpty();
        _logs.Entries.Should().Contain(entry =>
            entry.Category ==
                "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware" &&
            entry.EventId.Id == 2 &&
            entry.Level == LogLevel.Warning);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private CapturedLogEntry GetSingleCompletion()
    {
        return GetCompletions().Should().ContainSingle().Subject;
    }

    private IReadOnlyList<CapturedLogEntry> GetCompletions()
    {
        return _logs.Entries
            .Where(entry =>
                entry.Category == CompletionCategory &&
                entry.EventId ==
                    ApiRequestCompletionLoggingMiddleware.CompletionEvent)
            .ToArray();
    }

    private CapturedLogEntry GetSingleHandledException()
    {
        return GetHandledExceptions().Should().ContainSingle().Subject;
    }

    private IReadOnlyList<CapturedLogEntry> GetHandledExceptions()
    {
        return _logs.Entries
            .Where(entry =>
                entry.Category == ExceptionCategory &&
                entry.EventId == ApiExceptionHandler.HandledExceptionEvent)
            .ToArray();
    }

    private static void AssertLogRequestIdMatchesResponse(
        CapturedLogEntry entry,
        HttpResponseMessage response)
    {
        string requestId = GetRequestId(response);
        entry.Properties["RequestId"].Should().Be(requestId);
        entry.ScopeProperties["RequestId"].Should().Be(requestId);
    }

    private static string GetRequestId(HttpResponseMessage response)
    {
        response.Headers.TryGetValues(
                RequestIdentifierMiddleware.HeaderName,
                out IEnumerable<string>? values)
            .Should().BeTrue();

        return values.Should().ContainSingle().Subject;
    }

    private void AssertCustomLogsExclude(params string[] secrets)
    {
        CapturedLogEntry[] customEntries = _logs.Entries
            .Where(entry =>
                entry.Category == CompletionCategory ||
                entry.Category == ExceptionCategory)
            .ToArray();

        foreach (string secret in secrets)
        {
            customEntries.Should().NotContain(entry =>
                entry.Message.Contains(secret, StringComparison.Ordinal) ||
                entry.Properties.Values.Any(value =>
                    ContainsSecret(value, secret)) ||
                entry.ScopeProperties.Values.Any(value =>
                    ContainsSecret(value, secret)));
        }
    }

    private static bool ContainsSecret(object? value, string secret)
    {
        return value is not null && value.ToString()!.Contains(
            secret,
            StringComparison.Ordinal);
    }

    private static string GetConnectionString(
        CustomWebApplicationFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        return dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "The initialized test connection string is unavailable.");
    }

    private sealed class Chapter12BWebApplicationFactory(
        string connectionString,
        CapturingLoggerProvider logs,
        ThrowingFileStorageService storage,
        Chapter12BTestProbe probe)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                connectionString);
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] =
                                connectionString
                        }));
            builder.ConfigureLogging(logging =>
                logging.AddProvider(logs));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<
                    DbContextOptions<RealEstateDbContext>>();
                services.RemoveAll<RealEstateDbContext>();
                services.AddDbContext<RealEstateDbContext>(
                    options => options.UseNpgsql(connectionString));

                services.RemoveAll<IFileStorageService>();
                services.AddSingleton<IFileStorageService>(storage);
                services.AddSingleton(probe);

                services.AddControllers().AddApplicationPart(
                    typeof(Chapter12BTestController).Assembly);
            });
        }
    }
}

[ApiController]
[Route("api/chapter-12b-test")]
public sealed class Chapter12BTestController : ControllerBase
{
    [HttpGet("application-failure/{id:guid}")]
    public IActionResult ThrowApplicationFailure(Guid id)
    {
        throw new InjectedApplicationException(
            "Injected application failure.");
    }

    [HttpGet("cancellation")]
    public async Task WaitForCancellation(
        Chapter12BTestProbe probe,
        CancellationToken cancellationToken)
    {
        probe.CancellationStarted.TrySetResult();

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        finally
        {
            probe.CancellationObserved.TrySetResult();
        }
    }

    [HttpGet("response-started")]
    public async Task ThrowAfterResponseStarts()
    {
        Response.StatusCode = StatusCodes.Status200OK;
        await Response.StartAsync();
        await Response.WriteAsync("started-response");
        await Response.Body.FlushAsync();

        throw new InjectedResponseStartedException(
            "Injected response-started failure.");
    }
}

public sealed class Chapter12BTestProbe
{
    public TaskCompletionSource CancellationStarted { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource CancellationObserved { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class ThrowingFileStorageService : IFileStorageService
{
    private int _saveListingImageCallCount;

    public int SaveListingImageCallCount =>
        Volatile.Read(ref _saveListingImageCallCount);

    public Task<StoredFileResult> SaveListingImageAsync(
        Guid listingId,
        UploadedFile file,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _saveListingImageCallCount);
        throw new InjectedStorageException(
            "Injected storage failure.");
    }

    public Task DeleteListingImageAsync(
        Guid listingId,
        string storedFileName,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<StoredFileResult> SaveUserAvatarAsync(
        Guid userId,
        UploadedFile file,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task DeleteUserAvatarAsync(
        Guid userId,
        string storedFileName,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<StoredFileResult> SaveAgencyLogoAsync(
        Guid agencyId,
        UploadedFile file,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task DeleteAgencyLogoAsync(
        Guid agencyId,
        string storedFileName,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

internal sealed class InjectedApplicationException(string message)
    : Exception(message);

internal sealed class InjectedStorageException(string message)
    : Exception(message);

internal sealed class InjectedResponseStartedException(string message)
    : Exception(message);
