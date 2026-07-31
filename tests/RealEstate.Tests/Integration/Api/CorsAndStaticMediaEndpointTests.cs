using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RealEstate.Tests.Integration.Auth;
using RealEstate.Tests.Integration.Listings;

namespace RealEstate.Tests.Integration.Api;

[Collection(CorsAndStaticMediaTestCollection.Name)]
public sealed class CorsAndStaticMediaEndpointTests
{
    private const string AllowedOrigin =
        "https://frontend.example:7443";

    private const string DisallowedOrigin =
        "https://disallowed.example:7443";

    private const string RequestIdentifierHeader =
        "X-Request-ID";

    private const string InvalidCorsOriginMessage =
        "CORS allowed origins configuration contains an invalid origin.";

    private readonly CustomWebApplicationFactory _baseFactory;

    public CorsAndStaticMediaEndpointTests(
        CustomWebApplicationFactory baseFactory)
    {
        _baseFactory = baseFactory;
    }

    [Fact]
    public async Task ConfiguredCors_ActualApiRequests_AllowOnlyConfiguredOriginAndExposeRequestId()
    {
        using WebApplicationFactory<Program> configuredFactory =
            CreateFactory(
                [
                    $"  {AllowedOrigin}  ",
                    AllowedOrigin.ToUpperInvariant()
                ]);
        using HttpClient configuredClient =
            configuredFactory.CreateClient();

        using HttpResponseMessage allowedResponse =
            await SendWithOriginAsync(
                configuredClient,
                HttpMethod.Get,
                "/api/health",
                AllowedOrigin);

        allowedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertCorsAllowed(allowedResponse, AllowedOrigin);
        AssertRequestIdentifierExposed(allowedResponse);

        using HttpResponseMessage disallowedResponse =
            await SendWithOriginAsync(
                configuredClient,
                HttpMethod.Get,
                "/api/health",
                DisallowedOrigin);

        disallowedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertCorsNotApproved(disallowedResponse);

        using WebApplicationFactory<Program> missingFactory =
            CreateFactory();
        using HttpClient missingClient = missingFactory.CreateClient();
        using HttpResponseMessage missingResponse =
            await SendWithOriginAsync(
                missingClient,
                HttpMethod.Get,
                "/api/health",
                AllowedOrigin);

        missingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertCorsNotApproved(missingResponse);

        using WebApplicationFactory<Program> emptyFactory =
            CreateFactory([null, string.Empty, "   "]);
        using HttpClient emptyClient = emptyFactory.CreateClient();
        using HttpResponseMessage emptyResponse =
            await SendWithOriginAsync(
                emptyClient,
                HttpMethod.Get,
                "/api/health",
                AllowedOrigin);

        emptyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertCorsNotApproved(emptyResponse);
    }

