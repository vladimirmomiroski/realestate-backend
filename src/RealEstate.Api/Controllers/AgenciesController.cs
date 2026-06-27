using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Agencies.Commands.CreateAgency;
using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Common;
using RealEstate.Application.Agencies.Queries.GetAgencyById;
using RealEstate.Application.Agencies.Queries.GetMyAgencies;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/agencies")]
public sealed class AgenciesController : ControllerBase
{
    private readonly CreateAgencyHandler _createAgencyHandler;
    private readonly GetAgencyByIdHandler _getAgencyByIdHandler;
    private readonly GetMyAgenciesHandler _getMyAgenciesHandler;

    public AgenciesController(CreateAgencyHandler createAgencyHandler, GetAgencyByIdHandler getAgencyByIdHandler, GetMyAgenciesHandler getMyAgenciesHandler)
    {
        _createAgencyHandler = createAgencyHandler;
        _getAgencyByIdHandler = getAgencyByIdHandler;
        _getMyAgenciesHandler = getMyAgenciesHandler;
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AgencyResponse>> CreateAgency(
        [FromBody] CreateAgencyRequest request,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse> result =
            await _createAgencyHandler.HandleAsync(request, cancellationToken);

        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return BadRequest(result.Error);
        }

        return Created($"/api/agencies/{result.Value!.Id}", result.Value);
    }

    [Authorize]
    [HttpGet("my")]
    [ProducesResponseType(typeof(List<MyAgencyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MyAgencyResponse>>> GetMyAgencies(
    CancellationToken cancellationToken)
    {
        IReadOnlyList<MyAgencyResponse> response =
            await _getMyAgenciesHandler.HandleAsync(cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgencyResponse>> GetAgencyById(
        Guid id,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse> result =
            await _getAgencyByIdHandler.HandleAsync(id, cancellationToken);

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(result.Error);
        }

        return Ok(result.Value);
    }
}
