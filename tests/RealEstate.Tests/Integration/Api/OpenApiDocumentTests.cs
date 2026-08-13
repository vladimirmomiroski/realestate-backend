using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using RealEstate.Application.Common;
using RealEstate.Domain.Listings;
using Swashbuckle.AspNetCore.Swagger;

namespace RealEstate.Tests.Integration.Api;

[Collection(OpenApiDocumentTestCollection.Name)]
public sealed class OpenApiDocumentTests
{
    private const string ProblemContentType =
        "application/problem+json";

    private readonly CustomWebApplicationFactory _factory;

    public OpenApiDocumentTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void OpenApiDocument_SecurityAndOperationIdentity_AreAccurate()
    {
        using JsonDocument document = GetDocument();
        JsonElement root = document.RootElement;

        root.TryGetProperty("security", out _).Should().BeFalse();

        JsonElement securitySchemes = root
            .GetProperty("components")
            .GetProperty("securitySchemes");
        securitySchemes.EnumerateObject().Should().HaveCount(1);
        securitySchemes.TryGetProperty("Bearer", out _).Should().BeTrue();

        List<string> operationIds = GetOperations(root)
            .Select(operation => operation.Operation
                .GetProperty("operationId")
                .GetString())
            .Where(operationId => operationId is not null)
            .Cast<string>()
            .ToList();

        operationIds.Should().OnlyContain(
            operationId => !string.IsNullOrWhiteSpace(operationId));
        operationIds.Should().OnlyHaveUniqueItems();
        operationIds.Should().HaveCount(GetOperations(root).Count);

        AssertAnonymous(root, "/api/auth/login", "post");
        AssertAnonymous(root, "/api/listings", "get");
        AssertAnonymous(root, "/api/agencies/{id}", "get");
        AssertAnonymous(root, "/api/agencies/{id}/listings", "get");
        AssertAnonymous(root, "/api/health", "get");
        AssertAnonymous(root, "/api/health/readiness", "get");
        AssertAnonymous(root, "/api/health/database", "get");

        AssertBearerRequired(root, "/api/users/me", "get");
        AssertBearerRequired(root, "/api/listings/my", "get");
        AssertBearerRequired(
            root,
            "/api/listings/{id}/management",
            "get");
        AssertBearerRequired(
            root,
            "/api/agencies/{id}/invitations",
            "get");
        AssertBearerRequired(
            root,
            "/api/admin/agencies/{agencyId}/approve",
            "put");
    }

