using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using RealEstate.Application.Common;

namespace RealEstate.Api.Errors;

internal sealed class ApiClientErrorFactory : IClientErrorFactory
{
    private readonly ApiFailureService _failureService;

    public ApiClientErrorFactory(ApiFailureService failureService)
    {
        _failureService = failureService;
    }

    public IActionResult GetClientError(
        ActionContext actionContext,
        IClientErrorActionResult clientError)
    {
        int statusCode = clientError.StatusCode
            ?? StatusCodes.Status500InternalServerError;

        ProblemDetails problemDetails = statusCode == StatusCodes.Status400BadRequest
            ? _failureService.CreateValidation(
                actionContext.HttpContext,
                new Dictionary<string, string[]>
                {
                    ["request"] = ["The request is invalid."]
                },
                ErrorCodes.ValidationFailed)
            : _failureService.Create(
                actionContext.HttpContext,
                ApiFailureDescriptor.ForStatusCode(statusCode));

        return _failureService.CreateResult(problemDetails);
    }
}
