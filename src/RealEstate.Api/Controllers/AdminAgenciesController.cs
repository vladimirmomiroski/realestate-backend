using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Agencies.Commands.ApproveAgency;
using RealEstate.Application.Agencies.Commands.DisableAgency;
using RealEstate.Application.Agencies.Commands.RejectAgency;
using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Common;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/admin/agencies")]
public sealed class AdminAgenciesController : ControllerBase
{
    private readonly ApproveAgencyHandler _approveAgencyHandler;
    private readonly RejectAgencyHandler _rejectAgencyHandler;
    private readonly DisableAgencyHandler _disableAgencyHandler;

    public AdminAgenciesController(
        ApproveAgencyHandler approveAgencyHandler,
        RejectAgencyHandler rejectAgencyHandler,
        DisableAgencyHandler disableAgencyHandler)
    {
        _approveAgencyHandler = approveAgencyHandler;
        _rejectAgencyHandler = rejectAgencyHandler;
        _disableAgencyHandler = disableAgencyHandler;
    }

    [Authorize]
    [HttpPut("{agencyId:guid}/approve")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return BadRequest(result.Error);
        }

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
        }

        if (result.Status == ServiceResultStatus.Forbidden)
        {
            return Forbid();
        }

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(result.Error);
        }

        return Ok(result.Value);
    }
}
