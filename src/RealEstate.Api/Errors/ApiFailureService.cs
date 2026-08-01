using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using RealEstate.Application.Common;

namespace RealEstate.Api.Errors;

public sealed class ApiFailureService
{
    public const string ContentType = "application/problem+json";

    private readonly JsonSerializerOptions _serializerOptions;

    public ApiFailureService(IOptions<JsonOptions> jsonOptions)
    {
        _serializerOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    internal ApiProblemDetailsResponse Create(
        HttpContext httpContext,
        ApiFailureDescriptor descriptor)
    {
        EnsureDefinedCode(descriptor.Code);

        return new ApiProblemDetailsResponse
        {
            Type = CreateType(descriptor.Code),
            Title = descriptor.Title,
            Status = descriptor.StatusCode,
            Detail = descriptor.Detail,
            Instance = httpContext.Request.Path.Value ?? string.Empty,
            Code = descriptor.Code,
            TraceId = httpContext.TraceIdentifier
        };
    }

    internal ApiValidationProblemDetailsResponse CreateValidation(
        HttpContext httpContext,
        ModelStateDictionary modelState)
    {
        Dictionary<string, string[]> errors = CreateValidationErrors(modelState);

        return CreateValidation(
            httpContext,
            errors,
            ErrorCodes.ValidationFailed);
    }

    internal ApiValidationProblemDetailsResponse CreateValidation(
        HttpContext httpContext,
        IDictionary<string, string[]> errors,
        string errorCode)
    {
        EnsureDefinedCode(errorCode);

        return new ApiValidationProblemDetailsResponse(errors)
        {
            Type = CreateType(errorCode),
            Title = ApiFailureDescriptor.ValidationFailed.Title,
            Status = StatusCodes.Status400BadRequest,
            Detail = ApiFailureDescriptor.ValidationFailed.Detail,
            Instance = httpContext.Request.Path.Value ?? string.Empty,
            Code = errorCode,
            TraceId = httpContext.TraceIdentifier
        };
    }

    public IActionResult CreateResult(ProblemDetails problemDetails)
    {
        return new ApiFailureResult(this, problemDetails);
    }

    internal IActionResult CreateResult(
        HttpContext httpContext,
        ApiFailureDescriptor descriptor)
    {
        return CreateResult(Create(httpContext, descriptor));
    }

    public IActionResult CreateValidationResult(
        HttpContext httpContext,
        string validationKey,
        string error,
        string errorCode)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [validationKey] = new[] { error }
        };

        return CreateResult(CreateValidation(httpContext, errors, errorCode));
    }

    public async Task<bool> TryWriteAsync(
        HttpContext httpContext,
        ProblemDetails problemDetails)
    {
        HttpResponse response = httpContext.Response;

        if (response.HasStarted ||
            response.ContentLength.HasValue ||
            !string.IsNullOrEmpty(response.ContentType))
        {
            return false;
        }

        response.StatusCode = problemDetails.Status
            ?? StatusCodes.Status500InternalServerError;
        response.ContentType = ContentType;

        await JsonSerializer.SerializeAsync(
            response.Body,
            problemDetails,
            problemDetails.GetType(),
            _serializerOptions,
            httpContext.RequestAborted);

        return true;
    }

    private static Dictionary<string, string[]> CreateValidationErrors(
        ModelStateDictionary modelState)
    {
        var errors = new Dictionary<string, List<string>>(
            StringComparer.Ordinal);

        foreach ((string key, ModelStateEntry? entry) in modelState)
        {
            if (entry is null || entry.Errors.Count == 0)
            {
                continue;
            }

            string responseKey = NormalizeValidationKey(key);

            if (!errors.TryGetValue(responseKey, out List<string>? messages))
            {
                messages = new List<string>();
                errors.Add(responseKey, messages);
            }

            foreach (ModelError error in entry.Errors)
            {
                string message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "The supplied value is invalid."
                    : error.ErrorMessage;

                if (!messages.Contains(message, StringComparer.Ordinal))
                {
                    messages.Add(message);
                }
            }
        }

        if (errors.Count == 0)
        {
            errors.Add(
                "request",
                new List<string> { "The request is invalid." });
        }

        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private static string NormalizeValidationKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key == "$")
        {
            return "request";
        }

        string normalized = key.StartsWith("$.", StringComparison.Ordinal)
            ? key[2..]
            : key;

        const string RequestPrefix = "request.";

        if (normalized.StartsWith(
                RequestPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[RequestPrefix.Length..];
        }

        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Equals("request", StringComparison.OrdinalIgnoreCase))
        {
            return "request";
        }

        string[] segments = normalized.Split('.');

        for (int index = 0; index < segments.Length; index++)
        {
            string segment = segments[index];
            int bracketIndex = segment.IndexOf('[');
            string propertyName = bracketIndex < 0
                ? segment
                : segment[..bracketIndex];
            string suffix = bracketIndex < 0
                ? string.Empty
                : segment[bracketIndex..];

            segments[index] = string.IsNullOrEmpty(propertyName)
                ? suffix
                : JsonNamingPolicy.CamelCase.ConvertName(propertyName) + suffix;
        }

        return string.Join('.', segments);
    }

    private static string CreateType(string errorCode)
    {
        return $"urn:realestate:error:{errorCode}";
    }

    private static void EnsureDefinedCode(string errorCode)
    {
        if (!ErrorCodes.IsDefined(errorCode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(errorCode),
                errorCode,
                "The error code is not in the closed API error catalogue.");
        }
    }

    private sealed class ApiFailureResult : IActionResult
    {
        private readonly ApiFailureService _failureService;
        private readonly ProblemDetails _problemDetails;

        public ApiFailureResult(
            ApiFailureService failureService,
            ProblemDetails problemDetails)
        {
            _failureService = failureService;
            _problemDetails = problemDetails;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            await _failureService.TryWriteAsync(
                context.HttpContext,
                _problemDetails);
        }
    }
}
