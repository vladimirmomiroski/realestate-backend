using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Agencies.Commands.ApproveAgency;
using RealEstate.Application.Agencies.Commands.DisableAgency;
using RealEstate.Application.Agencies.Commands.RejectAgency;
using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Common;
using RealEstate.Api.Errors;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/admin/agencies")]
public sealed class AdminAgenciesController : ControllerBase
{
    private readonly ApproveAgencyHandler _approveAgencyHandler;
    private readonly RejectAgencyHandler _rejectAgencyHandler;
    private readonly DisableAgencyHandler _disableAgencyHandler;
    private readonly ApiFailureService _failureService;

    public AdminAgenciesController(
        ApproveAgencyHandler approveAgencyHandler,
        RejectAgencyHandler rejectAgencyHandler,
        DisableAgencyHandler disableAgencyHandler,
        ApiFailureService failureService)
    {
        _approveAgencyHandler = approveAgencyHandler;
        _rejectAgencyHandler = rejectAgencyHandler;
        _disableAgencyHandler = disableAgencyHandler;
        _failureService = failureService;
    }

    [Authorize]
    [HttpPut("{agencyId:guid}/approve")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveAgency(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse> result =
            await _approveAgencyHandler.HandleAsync(
                agencyId,
                cancellationToken);

        return MapResult(result);
    }

    [Authorize]
    [HttpPut("{agencyId:guid}/reject")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectAgency(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse> result =
            await _rejectAgencyHandler.HandleAsync(
                agencyId,
                cancellationToken);

        return MapResult(result);
    }

    [Authorize]
    [HttpPut("{agencyId:guid}/disable")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableAgency(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse> result =
            await _disableAgencyHandler.HandleAsync(
                agencyId,
                cancellationToken);

        return MapResult(result);
    }

    private IActionResult MapResult(
        ServiceResult<AgencyResponse> result)
    {
        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful admin agency result must provide a value."),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            ServiceResultStatus.Conflict => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The admin agency transition result was not mapped.")
        };
    }

    private IActionResult CreateFailureResult(
        ServiceResult<AgencyResponse> result)
    {
        string errorCode = result.ErrorCode ?? throw new InvalidOperationException(
            "A failure result must provide an error code.");

        if (errorCode == ErrorCodes.AuthenticationInvalidPrincipal)
        {
            Response.Headers["WWW-Authenticate"] = "Bearer";
        }

        return _failureService.CreateResult(
            HttpContext,
            ApiFailureDescriptor.ForCode(errorCode));
    }
}