    [Fact]
    public async Task ConfiguredCors_Preflight_AllowsBearerJsonAndMultipartRequestShape()
    {
        using WebApplicationFactory<Program> factory =
            CreateFactory([AllowedOrigin]);
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/listings");

        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add(
            "Access-Control-Request-Method",
            "POST");
        request.Headers.Add(
            "Access-Control-Request-Headers",
            "Authorization, Content-Type");

        using HttpResponseMessage response =
            await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AssertCorsAllowed(response, AllowedOrigin);
        GetCommaSeparatedHeaderValues(
                response,
                "Access-Control-Allow-Methods")
            .Should()
            .Contain(method => method.Equals(
                "POST",
                StringComparison.OrdinalIgnoreCase));
        GetCommaSeparatedHeaderValues(
                response,
                "Access-Control-Allow-Headers")
            .Should()
            .Contain(header => header.Equals(
                "Authorization",
                StringComparison.OrdinalIgnoreCase));
        GetCommaSeparatedHeaderValues(
                response,
                "Access-Control-Allow-Headers")
            .Should()
            .Contain(header => header.Equals(
                "Content-Type",
                StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("ftp://frontend.example")]
    [InlineData("https://*.frontend.example")]
    [InlineData("https://user:password@frontend.example")]
    [InlineData("https://frontend.example/path")]
    [InlineData("https://frontend.example/")]
    [InlineData("https://frontend.example?query=value")]
    [InlineData("https://frontend.example#fragment")]
    public async Task CorsConfiguration_InvalidNonemptyOrigin_FailsStartupWithSanitizedError(
        string invalidOrigin)
    {
        Exception? exception = await Record.ExceptionAsync(
            async () =>
            {
                using WebApplicationFactory<Program> factory =
                    CreateFactory([invalidOrigin]);
                using HttpClient client = factory.CreateClient();
                using HttpResponseMessage response =
                    await client.GetAsync("/api/health");
            });

        exception.Should().NotBeNull();

        GetExceptionChain(exception!)
            .Should()
            .Contain(item =>
                item is InvalidOperationException &&
                item.Message == InvalidCorsOriginMessage);

        GetExceptionChain(exception!)
            .Select(item => item.Message)
            .Should()
            .NotContain(message => message.Contains(
                invalidOrigin,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task CleanCheckout_UploadedMedia_IsCreatedServedAndCorsProtected()
    {
        string webRoot = CreateTemporaryWebRoot();
        string uploadsRoot = Path.Combine(webRoot, "uploads");
        byte[] expectedBytes = [0x89, 0x50, 0x4E, 0x47];

        Directory.Exists(uploadsRoot).Should().BeFalse();

        try
        {
            using (WebApplicationFactory<Program> factory =
                   CreateFactory(
                       [AllowedOrigin],
                       webRoot))
            using (HttpClient client = factory.CreateClient())
            {
                Directory.Exists(uploadsRoot).Should().BeFalse();

                (Guid listingId, AuthenticatedTestUser owner) =
                    await ListingTestHelpers
                        .CreateListingWithOwnerAsync(client);

                using var content = new MultipartFormDataContent();
                using var fileContent =
                    new ByteArrayContent(expectedBytes);
                fileContent.Headers.ContentType =
                    new MediaTypeHeaderValue("image/png");
                content.Add(
                    fileContent,
                    "file",
                    "clean-checkout.png");

                using var uploadRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"/api/listings/{listingId}/images")
                {
                    Content = content
                };
                uploadRequest.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        owner.AccessToken);
                uploadRequest.Headers.Add(
                    "Origin",
                    AllowedOrigin);

                using HttpResponseMessage uploadResponse =
                    await client.SendAsync(uploadRequest);

                uploadResponse.StatusCode.Should()
                    .Be(HttpStatusCode.Created);
                AssertCorsAllowed(
                    uploadResponse,
                    AllowedOrigin);

                using JsonDocument uploadBody =
                    JsonDocument.Parse(
                        await uploadResponse.Content
                            .ReadAsStringAsync());

                string mediaUrl = uploadBody.RootElement
                    .GetProperty("url")
                    .GetString()!;

                mediaUrl.Should().StartWith(
                    $"/uploads/listings/{listingId}/");

                string storedFilePath = Path.Combine(
                    webRoot,
                    mediaUrl
                        .TrimStart('/')
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar));

                Directory.Exists(uploadsRoot).Should().BeTrue();
                File.Exists(storedFilePath).Should().BeTrue();
                (await File.ReadAllBytesAsync(storedFilePath))
                    .Should()
                    .Equal(expectedBytes);

                using HttpResponseMessage allowedMediaResponse =
                    await SendWithOriginAsync(
                        client,
                        HttpMethod.Get,
                        mediaUrl,
                        AllowedOrigin);

                allowedMediaResponse.StatusCode.Should()
                    .Be(HttpStatusCode.OK);
                allowedMediaResponse.Content.Headers.ContentType!
                    .MediaType.Should()
                    .Be("image/png");
                (await allowedMediaResponse.Content
                        .ReadAsByteArrayAsync())
                    .Should()
                    .Equal(expectedBytes);
                allowedMediaResponse.Headers.Contains(
                        RequestIdentifierHeader)
                    .Should()
                    .BeTrue();
                AssertCorsAllowed(
                    allowedMediaResponse,
                    AllowedOrigin);
                AssertRequestIdentifierExposed(
                    allowedMediaResponse);

                using HttpResponseMessage disallowedMediaResponse =
                    await SendWithOriginAsync(
                        client,
                        HttpMethod.Get,
                        mediaUrl,
                        DisallowedOrigin);

                disallowedMediaResponse.StatusCode.Should()
                    .Be(HttpStatusCode.OK);
                (await disallowedMediaResponse.Content
                        .ReadAsByteArrayAsync())
                    .Should()
                    .Equal(expectedBytes);
                AssertCorsNotApproved(
                    disallowedMediaResponse);
            }
        }
        finally
        {
            DeleteTemporaryWebRoot(webRoot);
        }
    }

    private WebApplicationFactory<Program> CreateFactory(
        IReadOnlyList<string?>? allowedOrigins = null,
        string? webRoot = null)
    {
        return _baseFactory.WithWebHostBuilder(builder =>
        {
            var settings =
                new Dictionary<string, string?>();

            if (allowedOrigins is not null)
            {
                for (int index = 0;
                     index < allowedOrigins.Count;
                     index++)
                {
                    string key =
                        $"Cors:AllowedOrigins:{index}";
                    string value =
                        allowedOrigins[index] ?? string.Empty;

                    settings[key] = value;
                    builder.UseSetting(key, value);
                }
            }

            if (webRoot is not null)
            {
                builder.UseWebRoot(webRoot);
            }

            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        settings));
        });
    }

