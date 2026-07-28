using Microsoft.AspNetCore.Mvc;

namespace RealEstate.Api.Errors;

public sealed class ApiProblemDetailsResponse : ProblemDetails
{
    public string Code { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;
}
