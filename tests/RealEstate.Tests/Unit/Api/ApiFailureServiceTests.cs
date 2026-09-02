using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using RealEstate.Api.Errors;
using RealEstate.Application.Common;

namespace RealEstate.Tests.Unit.Api;

public sealed class ApiFailureServiceTests
{
    [Fact]
    public async Task EmptyMvcBadRequest_UsesCanonicalValidationProblemDetails()
    {
        ApiFailureService service = CreateService();
        DefaultHttpContext httpContext = CreateHttpContext();
        ActionContext actionContext = CreateActionContext(httpContext);
        var factory = new ApiClientErrorFactory(service);

        IActionResult result = factory.GetClientError(
            actionContext,
            new BadRequestResult());

        await result.ExecuteResultAsync(actionContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        httpContext.Response.ContentType.Should().Be(ApiFailureService.ContentType);

        JsonElement body = ReadResponseBody(httpContext);
        body.GetProperty("code").GetString()
            .Should().Be(ErrorCodes.ValidationFailed);
        body.GetProperty("type").GetString()
            .Should().Be("urn:realestate:error:validation.failed");
        body.GetProperty("errors").GetProperty("request")[0].GetString()
            .Should().Be("The request is invalid.");
    }

    [Fact]
    public void ValidationKeys_AreNormalizedForNestedAndCollectionProperties()
    {
        ApiFailureService service = CreateService();
        DefaultHttpContext httpContext = CreateHttpContext();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(
            "$.Translations[0].LanguageCode",
            "The language code is invalid.");
        modelState.AddModelError(
            "request.Items[1].DisplayName",
            "The display name is invalid.");

        ApiValidationProblemDetailsResponse problem =
            service.CreateValidation(httpContext, modelState);

        problem.Errors.Keys.Should().BeEquivalentTo(
            "translations[0].languageCode",
            "items[1].displayName");
    }

    [Fact]
    public async Task Writer_RefusesToReplaceExistingResponse()
    {
        ApiFailureService service = CreateService();
        DefaultHttpContext httpContext = CreateHttpContext();
        httpContext.Response.StatusCode = StatusCodes.Status418ImATeapot;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsync("{\"existing\":true}");
        long originalLength = httpContext.Response.Body.Length;

        bool written = await service.TryWriteAsync(
            httpContext,
            service.Create(httpContext, ApiFailureDescriptor.ResourceNotFound));

        written.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status418ImATeapot);
        httpContext.Response.ContentType.Should().Be("application/json");
        httpContext.Response.Body.Length.Should().Be(originalLength);
    }

    [Fact]
    public async Task Writer_RefusesToReplaceStartedResponse()
    {
        var responseFeature = new StartedResponseFeature
        {
            StatusCode = StatusCodes.Status202Accepted
        };
        DefaultHttpContext httpContext = CreateHttpContext();
        httpContext.Features.Set<IHttpResponseFeature>(responseFeature);
        ApiFailureService service = CreateService();

        bool written = await service.TryWriteAsync(
            httpContext,
            service.Create(httpContext, ApiFailureDescriptor.ResourceNotFound));

        written.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        httpContext.Response.ContentType.Should().BeNull();
        httpContext.Response.Body.Length.Should().Be(0);
    }

    [Theory]
    [InlineData(
        StatusCodes.Status503ServiceUnavailable,
        ErrorCodes.DependencyGeocodingUnavailable,
        "Geocoding unavailable",
        "Location search and confirmation are temporarily unavailable.")]
    [InlineData(
        StatusCodes.Status429TooManyRequests,
        ErrorCodes.RateLimitGeocodingExceeded,
        "Too many geocoding requests",
        "Too many location requests were made. Try again later.")]
    public async Task GeocodingFailures_UseCanonicalSanitizedProblemDetails(
        int expectedStatus,
        string expectedCode,
        string expectedTitle,
        string expectedDetail)
    {
        ApiFailureService service = CreateService();
        DefaultHttpContext httpContext = CreateHttpContext();
        ApiFailureDescriptor descriptor = ApiFailureDescriptor.ForCode(
            expectedCode);

        bool written = await service.TryWriteAsync(
            httpContext,
            service.Create(httpContext, descriptor));

        written.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(expectedStatus);
        httpContext.Response.ContentType.Should().Be(ApiFailureService.ContentType);

        JsonElement body = ReadResponseBody(httpContext);
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
            .Should().Be($"urn:realestate:error:{expectedCode}");
        body.GetProperty("title").GetString().Should().Be(expectedTitle);
        body.GetProperty("status").GetInt32().Should().Be(expectedStatus);
        body.GetProperty("detail").GetString().Should().Be(expectedDetail);
        body.GetProperty("instance").GetString().Should().Be("/api/resource");
        body.GetProperty("code").GetString().Should().Be(expectedCode);
        body.GetProperty("traceId").GetString().Should().Be("server-trace-id");

        string json = body.GetRawText();
        foreach (string forbidden in new[]
        {
            "geoapify",
            "api key",
            "provider reference",
            "confirmation token",
            "address query"
        })
        {
            json.Should().NotContainEquivalentOf(forbidden);
        }
    }

    private static ApiFailureService CreateService()
    {
        return new ApiFailureService(Options.Create(new JsonOptions()));
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "server-trace-id"
        };
        httpContext.Request.Path = "/api/resource";
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private static ActionContext CreateActionContext(HttpContext httpContext)
    {
        return new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
    }

    private static JsonElement ReadResponseBody(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using JsonDocument document = JsonDocument.Parse(
            httpContext.Response.Body);
        return document.RootElement.Clone();
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; }

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = new MemoryStream();

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}