    private static async Task<HttpResponseMessage>
        SendWithOriginAsync(
            HttpClient client,
            HttpMethod method,
            string path,
            string origin)
    {
        using var request = new HttpRequestMessage(
            method,
            path);
        request.Headers.Add("Origin", origin);

        return await client.SendAsync(request);
    }

    private static void AssertCorsAllowed(
        HttpResponseMessage response,
        string expectedOrigin)
    {
        response.Headers.TryGetValues(
                "Access-Control-Allow-Origin",
                out IEnumerable<string>? values)
            .Should()
            .BeTrue();
        values.Should()
            .ContainSingle()
            .Which.Should()
            .Be(expectedOrigin);
    }

    private static void AssertRequestIdentifierExposed(
        HttpResponseMessage response)
    {
        GetCommaSeparatedHeaderValues(
                response,
                "Access-Control-Expose-Headers")
            .Should()
            .Contain(header => header.Equals(
                RequestIdentifierHeader,
                StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertCorsNotApproved(
        HttpResponseMessage response)
    {
        response.Headers.Contains(
                "Access-Control-Allow-Origin")
            .Should()
            .BeFalse();
    }

    private static string[] GetCommaSeparatedHeaderValues(
        HttpResponseMessage response,
        string headerName)
    {
        response.Headers.TryGetValues(
                headerName,
                out IEnumerable<string>? values)
            .Should()
            .BeTrue();

        return values!
            .SelectMany(value => value.Split(','))
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();
    }

    private static IEnumerable<Exception>
        GetExceptionChain(Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            yield return current;
        }
    }

    private static string CreateTemporaryWebRoot()
    {
        string webRoot = Path.Combine(
            Path.GetTempPath(),
            $"realestate-12n-{Guid.NewGuid():N}");

        Directory.CreateDirectory(webRoot);

        return webRoot;
    }

    private static void DeleteTemporaryWebRoot(
        string webRoot)
    {
        string fullWebRoot = Path.GetFullPath(webRoot);
        string fullTemporaryRoot = Path.GetFullPath(
            Path.GetTempPath());

        bool isExpectedTemporaryRoot =
            fullWebRoot.StartsWith(
                fullTemporaryRoot,
                StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(fullWebRoot).StartsWith(
                "realestate-12n-",
                StringComparison.Ordinal);

        if (!isExpectedTemporaryRoot)
        {
            throw new InvalidOperationException(
                "The temporary web root is outside the expected location.");
        }

        if (Directory.Exists(fullWebRoot))
        {
            Directory.Delete(
                fullWebRoot,
                recursive: true);
        }
    }
}