    [Fact]
    public void OpenApiDocument_CanonicalResponsesAndPagination_AreAccurate()
    {
        using JsonDocument document = GetDocument();
        JsonElement root = document.RootElement;

        AssertProblemResponse(
            root,
            "/api/listings",
            "get",
            "400",
            "ApiValidationProblemDetailsResponse");
        AssertProblemResponse(
            root,
            "/api/users/me",
            "get",
            "401",
            "ApiProblemDetailsResponse");
        AssertProblemResponse(
            root,
            "/api/users/me",
            "get",
            "403",
            "ApiProblemDetailsResponse");
        AssertProblemResponse(
            root,
            "/api/listings/{id}",
            "get",
            "404",
            "ApiProblemDetailsResponse");
        AssertProblemResponse(
            root,
            "/api/listings/{id}/management",
            "get",
            "401",
            "ApiProblemDetailsResponse");
        AssertProblemResponse(
            root,
            "/api/listings/{id}/management",
            "get",
            "403",
            "ApiProblemDetailsResponse");
        AssertProblemResponse(
            root,
            "/api/listings/{id}/management",
            "get",
            "404",
            "ApiProblemDetailsResponse");
        AssertProblemResponse(
            root,
            "/api/listings/{id}/publish",
            "put",
            "409",
            "ApiProblemDetailsResponse");
        AssertProblemResponse(
            root,
            "/api/auth/login",
            "post",
            "500",
            "ApiProblemDetailsResponse");

        foreach ((_, _, JsonElement operation) in GetOperations(root))
        {
            foreach (JsonProperty response in operation
                .GetProperty("responses")
                .EnumerateObject())
            {
                response.Value
                    .GetProperty("headers")
                    .TryGetProperty("X-Request-ID", out _)
                    .Should()
                    .BeTrue();

                if (response.NameEquals("401"))
                {
                    response.Value
                        .GetProperty("headers")
                        .TryGetProperty("WWW-Authenticate", out _)
                        .Should()
                        .BeTrue();
                }
            }
        }

        JsonElement schemas = root
            .GetProperty("components")
            .GetProperty("schemas");
        JsonElement problem = schemas
            .GetProperty("ApiProblemDetailsResponse");
        JsonElement validation = schemas
            .GetProperty("ApiValidationProblemDetailsResponse");

        problem.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .Should()
            .Contain(["code", "traceId"]);
        validation.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .Should()
            .Contain(["code", "traceId", "errors"]);

        string[] documentedCodes = problem
            .GetProperty("properties")
            .GetProperty("code")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        documentedCodes.Should().Equal(
            ErrorCodes.All.Order(StringComparer.Ordinal));

        string[] paginationMembers =
        [
            "items",
            "page",
            "pageSize",
            "totalCount",
            "totalPages",
            "hasNextPage",
            "hasPreviousPage"
        ];
        (string Schema, string ItemSchema)[] paginationSchemas =
        [
            ("PublicListingResponsePagedResponse", "PublicListingResponse"),
            ("ListingResponsePagedResponse", "ListingResponse")
        ];

        foreach ((string schemaName, string itemSchemaName) in paginationSchemas)
        {
            JsonElement paginationSchema = schemas.GetProperty(schemaName);
            JsonElement paginationProperties =
                paginationSchema.GetProperty("properties");
            paginationProperties
                .EnumerateObject()
                .Select(property => property.Name)
                .Should()
                .BeEquivalentTo(paginationMembers);
            paginationSchema.GetProperty("required")
                .EnumerateArray()
                .Select(value => value.GetString())
                .Should()
                .Contain("items");

            JsonElement items = paginationProperties.GetProperty("items");
            items.GetProperty("type").GetString().Should().Be("array");
            items.GetProperty("items")
                .GetProperty("$ref")
                .GetString()
                .Should()
                .Be($"#/components/schemas/{itemSchemaName}");
            IsNullable(items).Should().BeFalse(
                "paged items must not be represented as nullable");

            foreach (string integerMember in new[]
            {
                "page",
                "pageSize",
                "totalCount",
                "totalPages"
            })
            {
                paginationProperties.GetProperty(integerMember)
                    .GetProperty("type")
                    .GetString()
                    .Should()
                    .Be("integer");
            }

            foreach (string booleanMember in new[]
            {
                "hasNextPage",
                "hasPreviousPage"
            })
            {
                paginationProperties.GetProperty(booleanMember)
                    .GetProperty("type")
                    .GetString()
                    .Should()
                    .Be("boolean");
            }
        }

        (string Path, string Method, string ResponseSchema)[] paginatedOperations =
        [
            (
                "/api/listings",
                "get",
                "PublicListingResponsePagedResponse"),
            (
                "/api/agencies/{id}/listings",
                "get",
                "PublicListingResponsePagedResponse"),
            (
                "/api/listings/my",
                "get",
                "ListingResponsePagedResponse"),
            (
                "/api/agencies/{id}/dashboard/listings",
                "get",
                "ListingResponsePagedResponse")
        ];

        foreach ((string path, string method, string responseSchema) in
                 paginatedOperations)
        {
            AssertResponseSchemaReference(
                root,
                path,
                method,
                "200",
                responseSchema);

            JsonElement operation = GetOperation(root, path, method);

            JsonElement page = GetParameter(operation, "page");
            page.GetProperty("schema")
                .GetProperty("default")
                .GetInt32()
                .Should()
                .Be(1);
            page.GetProperty("description")
                .GetString()
                .Should()
                .ContainAll("below 1", "normalize to 1");

            JsonElement pageSize = GetParameter(operation, "pageSize");
            pageSize.GetProperty("schema")
                .GetProperty("default")
                .GetInt32()
                .Should()
                .Be(20);
            pageSize.GetProperty("description")
                .GetString()
                .Should()
                .ContainAll(
                    "below 1",
                    "normalize to 20",
                    "above 100",
                    "capped at 100");
        }

        foreach (string enumName in new[]
        {
            "ListingType",
            "PropertyType",
            "AgencyInvitationStatus"
        })
        {
            schemas.GetProperty(enumName)
                .GetProperty("type")
                .GetString()
                .Should()
                .Be("string");
        }
    }

    [Fact]
    public void OpenApiDocument_PublishListing_ReadinessConflictIsDocumented()
    {
        using JsonDocument document = GetDocument();
        JsonElement root = document.RootElement;
        JsonElement operation = GetOperation(
            root,
            "/api/listings/{id}/publish",
            "put");

        AssertBearerRequired(root, "/api/listings/{id}/publish", "put");

        JsonElement idParameter = GetParameter(operation, "id");
        idParameter.GetProperty("in").GetString().Should().Be("path");
        idParameter.GetProperty("required").GetBoolean().Should().BeTrue();

        operation.GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString()
            .Should()
            .Be("#/components/schemas/PublicListingResponse");

        foreach (string status in new[] { "400", "401", "403", "404", "409" })
        {
            AssertProblemResponse(
                root,
                "/api/listings/{id}/publish",
                "put",
                status,
                status == "400"
                    ? "ApiValidationProblemDetailsResponse"
                    : "ApiProblemDetailsResponse");
        }

        operation.GetProperty("responses")
            .GetProperty("409")
            .GetProperty("description")
            .GetString()
            .Should()
            .ContainAll(
                ErrorCodes.ConflictResourceState,
                ErrorCodes.ConflictListingNotReady,
                "The listing is not ready for publication.");

        root.GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("ApiProblemDetailsResponse")
            .GetProperty("properties")
            .GetProperty("code")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .Contain(ErrorCodes.ConflictListingNotReady);
    }

