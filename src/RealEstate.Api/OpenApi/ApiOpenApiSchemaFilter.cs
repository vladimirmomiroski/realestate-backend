using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using RealEstate.Api.Errors;
using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Common;
using RealEstate.Application.Listings.Commands.CreateListing;
using RealEstate.Application.Listings.Commands.UpdateListing;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Users.Dtos;
using RealEstate.Domain.Listings;
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

        if (context.Type == typeof(CreateListingTranslationRequest))
        {
            ApplyCreateListingTranslationSchema(mutableSchema);
        }

        if (context.Type == typeof(UpdateListingRequest))
        {
            ApplyUpdateListingSchema(mutableSchema);
        }

        if (context.Type == typeof(UpdateListingTranslationRequest))
        {
            ApplyUpdateListingTranslationSchema(mutableSchema);
        }

        if (context.Type == typeof(UpdateListingApartmentDetailsRequest))
        {
            WrapReferenceWithDescription(
                mutableSchema,
                "apartmentType",
                "Optional; omission resets the stored value to Unknown.");
        }

        if (context.Type == typeof(UpdateListingHouseDetailsRequest))
        {
            WrapReferenceWithDescription(
                mutableSchema,
                "houseType",
                "Optional; omission resets the stored value to Unknown.");
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

    private static void ApplyCreateListingTranslationSchema(
        OpenApiSchema schema)
    {
        SetStringRule(
            schema,
            "languageCode",
            ListingTranslationRules.LanguageCodeMaxLength,
            "Boundary-trimmed and lowercased before validation. " +
            $"Canonical value must match {ListingTranslationRules.LanguageCodePattern} and contain at most {ListingTranslationRules.LanguageCodeMaxLength} characters.",
            ListingTranslationRules.LanguageCodePattern);
        SetStringRule(
            schema,
            "title",
            ListingTranslationRules.TitleMaxLength,
            $"Required, boundary-trimmed, nonblank; maximum {ListingTranslationRules.TitleMaxLength} characters after normalization.");
        SetStringRule(
            schema,
            "description",
            ListingTranslationRules.DescriptionMaxLength,
            $"Optional; boundary whitespace normalizes to null; maximum {ListingTranslationRules.DescriptionMaxLength} characters after normalization.");
        SetStringRule(
            schema,
            "addressLine",
            ListingTranslationRules.AddressLineMaxLength,
            $"Optional; boundary whitespace normalizes to null; maximum {ListingTranslationRules.AddressLineMaxLength} characters after normalization.");

        foreach (string propertyName in new[]
        {
            "city",
            "municipality",
            "neighborhood"
        })
        {
            SetStringRule(
                schema,
                propertyName,
                ListingTranslationRules.LocationMaxLength,
                $"Optional; boundary whitespace normalizes to null; maximum {ListingTranslationRules.LocationMaxLength} characters after normalization.");
        }
    }

    private static void ApplyUpdateListingSchema(OpenApiSchema schema)
    {
        schema.Description =
            "Full replacement of the current editable Draft listing content. " +
            "For optional nullable members, omission and explicit null both clear the stored value.";

        foreach (string propertyName in new[]
        {
            "rooms",
            "bathrooms",
            "balconyCount",
            "parkingSpaces",
            "hasBasement",
            "isExchangePossible",
            "yearRenovated",
            "yearBuilt",
            "latitude",
            "longitude"
        })
        {
            SetDescription(
                schema,
                propertyName,
                "Optional and nullable; omission or explicit null clears the stored value.");
        }

        foreach (string propertyName in new[]
        {
            "heatingType",
            "furnishingStatus",
            "condition",
            "orientation"
        })
        {
            WrapReferenceWithDescription(
                schema,
                propertyName,
                "Optional; omission resets the stored value to Unknown.");
        }

        WrapNullableReference(
            schema,
            "apartmentDetails",
            "Required when propertyType is Apartment and forbidden when propertyType is House. Omission or null clears this subtype payload during a valid House replacement.");
        WrapNullableReference(
            schema,
            "houseDetails",
            "Required when propertyType is House and forbidden when propertyType is Apartment. Omission or null clears this subtype payload during a valid Apartment replacement.");
        SetDescription(
            schema,
            "translations",
            "Complete authoritative translation set. Omitted stored languages are deleted; retained canonical languages preserve their server-owned translation IDs.");
    }

    private static void ApplyUpdateListingTranslationSchema(
        OpenApiSchema schema)
    {
        schema.Description =
            "One member of the complete authoritative replacement translation set. " +
            "The client does not submit translation IDs.";

        ApplyCreateListingTranslationSchema(schema);

        foreach (string propertyName in new[]
        {
            "description",
            "addressLine",
            "city",
            "municipality",
            "neighborhood"
        })
        {
            SetDescription(
                schema,
                propertyName,
                "Optional and nullable; omission, explicit null, or boundary-whitespace-only input clears the stored value.");
        }
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

    private static void WrapNullableReference(
        OpenApiSchema schema,
        string propertyName,
        string description)
    {
        if (schema.Properties is null ||
            !schema.Properties.TryGetValue(
                propertyName,
                out IOpenApiSchema? property))
        {
            return;
        }

        schema.Properties[propertyName] = new OpenApiSchema
        {
            Description = description,
            OneOf =
            [
                property,
                new OpenApiSchema
                {
                    Type = JsonSchemaType.Object | JsonSchemaType.Null,
                    Enum = [null!]
                }
            ]
        };
    }

    private static void WrapReferenceWithDescription(
        OpenApiSchema schema,
        string propertyName,
        string description)
    {
        if (schema.Properties is null ||
            !schema.Properties.TryGetValue(
                propertyName,
                out IOpenApiSchema? property))
        {
            return;
        }

        schema.Properties[propertyName] = new OpenApiSchema
        {
            Description = description,
            AllOf = [property]
        };
    }

    private static void SetStringRule(
        OpenApiSchema schema,
        string propertyName,
        int normalizedMaxLength,
        string description,
        string? canonicalPattern = null)
    {
        if (schema.Properties is null ||
            !schema.Properties.TryGetValue(
                propertyName,
                out IOpenApiSchema? propertyValue) ||
            propertyValue is not OpenApiSchema property)
        {
            return;
        }

        property.MaxLength = normalizedMaxLength;
        property.Description = description;
        property.Pattern = canonicalPattern;
    }
}
