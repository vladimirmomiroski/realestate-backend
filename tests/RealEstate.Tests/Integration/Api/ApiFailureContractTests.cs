using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealEstate.Application.Common;
using RealEstate.Infrastructure.Persistence;

namespace RealEstate.Tests.Integration.Api;

public sealed class ApiFailureContractTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string RequestIdentifierHeader = "X-Request-ID";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiFailureContractTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MalformedJson_ReturnsCanonicalValidationProblemDetails()
    {
        using var content = new StringContent(
            "{",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await _client.PostAsync(
            "/api/auth/login",
            content);

        JsonElement body = await AssertCanonicalProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            "/api/auth/login",
            validation: true);

        body.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvalidQueryBinding_ReturnsCanonicalValidationWithoutQueryInInstance()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/listings?page=not-a-number&ignored=sensitive");

        JsonElement body = await AssertCanonicalProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            "/api/listings",
            validation: true);

        body.GetProperty("errors").TryGetProperty("page", out _)
            .Should().BeTrue();
    }

    [Fact]
    public async Task InvalidEnumBinding_ReturnsCanonicalValidationProblemDetails()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/listings?listingType=not-a-listing-type");

        JsonElement body = await AssertCanonicalProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            "/api/listings",
            validation: true);

        body.GetProperty("errors").TryGetProperty("listingType", out _)
            .Should().BeTrue();
    }

    [Fact]
    public async Task MissingBody_ReturnsCanonicalValidationProblemDetails()
    {
        using var content = new ByteArrayContent(Array.Empty<byte>());
        content.Headers.ContentType = new MediaTypeHeaderValue(
            "application/json");

        HttpResponseMessage response = await _client.PostAsync(
            "/api/auth/login",
            content);

        JsonElement body = await AssertCanonicalProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ErrorCodes.ValidationFailed,
            "/api/auth/login",
            validation: true);

        body.GetProperty("errors").TryGetProperty("request", out _)
            .Should().BeTrue();
    }

    [Fact]
    public async Task UnmatchedApiRoute_ReturnsCanonicalNotFoundAndRejectsClientRequestId()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/does-not-exist?secret=not-in-instance");
        request.Headers.TryAddWithoutValidation(
            RequestIdentifierHeader,
            "client-selected-request-id");

        HttpResponseMessage response = await _client.SendAsync(request);

        JsonElement body = await AssertCanonicalProblemAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.ResourceNotFound,
            "/api/does-not-exist",
            validation: false);

        string responseRequestId = GetRequestIdentifier(response);
        responseRequestId.Should().NotBe("client-selected-request-id");
        body.GetProperty("traceId").GetString()
            .Should().Be(responseRequestId);
    }

    [Fact]
    public async Task UnsupportedMethod_ReturnsCanonicalProblemAndPreservesAllow()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            "/api/auth/login");

        HttpResponseMessage response = await _client.SendAsync(request);

        await AssertCanonicalProblemAsync(
            response,
            HttpStatusCode.MethodNotAllowed,
            ErrorCodes.RequestMethodNotAllowed,
            "/api/auth/login",
            validation: false);

        response.Content.Headers.Allow.Should().Contain("POST");
    }

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/health/database")]
    public async Task UnsupportedHealthMethod_ReturnsCanonicalProblemAndPreservesAllow(
        string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);

        HttpResponseMessage response = await _client.SendAsync(request);

        await AssertCanonicalProblemAsync(
            response,
            HttpStatusCode.MethodNotAllowed,
            ErrorCodes.RequestMethodNotAllowed,
            path,
            validation: false);

        response.Content.Headers.Allow.Should().Contain("GET");
    }

    [Fact]
    public async Task UnsupportedContentType_ReturnsCanonicalProblemDetails()
    {
        using var content = new StringContent(
            "{}",
            Encoding.UTF8,
            "text/plain");

        HttpResponseMessage response = await _client.PostAsync(
            "/api/auth/login",
            content);

        await AssertCanonicalProblemAsync(
            response,
            HttpStatusCode.UnsupportedMediaType,
            ErrorCodes.RequestMediaTypeNotSupported,
            "/api/auth/login",
            validation: false);
    }

    [Fact]
    public async Task MissingUpload_RemainsOrdinaryStaticNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/uploads/missing-12a-test-file.webp");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType.Should().BeNull();
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        GetRequestIdentifier(response).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ServedUpload_ReceivesRequestId()
    {
        string webRoot = Path.Combine(
            Path.GetTempPath(),
            $"realestate-12a-{Guid.NewGuid():N}");
        string uploadsRoot = Path.Combine(webRoot, "uploads");
        string filePath = Path.Combine(uploadsRoot, "request-id.webp");
        byte[] fileBytes = [0x52, 0x49, 0x46, 0x46];

        Directory.CreateDirectory(uploadsRoot);
        await File.WriteAllBytesAsync(filePath, fileBytes);

        try
        {
            string connectionString = GetInitializedConnectionString();
            using var factory = _factory.WithWebHostBuilder(
                builder =>
                {
                    builder.UseWebRoot(webRoot);
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
                });
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync(
                "/uploads/request-id.webp");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsByteArrayAsync())
                .Should().Equal(fileBytes);
            GetRequestIdentifier(response).Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    private string GetInitializedConnectionString()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        RealEstateDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<RealEstateDbContext>();

        return dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "The initialized test connection string is unavailable.");
    }

    [Fact]
    public async Task NonApiPrefix_RemainsOrdinaryNotFoundWithoutRequestId()
    {
        HttpResponseMessage response = await _client.GetAsync("/apiary");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType.Should().BeNull();
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        response.Headers.Contains(RequestIdentifierHeader).Should().BeFalse();
    }

    [Fact]
    public async Task HealthBody_RemainsDedicatedJsonAndReceivesRequestId()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/json");
        GetRequestIdentifier(response).Should().NotBeNullOrWhiteSpace();

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement;

        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("status", "app");
        body.GetProperty("status").GetString().Should().Be("ok");
        body.GetProperty("app").GetString().Should().Be("RealEstate.Api");
        body.TryGetProperty("code", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SuccessfulApiResponse_RemainsUnwrappedAndReceivesRequestId()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/listings?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        GetRequestIdentifier(response).Should().NotBeNullOrWhiteSpace();

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement;

        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(
                "items",
                "page",
                "pageSize",
                "totalCount",
                "totalPages",
                "hasNextPage",
                "hasPreviousPage");
        body.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
        body.TryGetProperty("data", out _).Should().BeFalse();
    }

    private static async Task<JsonElement> AssertCanonicalProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string expectedInstance,
        bool validation)
    {
        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.ToString()
            .Should().Be("application/problem+json");

        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        JsonElement body = document.RootElement;

        string[] expectedProperties = validation
            ? new[]
            {
                "type",
                "title",
                "status",
                "detail",
                "instance",
                "code",
                "traceId",
                "errors"
            }
            : new[]
            {
                "type",
                "title",
                "status",
                "detail",
                "instance",
                "code",
                "traceId"
            };

        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(expectedProperties);
        body.GetProperty("status").GetInt32()
            .Should().Be((int)expectedStatus);
        body.GetProperty("code").GetString().Should().Be(expectedCode);
        body.GetProperty("type").GetString()
            .Should().Be($"urn:realestate:error:{expectedCode}");
        body.GetProperty("instance").GetString()
            .Should().Be(expectedInstance);

        (string expectedTitle, string expectedDetail) = expectedCode switch
        {
            ErrorCodes.ValidationFailed =>
                ("Validation failed", "One or more validation errors occurred."),
            ErrorCodes.ResourceNotFound =>
                ("Resource not found", "The requested resource was not found."),
            ErrorCodes.RequestMethodNotAllowed =>
                (
                    "Method not allowed",
                    "The requested HTTP method is not supported for this resource."),
            ErrorCodes.RequestMediaTypeNotSupported =>
                (
                    "Unsupported media type",
                    "The request media type is not supported."),
            _ => throw new InvalidOperationException(
                $"No focused-test contract is defined for '{expectedCode}'.")
        };

        body.GetProperty("title").GetString().Should().Be(expectedTitle);
        body.GetProperty("detail").GetString().Should().Be(expectedDetail);

        string responseRequestId = GetRequestIdentifier(response);
        body.GetProperty("traceId").GetString()
            .Should().Be(responseRequestId);

        return body.Clone();
    }

    private static string GetRequestIdentifier(HttpResponseMessage response)
    {
        response.Headers.TryGetValues(
            RequestIdentifierHeader,
            out IEnumerable<string>? values).Should().BeTrue();

        return values!.Should().ContainSingle().Which;
    }
}