    [Fact]
    public void OpenApiDocument_Chapter13GListingResponses_AreSeparatedAndTruthful()
    {
        using JsonDocument document = GetDocument();
        JsonElement root = document.RootElement;
        JsonElement schemas = root
            .GetProperty("components")
            .GetProperty("schemas");

        (string Path, string Method, string Status, string Schema)[] directResponses =
        [
            ("/api/listings/{id}", "get", "200", "PublicListingResponse"),
            ("/api/listings/{id}/publish", "put", "200", "PublicListingResponse"),
            ("/api/listings", "post", "201", "ListingResponse"),
            ("/api/listings/{id}/unpublish", "put", "200", "ListingResponse"),
            ("/api/listings/{id}/archive", "put", "200", "ListingResponse"),
            (
                "/api/listings/{id}/management",
                "get",
                "200",
                "ListingAuthoringResponse"),
            ("/api/listings/{id}", "put", "200", "ListingAuthoringResponse")
        ];

        foreach ((string path, string method, string status, string schema) in
                 directResponses)
        {
            AssertResponseSchemaReference(root, path, method, status, schema);
        }

        AssertResponseSchemaReference(
            root,
            "/api/listings",
            "get",
            "200",
            "PublicListingResponsePagedResponse");
        AssertResponseSchemaReference(
            root,
            "/api/agencies/{id}/listings",
            "get",
            "200",
            "PublicListingResponsePagedResponse");
        AssertResponseSchemaReference(
            root,
            "/api/listings/my",
            "get",
            "200",
            "ListingResponsePagedResponse");
        AssertResponseSchemaReference(
            root,
            "/api/agencies/{id}/dashboard/listings",
            "get",
            "200",
            "ListingResponsePagedResponse");

        JsonElement comparableSchema = GetSuccessSchema(
            root,
            "/api/listings/{id}/comparables",
            "get");
        comparableSchema.GetProperty("type").GetString().Should().Be("array");
        comparableSchema.GetProperty("items")
            .GetProperty("$ref")
            .GetString()
            .Should()
            .Be("#/components/schemas/PublicListingResponse");
        AssertProblemResponse(
            root,
            "/api/listings/{id}/comparables",
            "get",
            "400",
            "ApiValidationProblemDetailsResponse");
        AssertProblemResponse(
            root,
            "/api/listings/{id}/comparables",
            "get",
            "404",
            "ApiProblemDetailsResponse");

        JsonElement publicSchema = schemas.GetProperty("PublicListingResponse");
        foreach (string propertyName in new[]
        {
            "languageCode",
            "title",
            "city",
            "description"
        })
        {
            AssertRequiredNonNullableString(publicSchema, propertyName);
        }

        JsonElement privateSchema = schemas.GetProperty("ListingResponse");
        foreach (string propertyName in new[]
        {
            "languageCode",
            "title",
            "city",
            "description"
        })
        {
            AssertNullableString(privateSchema, propertyName);
        }

        JsonElement authoringTranslationSchema = schemas
            .GetProperty("ListingAuthoringTranslationResponse");
        AssertRequiredNonNullableString(
            authoringTranslationSchema,
            "languageCode");
        AssertRequiredNonNullableString(authoringTranslationSchema, "title");
        AssertNullableString(authoringTranslationSchema, "city");
        AssertNullableString(authoringTranslationSchema, "description");

        schemas.EnumerateObject()
            .Select(schema => schema.Name)
            .Should()
            .NotContain(schemaName =>
                schemaName.Contains("ServiceResult", StringComparison.Ordinal) ||
                schemaName.Contains(
                    "PublicListingIntegrityException",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void OpenApiDocument_Chapter13H5PrivateLocationReadContract_IsNullableAndPrivate()
    {
        using JsonDocument document = GetDocument();
        JsonElement schemas = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas");

        foreach (string schemaName in new[]
        {
            "ListingResponse",
            "ListingAuthoringResponse"
        })
        {
            JsonElement schema = schemas.GetProperty(schemaName);
            JsonElement properties = schema.GetProperty("properties");

            foreach (string coordinate in new[] { "latitude", "longitude" })
            {
                JsonElement property = properties.GetProperty(coordinate);
                property.GetProperty("type").GetString().Should().Be("number");
                IsNullable(property).Should().BeTrue();
            }

            JsonElement precision = properties.GetProperty("locationPrecision");
            JsonElement[] precisionAlternatives = precision
                .GetProperty("oneOf")
                .EnumerateArray()
                .ToArray();
            precisionAlternatives.Should().HaveCount(2);
            precisionAlternatives[0]
                .GetProperty("$ref")
                .GetString()
                .Should().Be("#/components/schemas/LocationPrecision");
            IsNullable(precisionAlternatives[1]).Should().BeTrue();
            precisionAlternatives[1].GetProperty("enum")[0].ValueKind
                .Should().Be(JsonValueKind.Null);

            AssertNullableString(schema, "geocodedDisplayName");

            JsonElement confirmedAt =
                properties.GetProperty("locationConfirmedAtUtc");
            confirmedAt.GetProperty("type").GetString().Should().Be("string");
            confirmedAt.GetProperty("format").GetString().Should().Be("date-time");
            IsNullable(confirmedAt).Should().BeTrue();

            properties.TryGetProperty("geocodingProviderKey", out _)
                .Should().BeFalse();
            properties.TryGetProperty("geocodingResultReference", out _)
                .Should().BeFalse();
            if (schema.TryGetProperty("required", out JsonElement required))
            {
                required.EnumerateArray()
                    .Select(value => value.GetString())
                    .Should().NotContain(
                    [
                        "latitude",
                        "longitude",
                        "locationPrecision",
                        "geocodedDisplayName",
                        "locationConfirmedAtUtc"
                    ]);
            }
        }

        schemas.GetProperty("LocationPrecision")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should().BeEquivalentTo(
                "ExactAddress",
                "Street",
                "Neighborhood",
                "Municipality",
                "City",
                "Approximate");
        schemas.GetRawText().Should()
            .NotContain("geocodingProviderKey")
            .And.NotContain("geocodingResultReference");

        JsonElement publicProperties = schemas
            .GetProperty("PublicListingResponse")
            .GetProperty("properties");
        publicProperties.TryGetProperty("latitude", out _).Should().BeTrue();
        publicProperties.TryGetProperty("longitude", out _).Should().BeTrue();
        foreach (string privateOnly in new[]
        {
            "locationPrecision",
            "geocodedDisplayName",
            "locationConfirmedAtUtc",
            "geocodingProviderKey",
            "geocodingResultReference"
        })
        {
            publicProperties.TryGetProperty(privateOnly, out _)
                .Should().BeFalse();
        }

        foreach (string requestName in new[]
        {
            "CreateListingRequest",
            "UpdateListingRequest"
        })
        {
            JsonElement requestProperties = schemas
                .GetProperty(requestName)
                .GetProperty("properties");
            foreach (string readOnlyLocationMember in new[]
            {
                "latitude",
                "longitude",
                "locationPrecision",
                "geocodedDisplayName",
                "locationConfirmedAtUtc",
                "geocodingProviderKey",
                "geocodingResultReference"
            })
            {
                requestProperties.TryGetProperty(readOnlyLocationMember, out _)
                    .Should().BeFalse();
            }
        }
    }

    [Fact]
    public void OpenApiDocument_ListingManagementContract_IsCompleteAndTruthful()
    {
        using JsonDocument document = GetDocument();
        JsonElement root = document.RootElement;
        JsonElement successSchema = GetOperation(
                root,
                "/api/listings/{id}/management",
                "get")
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        successSchema.GetProperty("$ref").GetString().Should().Be(
            "#/components/schemas/ListingAuthoringResponse");

        JsonElement schemas = root
            .GetProperty("components")
            .GetProperty("schemas");
        JsonElement authoringSchema = schemas
            .GetProperty("ListingAuthoringResponse");
        authoringSchema.GetProperty("required")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .Contain(["currency", "translations", "images"]);

        JsonElement translations = authoringSchema
            .GetProperty("properties")
            .GetProperty("translations");
        translations.GetProperty("type").GetString().Should().Be("array");
        translations.GetProperty("items")
            .GetProperty("$ref")
            .GetString()
            .Should()
            .Be("#/components/schemas/ListingAuthoringTranslationResponse");

        JsonElement translationSchema = schemas
            .GetProperty("ListingAuthoringTranslationResponse");
        translationSchema.GetProperty("required")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .Contain(["languageCode", "title"]);
        JsonElement translationProperties =
            translationSchema.GetProperty("properties");
        translationProperties.GetProperty("city")
            .GetProperty("nullable")
            .GetBoolean()
            .Should()
            .BeTrue();
        translationProperties.GetProperty("description")
            .GetProperty("nullable")
            .GetBoolean()
            .Should()
            .BeTrue();
    }

    [Fact]
    public void OpenApiDocument_UpdateListingContract_IsCompleteAndTruthful()
    {
        using JsonDocument document = GetDocument();
        JsonElement root = document.RootElement;
        JsonElement operation = GetOperation(
            root,
            "/api/listings/{id}",
            "put");

        AssertBearerRequired(root, "/api/listings/{id}", "put");

        JsonElement idParameter = GetParameter(operation, "id");
        idParameter.GetProperty("in").GetString().Should().Be("path");
        idParameter.GetProperty("required").GetBoolean().Should().BeTrue();

        JsonElement requestSchema = operation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        requestSchema.GetProperty("$ref").GetString().Should().Be(
            "#/components/schemas/UpdateListingRequest");

        JsonElement responseSchema = operation
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        responseSchema.GetProperty("$ref").GetString().Should().Be(
            "#/components/schemas/ListingAuthoringResponse");

        AssertProblemResponse(
            root,
            "/api/listings/{id}",
            "put",
            "400",
            "ApiValidationProblemDetailsResponse");

        foreach (string status in new[] { "401", "403", "404", "409" })
        {
            AssertProblemResponse(
                root,
                "/api/listings/{id}",
                "put",
                status,
                "ApiProblemDetailsResponse");
        }

        JsonElement schemas = root
            .GetProperty("components")
            .GetProperty("schemas");
        JsonElement updateSchema = schemas.GetProperty("UpdateListingRequest");
        updateSchema.GetProperty("required")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .BeEquivalentTo(
            [
                "listingType",
                "propertyType",
                "price",
                "currency",
                "areaSquareMeters",
                "translations"
            ]);

        string[] writableMembers =
        [
            "listingType",
            "propertyType",
            "price",
            "currency",
            "areaSquareMeters",
            "rooms",
            "bathrooms",
            "balconyCount",
            "parkingSpaces",
            "hasBasement",
            "isExchangePossible",
            "heatingType",
            "furnishingStatus",
            "condition",
            "yearRenovated",
            "orientation",
            "yearBuilt",
            "apartmentDetails",
            "houseDetails",
            "translations"
        ];
        JsonElement updateProperties = updateSchema.GetProperty("properties");
        foreach (string readOnlyLocationMember in new[]
        {
            "latitude",
            "longitude",
            "locationPrecision",
            "geocodedDisplayName",
            "locationConfirmedAtUtc",
            "geocodingProviderKey",
            "geocodingResultReference"
        })
        {
            updateProperties.TryGetProperty(readOnlyLocationMember, out _)
                .Should().BeFalse();
        }
        updateProperties.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(writableMembers);
        updateSchema.GetProperty("description").GetString()
            .Should().ContainAll("Full replacement", "omission", "clear");

        foreach (string nullableMember in new[]
        {
            "rooms",
            "bathrooms",
            "balconyCount",
            "parkingSpaces",
            "hasBasement",
            "isExchangePossible",
            "yearRenovated",
            "yearBuilt"
        })
        {
            JsonElement property = updateProperties.GetProperty(nullableMember);
            property.GetProperty("nullable").GetBoolean().Should().BeTrue();
            property.GetProperty("description").GetString()
                .Should().ContainAny("clear", "clears");
        }

        foreach (string optionalEnum in new[]
        {
            "heatingType",
            "furnishingStatus",
            "condition",
            "orientation"
        })
        {
            updateProperties.GetProperty(optionalEnum)
                .GetProperty("description")
                .GetString()
                .Should()
                .ContainAll("omission", "Unknown");
        }

        schemas.GetProperty("UpdateListingApartmentDetailsRequest")
            .GetProperty("properties")
            .GetProperty("apartmentType")
            .GetProperty("description")
            .GetString()
            .Should()
            .ContainAll("omission", "Unknown");
        schemas.GetProperty("UpdateListingHouseDetailsRequest")
            .GetProperty("properties")
            .GetProperty("houseType")
            .GetProperty("description")
            .GetString()
            .Should()
            .ContainAll("omission", "Unknown");

        AssertNullableReference(
            updateProperties.GetProperty("apartmentDetails"),
            "#/components/schemas/UpdateListingApartmentDetailsRequest",
            "Apartment",
            "House");
        AssertNullableReference(
            updateProperties.GetProperty("houseDetails"),
            "#/components/schemas/UpdateListingHouseDetailsRequest",
            "House",
            "Apartment");
        updateProperties.GetProperty("translations")
            .GetProperty("description")
            .GetString()
            .Should().ContainAll("authoritative", "deleted", "preserve");

        JsonElement translationSchema = schemas
            .GetProperty("UpdateListingTranslationRequest");
        translationSchema.GetProperty("required")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .BeEquivalentTo("languageCode", "title");
        translationSchema.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .Should()
            .BeEquivalentTo(
                "languageCode",
                "title",
                "description",
                "addressLine",
                "city",
                "municipality",
                "neighborhood");
        translationSchema.GetProperty("description").GetString()
            .Should().Contain("does not submit translation IDs");

        foreach (string serverOwned in new[]
        {
            "id",
            "status",
            "agencyId",
            "createdByUserId",
            "images",
            "createdAtUtc",
            "modifiedAtUtc"
        })
        {
            updateProperties.TryGetProperty(serverOwned, out _).Should().BeFalse();
        }
    }

    private static void AssertNullableReference(
        JsonElement property,
        string expectedReference,
        string requiredPropertyType,
        string forbiddenPropertyType)
    {
        property.TryGetProperty("allOf", out _).Should().BeFalse();
        property.GetProperty("description").GetString()
            .Should().ContainAll(
                requiredPropertyType,
                "Required",
                forbiddenPropertyType,
                "forbidden");

        JsonElement[] alternatives = property.GetProperty("oneOf")
            .EnumerateArray()
            .ToArray();
        alternatives.Should().HaveCount(2);
        alternatives[0].GetProperty("$ref").GetString()
            .Should().Be(expectedReference);

        JsonElement nullAlternative = alternatives[1];
        nullAlternative.GetProperty("type").GetString().Should().Be("object");
        nullAlternative.GetProperty("nullable").GetBoolean().Should().BeTrue();
        JsonElement[] allowedValues = nullAlternative.GetProperty("enum")
            .EnumerateArray()
            .ToArray();
        allowedValues.Should().ContainSingle();
        allowedValues[0].ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void OpenApiDocument_CreateTranslationRulesAndCurrentTaxonomy_AreAccurate()
    {
        using JsonDocument document = GetDocument();
        JsonElement schemas = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas");
        JsonElement createProperties = schemas
            .GetProperty("CreateListingRequest")
            .GetProperty("properties");
        foreach (string readOnlyLocationMember in new[]
        {
            "latitude",
            "longitude",
            "locationPrecision",
            "geocodedDisplayName",
            "locationConfirmedAtUtc",
            "geocodingProviderKey",
            "geocodingResultReference"
        })
        {
            createProperties.TryGetProperty(readOnlyLocationMember, out _)
                .Should().BeFalse();
        }

        JsonElement properties = schemas
            .GetProperty("CreateListingTranslationRequest")
            .GetProperty("properties");

        AssertNormalizedStringRule(
            properties,
            "languageCode",
            ListingTranslationRules.LanguageCodeMaxLength);
        properties.GetProperty("languageCode")
            .GetProperty("pattern")
            .GetString()
            .Should()
            .Be(ListingTranslationRules.LanguageCodePattern);
        properties.GetProperty("languageCode")
            .GetProperty("description")
            .GetString()
            .Should()
            .ContainAll("Boundary-trimmed", "lowercased", "Canonical");

        AssertNormalizedStringRule(
            properties,
            "title",
            ListingTranslationRules.TitleMaxLength);
        AssertNormalizedStringRule(
            properties,
            "description",
            ListingTranslationRules.DescriptionMaxLength);
        AssertNormalizedStringRule(
            properties,
            "addressLine",
            ListingTranslationRules.AddressLineMaxLength);

        foreach (string propertyName in new[]
        {
            "city",
            "municipality",
            "neighborhood"
        })
        {
            AssertNormalizedStringRule(
                properties,
                propertyName,
                ListingTranslationRules.LocationMaxLength);
        }

        schemas.GetProperty("ListingType")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .BeEquivalentTo("Sale", "Rent");
        schemas.GetProperty("PropertyType")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .BeEquivalentTo("Apartment", "House");
    }

    [Fact]
    public void OpenApiDocument_MultipartMediaAndHealth_AreAccurate()
    {
        using JsonDocument document = GetDocument();
        JsonElement root = document.RootElement;

        (string Path, string Method)[] uploadOperations =
        [
            ("/api/users/me/avatar", "put"),
            ("/api/agencies/{agencyId}/logo", "put"),
            ("/api/listings/{id}/images", "post")
        ];

        foreach ((string path, string method) in uploadOperations)
        {
            JsonElement requestBody = GetOperation(root, path, method)
                .GetProperty("requestBody");
            requestBody.GetProperty("required").GetBoolean().Should().BeTrue();

            JsonElement schema = requestBody
                .GetProperty("content")
                .GetProperty("multipart/form-data")
                .GetProperty("schema");
            schema.GetProperty("required")
                .EnumerateArray()
                .Select(value => value.GetString())
                .Should()
                .Contain("file");

            JsonElement file = schema
                .GetProperty("properties")
                .GetProperty("file");
            file.GetProperty("type").GetString().Should().Be("string");
            file.GetProperty("format").GetString().Should().Be("binary");
            file.GetProperty("maxLength").GetInt32().Should().Be(5_242_880);
            file.GetProperty("description")
                .GetString()
                .Should()
                .ContainAll(
                    ".jpg",
                    ".jpeg",
                    ".png",
                    ".webp",
                    "image/jpeg",
                    "image/png",
                    "image/webp");
        }

        JsonElement schemas = root
            .GetProperty("components")
            .GetProperty("schemas");
        (string Schema, string Property)[] mediaProperties =
        [
            ("UserProfileResponse", "avatarUrl"),
            ("AgencyResponse", "logoUrl"),
            ("MyAgencyResponse", "logoUrl"),
            ("ListingResponse", "primaryImageUrl"),
            ("PublicListingResponse", "primaryImageUrl"),
            ("ListingImageResponse", "url")
        ];

        foreach ((string schemaName, string propertyName) in mediaProperties)
        {
            schemas.GetProperty(schemaName)
                .GetProperty("properties")
                .GetProperty(propertyName)
                .GetProperty("description")
                .GetString()
                .Should()
                .Contain("API-relative media path");
        }

        schemas.GetProperty("AgencyResponse")
            .GetProperty("properties")
            .GetProperty("websiteUrl")
            .TryGetProperty("description", out JsonElement websiteDescription)
            .Should()
            .BeFalse();

        AssertHealthResponse(
            root,
            "/api/health",
            "200",
            "ok",
            "app",
            "RealEstate.Api");
        AssertHealthResponse(
            root,
            "/api/health/readiness",
            "200",
            "ok",
            "database",
            "PostgreSQL");
        AssertHealthResponse(
            root,
            "/api/health/readiness",
            "503",
            "unavailable",
            "database",
            "PostgreSQL");
        AssertHealthResponse(
            root,
            "/api/health/database",
            "200",
            "ok",
            "database",
            "PostgreSQL");
        AssertHealthResponse(
            root,
            "/api/health/database",
            "503",
            "unavailable",
            "database",
            "PostgreSQL");

        AssertAnonymous(root, "/api/health", "get");
        AssertAnonymous(root, "/api/health/readiness", "get");
        AssertAnonymous(root, "/api/health/database", "get");
        root.GetProperty("paths")
            .EnumerateObject()
            .Select(path => path.Name)
            .Should()
            .NotContain(path => path.StartsWith(
                "/uploads",
                StringComparison.Ordinal));
    }

    [Fact]
    public void DeveloperSamples_UseCurrentSafeDocumentedRoutes()
    {
        using JsonDocument document = GetDocument();
        JsonElement root = document.RootElement;
        IWebHostEnvironment environment = _factory.Services
            .GetRequiredService<IWebHostEnvironment>();
        string samplesPath = Path.Combine(
            environment.ContentRootPath,
            "RealEstate.Api.http");
        string samples = File.ReadAllText(samplesPath);

        (string Method, string SampleRoute, string DocumentPath)[] examples =
        [
            ("GET", "/api/health", "/api/health"),
            ("POST", "/api/auth/register", "/api/auth/register"),
            ("POST", "/api/auth/login", "/api/auth/login"),
            (
                "GET",
                "/api/listings?lang=en&page=1&pageSize=20",
                "/api/listings"),
            (
                "GET",
                "/api/listings/my?lang=en&page=1&pageSize=20",
                "/api/listings/my")
        ];

        foreach ((string method, string sampleRoute, string documentPath) in examples)
        {
            samples.Should().Contain(
                $"{method} {{{{RealEstate.Api_HostAddress}}}}{sampleRoute}");
            GetOperation(root, documentPath, method.ToLowerInvariant())
                .ValueKind
                .Should()
                .Be(JsonValueKind.Object);
        }

        samples.Should().Contain("paste-access-token-here");
        samples.Should().Contain("X-Request-ID:");
        samples.Should().NotContain("weatherforecast");
        samples.Should().NotContain("ConnectionStrings");
        samples.Should().NotContain("DefaultConnection");
        samples.Should().NotContain("Host=");
        samples.Should().NotContain("Username=");
        samples.Should().NotContain("Password=");
        Regex.IsMatch(
                samples,
                @"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+")
            .Should()
            .BeFalse();
    }

    private JsonDocument GetDocument()
    {
        ISwaggerProvider provider = _factory.Services
            .GetRequiredService<ISwaggerProvider>();
        OpenApiDocument openApi = provider.GetSwagger("v1");

        using var textWriter = new StringWriter();
        var writer = new OpenApiJsonWriter(textWriter);
        openApi.SerializeAsV3(writer);

        return JsonDocument.Parse(textWriter.ToString());
    }

    private static List<(string Path, string Method, JsonElement Operation)>
        GetOperations(JsonElement root)
    {
        string[] methods = ["get", "post", "put", "delete", "patch"];
        var operations =
            new List<(string Path, string Method, JsonElement Operation)>();

        foreach (JsonProperty path in root
            .GetProperty("paths")
            .EnumerateObject())
        {
            foreach (string method in methods)
            {
                if (path.Value.TryGetProperty(method, out JsonElement operation))
                {
                    operations.Add((path.Name, method, operation));
                }
            }
        }

        return operations;
    }

    private static JsonElement GetOperation(
        JsonElement root,
        string path,
        string method)
    {
        return root.GetProperty("paths")
            .GetProperty(path)
            .GetProperty(method);
    }

    private static JsonElement GetSuccessSchema(
        JsonElement root,
        string path,
        string method,
        string status = "200")
    {
        return GetOperation(root, path, method)
            .GetProperty("responses")
            .GetProperty(status)
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
    }

    private static void AssertResponseSchemaReference(
        JsonElement root,
        string path,
        string method,
        string status,
        string schemaName)
    {
        GetSuccessSchema(root, path, method, status)
            .GetProperty("$ref")
            .GetString()
            .Should()
            .Be($"#/components/schemas/{schemaName}");
    }

    private static void AssertRequiredNonNullableString(
        JsonElement schema,
        string propertyName)
    {
        schema.GetProperty("required")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .Contain(propertyName);

        JsonElement property = schema
            .GetProperty("properties")
            .GetProperty(propertyName);
        property.GetProperty("type").GetString().Should().Be("string");
        IsNullable(property).Should().BeFalse();
    }

    private static void AssertNullableString(
        JsonElement schema,
        string propertyName)
    {
        JsonElement property = schema
            .GetProperty("properties")
            .GetProperty(propertyName);
        property.GetProperty("type").GetString().Should().Be("string");
        IsNullable(property).Should().BeTrue();
    }

    private static bool IsNullable(JsonElement schema)
    {
        return schema.TryGetProperty("nullable", out JsonElement nullable) &&
               nullable.GetBoolean();
    }

    private static JsonElement GetParameter(
        JsonElement operation,
        string name)
    {
        return operation.GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter =>
                parameter.GetProperty("name").GetString() == name);
    }

    private static void AssertAnonymous(
        JsonElement root,
        string path,
        string method)
    {
        JsonElement operation = GetOperation(root, path, method);
        if (operation.TryGetProperty("security", out JsonElement security))
        {
            security.GetArrayLength().Should().Be(0);
        }
    }

    private static void AssertBearerRequired(
        JsonElement root,
        string path,
        string method)
    {
        JsonElement security = GetOperation(root, path, method)
            .GetProperty("security");
        security.GetArrayLength().Should().Be(1);
        JsonElement requirement = security[0];
        requirement.EnumerateObject().Should().ContainSingle();
        requirement.TryGetProperty("Bearer", out JsonElement scopes)
            .Should()
            .BeTrue();
        scopes.GetArrayLength().Should().Be(0);
    }

    private static void AssertProblemResponse(
        JsonElement root,
        string path,
        string method,
        string status,
        string schemaName)
    {
        JsonElement response = GetOperation(root, path, method)
            .GetProperty("responses")
            .GetProperty(status);
        JsonElement content = response.GetProperty("content");
        content.EnumerateObject().Should().ContainSingle();
        content.TryGetProperty(ProblemContentType, out JsonElement problemContent)
            .Should()
            .BeTrue();
        problemContent.GetProperty("schema")
            .GetProperty("$ref")
            .GetString()
            .Should()
            .Be($"#/components/schemas/{schemaName}");
    }

    private static void AssertHealthResponse(
        JsonElement root,
        string path,
        string status,
        string statusValue,
        string secondProperty,
        string secondValue)
    {
        JsonElement schema = GetOperation(root, path, "get")
            .GetProperty("responses")
            .GetProperty(status)
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        schema.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .Should()
            .BeEquivalentTo(["status", secondProperty]);
        schema.GetProperty("required")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .BeEquivalentTo(["status", secondProperty]);
        schema.GetProperty("properties")
            .GetProperty("status")
            .GetProperty("enum")[0]
            .GetString()
            .Should()
            .Be(statusValue);
        schema.GetProperty("properties")
            .GetProperty(secondProperty)
            .GetProperty("enum")[0]
            .GetString()
            .Should()
            .Be(secondValue);
    }

    private static void AssertNormalizedStringRule(
        JsonElement properties,
        string propertyName,
        int maximumLength)
    {
        JsonElement property = properties.GetProperty(propertyName);
        property.GetProperty("maxLength").GetInt32().Should().Be(maximumLength);
        property.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
