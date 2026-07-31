using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using RealEstate.Api.Errors;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RealEstate.Api.OpenApi;

public sealed class ApiOpenApiOperationFilter : IOperationFilter
{
    private const string BearerScheme = "Bearer";
    private const string ProblemContentType = "application/problem+json";
    private const string MultipartContentType = "multipart/form-data";
    private const string RequestIdHeader = "X-Request-ID";
    private const string AuthenticateHeader = "WWW-Authenticate";
    private const int MaximumImageSizeBytes = 5 * 1024 * 1024;

    private static readonly string[] CanonicalProblemStatuses =
        ["401", "403", "404", "409", "500"];

    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        operation.Responses ??= new OpenApiResponses();

        ApplyOperationId(operation, context);

        bool requiresAuthorization = ApplySecurity(operation, context);

        if (requiresAuthorization)
        {
            EnsureResponse(operation, "401", "Unauthorized");
            EnsureResponse(operation, "403", "Forbidden");
        }

        EnsureResponse(operation, "500", "Internal Server Error");
        ApplyCanonicalFailures(operation, context);
        ApplyPaginationDocumentation(operation);
        ApplyMultipartDocumentation(operation);
        ApplyHealthDocumentation(operation, context);
        ApplyResponseHeaders(operation);
    }

    private static void ApplyOperationId(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (!string.IsNullOrWhiteSpace(operation.OperationId) ||
            context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor controller)
        {
            return;
        }

        operation.OperationId = $"{controller.ControllerName}_{controller.ActionName}";
    }

    private static bool ApplySecurity(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        IList<object> metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        bool allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();
        bool requiresAuthorization = !allowsAnonymous &&
            metadata.OfType<IAuthorizeData>().Any();

        operation.Security = [];

        if (!requiresAuthorization)
        {
            return false;
        }

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                BearerScheme,
                context.Document)] = []
        });

        return true;
    }

    private static void ApplyCanonicalFailures(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        OpenApiResponses responses = GetResponses(operation);

        if (responses.TryGetValue("400", out _))
        {
            responses["400"] = CreateProblemResponse(
                "Bad Request",
                typeof(ApiValidationProblemDetailsResponse),
                context);
        }

        foreach (string status in CanonicalProblemStatuses)
        {
            if (!responses.TryGetValue(status, out _))
            {
                continue;
            }

            responses[status] = CreateProblemResponse(
                GetResponseDescription(status),
                typeof(ApiProblemDetailsResponse),
                context);
        }
    }

    private static OpenApiResponse CreateProblemResponse(
        string description,
        Type responseType,
        OperationFilterContext context)
    {
        var schema = context.SchemaGenerator.GenerateSchema(
            responseType,
            context.SchemaRepository);

        return new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [ProblemContentType] = new OpenApiMediaType
                {
                    Schema = schema
                }
            }
        };
    }

    private static void ApplyResponseHeaders(OpenApiOperation operation)
    {
        OpenApiResponses responses = GetResponses(operation);

        foreach ((string status, IOpenApiResponse responseValue) in responses)
        {
            if (responseValue is not OpenApiResponse response)
            {
                continue;
            }

            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[RequestIdHeader] = new OpenApiHeader
            {
                Description = "Correlates the response with API logs and problem details.",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String
                }
            };

            if (status == "401")
            {
                response.Headers[AuthenticateHeader] = new OpenApiHeader
                {
                    Description = "Bearer authentication challenge.",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String
                    }
                };
            }
        }
    }

    private static void ApplyPaginationDocumentation(OpenApiOperation operation)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        foreach (IOpenApiParameter parameter in operation.Parameters)
        {
            if (parameter.In != ParameterLocation.Query)
            {
                continue;
            }

            if (string.Equals(parameter.Name, "page", StringComparison.Ordinal))
            {
                parameter.Description =
                    "Page number. Values below 1 normalize to 1. Default: 1.";
            }
            else if (string.Equals(parameter.Name, "pageSize", StringComparison.Ordinal))
            {
                parameter.Description =
                    "Page size. Values below 1 normalize to 20; values above 100 are capped at 100. Default: 20.";
            }
        }
    }

    private static void ApplyMultipartDocumentation(OpenApiOperation operation)
    {
        if (operation.RequestBody is not OpenApiRequestBody requestBody ||
            requestBody.Content is null ||
            !requestBody.Content.TryGetValue(
                MultipartContentType,
                out OpenApiMediaType? mediaType) ||
            mediaType.Schema is not OpenApiSchema bodySchema ||
            bodySchema.Properties is null ||
            !bodySchema.Properties.TryGetValue("file", out IOpenApiSchema? fileSchemaValue) ||
            fileSchemaValue is not OpenApiSchema fileSchema)
        {
            return;
        }

        requestBody.Required = true;
        bodySchema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        bodySchema.Required.Add("file");
        fileSchema.Type = JsonSchemaType.String;
        fileSchema.Format = "binary";
        fileSchema.MaxLength = MaximumImageSizeBytes;
        fileSchema.Description =
            "Required image file. Maximum 5,242,880 bytes (5 MiB). " +
            "Allowed extensions: .jpg, .jpeg, .png, .webp. " +
            "Allowed media types: image/jpeg, image/png, image/webp.";
    }

    private static void ApplyHealthDocumentation(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        OpenApiResponses responses = GetResponses(operation);
        string path = "/" + (context.ApiDescription.RelativePath ?? string.Empty)
            .Split('?', 2)[0]
            .Trim('/');

        if (path.Equals("/api/health", StringComparison.Ordinal))
        {
            responses["200"] = CreateHealthResponse(
                "Liveness: the API process can respond.",
                "app",
                "RealEstate.Api");
            return;
        }

        if (!path.Equals("/api/health/readiness", StringComparison.Ordinal) &&
            !path.Equals("/api/health/database", StringComparison.Ordinal))
        {
            return;
        }

        responses["200"] = CreateHealthResponse(
            "Readiness: PostgreSQL is available.",
            "database",
            "PostgreSQL");
        responses["503"] = CreateHealthResponse(
            "Readiness: PostgreSQL is unavailable.",
            "database",
            "PostgreSQL",
            statusValue: "unavailable");
    }

    private static OpenApiResponse CreateHealthResponse(
        string description,
        string componentName,
        string componentValue,
        string statusValue = "ok")
    {
        return new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        AdditionalPropertiesAllowed = false,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["status"] = CreateFixedStringSchema(statusValue),
                            [componentName] = CreateFixedStringSchema(componentValue)
                        },
                        Required = new HashSet<string>(
                            ["status", componentName],
                            StringComparer.Ordinal)
                    }
                }
            }
        };
    }

    private static OpenApiSchema CreateFixedStringSchema(string value)
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = [value]
        };
    }

    private static void EnsureResponse(
        OpenApiOperation operation,
        string status,
        string description)
    {
        OpenApiResponses responses = GetResponses(operation);

        if (!responses.ContainsKey(status))
        {
            responses[status] = new OpenApiResponse
            {
                Description = description
            };
        }
    }

    private static OpenApiResponses GetResponses(OpenApiOperation operation)
    {
        return operation.Responses ?? throw new InvalidOperationException(
            "Swagger generation requires an operation response collection.");
    }

    private static string GetResponseDescription(string status)
    {
        return status switch
        {
            "401" => "Unauthorized",
            "403" => "Forbidden",
            "404" => "Not Found",
            "409" => "Conflict",
            "500" => "Internal Server Error",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}
