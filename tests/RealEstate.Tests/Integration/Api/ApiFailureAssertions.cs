using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace RealEstate.Tests.Integration.Api;

internal static class ApiFailureAssertions
{
    private const string RequestIdentifierHeader = "X-Request-ID";

    public static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        string instance,
        string? validationKey = null,
        bool bearerChallenge = false)
    {
        string responseText = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            status,
            "the response body was {0}",
            responseText);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/problem+json");

        using JsonDocument document = JsonDocument.Parse(responseText);

        JsonElement body = document.RootElement;
        string traceId = body.GetProperty("traceId").GetString()!;

        body.GetProperty("type").GetString().Should()
            .Be($"urn:realestate:error:{code}");
        body.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("status").GetInt32().Should().Be((int)status);
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("instance").GetString().Should().Be(instance);
        body.GetProperty("code").GetString().Should().Be(code);
        traceId.Should().NotBeNullOrWhiteSpace();
        response.Headers.GetValues(RequestIdentifierHeader).Should()
            .ContainSingle().Which.Should().Be(traceId);

        string[] expectedProperties = validationKey is null
            ? ["type", "title", "status", "detail", "instance", "code", "traceId"]
            : ["type", "title", "status", "detail", "instance", "code", "traceId", "errors"];

        body.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(expectedProperties);

        if (validationKey is not null)
        {
            JsonElement errors = body.GetProperty("errors");
            errors.EnumerateObject().Should().ContainSingle();
            errors.TryGetProperty(validationKey, out JsonElement messages)
                .Should().BeTrue();
            messages.GetArrayLength().Should().Be(1);
            messages[0].GetString().Should().NotBeNullOrWhiteSpace();
        }

        if (bearerChallenge)
        {
            response.Headers.WwwAuthenticate.Should().ContainSingle();
            response.Headers.WwwAuthenticate.Single().Scheme.Should().Be("Bearer");
            response.Headers.WwwAuthenticate.Single().Parameter.Should().BeNull();
        }
        else
        {
            response.Headers.WwwAuthenticate.Should().BeEmpty();
        }

        return body.Clone();
    }
}
