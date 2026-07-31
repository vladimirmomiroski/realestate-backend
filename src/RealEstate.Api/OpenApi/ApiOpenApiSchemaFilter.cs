using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using RealEstate.Api.Errors;
using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Users.Dtos;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RealEstate.Api.OpenApi;

public sealed class ApiOpenApiSchemaFilter : ISchemaFilter
{
    private const string RelativeMediaPathDescription =
        "API-relative media path, for example /uploads/... .";

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (schema is not OpenApiSchema mutableSchema)
        {
            return;
        }

        if (context.Type == typeof(ApiProblemDetailsResponse))
        {
            ApplyCanonicalProblemSchema(mutableSchema, includeErrors: false);
        }
        else if (context.Type == typeof(ApiValidationProblemDetailsResponse))
        {
            ApplyCanonicalProblemSchema(mutableSchema, includeErrors: true);
        }

        if (context.Type.IsGenericType &&
            context.Type.GetGenericTypeDefinition() == typeof(PagedResponse<>) &&
            context.Type.GenericTypeArguments[0] == typeof(ListingResponse))
        {
            ApplyPaginationSchema(mutableSchema);
        }

        ApplyRelativeMediaDescriptions(mutableSchema, context.Type);
    }

    private static void ApplyCanonicalProblemSchema(
        OpenApiSchema schema,
        bool includeErrors)
    {
        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();

        string[] canonicalMembers =
        [
            "type",
            "title",
            "status",
            "detail",
            "instance",
            "code",
            "traceId"
        ];

        foreach (string member in canonicalMembers)
        {
            if (schema.Properties.ContainsKey(member))
            {
                continue;
            }

            schema.Properties[member] = new OpenApiSchema
            {
                Type = member == "status"
                    ? JsonSchemaType.Integer
                    : JsonSchemaType.String
            };
        }

        if (includeErrors && !schema.Properties.ContainsKey("errors"))
        {
            schema.Properties["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalProperties = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String
                    }
                }
            };
        }

        if (schema.Properties.TryGetValue("code", out IOpenApiSchema? codeSchemaValue) &&
            codeSchemaValue is OpenApiSchema codeSchema)
        {
            codeSchema.Description = "Stable machine-readable API error code.";
            codeSchema.Enum = ErrorCodes.All
                .Order(StringComparer.Ordinal)
                .Select(value => (JsonNode)JsonValue.Create(value)!)
                .ToList();
        }

        if (schema.Properties.TryGetValue("traceId", out IOpenApiSchema? traceIdSchemaValue) &&
            traceIdSchemaValue is OpenApiSchema traceIdSchema)
        {
            traceIdSchema.Description =
                "Request correlation identifier; matches X-Request-ID.";
        }

        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        schema.Required.Add("code");
        schema.Required.Add("traceId");

        if (includeErrors)
        {
            schema.Required.Add("errors");
        }
    }

    private static void ApplyPaginationSchema(OpenApiSchema schema)
    {
        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();

        SetDescription(schema, "items", "Items for the requested page.");
        SetDescription(schema, "page", "Normalized one-based page number.");
        SetDescription(schema, "pageSize", "Normalized page size, capped at 100.");
        SetDescription(schema, "totalCount", "Total matching item count.");
        SetDescription(schema, "totalPages", "Total page count; zero when there are no matches.");
        SetDescription(schema, "hasNextPage", "True when page is less than totalPages.");
        SetDescription(schema, "hasPreviousPage", "True when page is greater than 1.");

        if (schema.Properties.TryGetValue("items", out IOpenApiSchema? itemsSchemaValue) &&
            itemsSchemaValue is OpenApiSchema itemsSchema &&
            itemsSchema.Type.HasValue)
        {
            itemsSchema.Type &= ~JsonSchemaType.Null;
        }

        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        schema.Required.Add("items");
    }

    private static void ApplyRelativeMediaDescriptions(
        OpenApiSchema schema,
        Type type)
    {
        if (type == typeof(UserProfileResponse))
        {
            SetDescription(schema, "avatarUrl", RelativeMediaPathDescription);
        }
        else if (type == typeof(AgencyResponse) || type == typeof(MyAgencyResponse))
        {
            SetDescription(schema, "logoUrl", RelativeMediaPathDescription);
        }
        else if (type == typeof(ListingResponse))
        {
            SetDescription(schema, "primaryImageUrl", RelativeMediaPathDescription);
        }
        else if (type == typeof(ListingImageResponse))
        {
            SetDescription(schema, "url", RelativeMediaPathDescription);
        }
    }

    private static void SetDescription(
        OpenApiSchema schema,
        string propertyName,
        string description)
    {
        if (schema.Properties is not null &&
            schema.Properties.TryGetValue(propertyName, out IOpenApiSchema? propertyValue) &&
            propertyValue is OpenApiSchema property)
        {
            property.Description = description;
        }
    }
}
