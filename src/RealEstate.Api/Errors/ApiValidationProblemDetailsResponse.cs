using Microsoft.AspNetCore.Mvc;

namespace RealEstate.Api.Errors;

public sealed class ApiValidationProblemDetailsResponse : ValidationProblemDetails
{
    public ApiValidationProblemDetailsResponse(
        IDictionary<string, string[]> errors)
        : base(errors)
    {
    }

    public string Code { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;
}
