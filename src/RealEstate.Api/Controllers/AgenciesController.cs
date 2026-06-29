using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Agencies.Commands.CreateAgency;
using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Common;
using RealEstate.Application.Agencies.Queries.GetAgencyById;
using RealEstate.Application.Agencies.Queries.GetMyAgencies;
using RealEstate.Application.Agencies.Queries.GetAgencyMembers;
using RealEstate.Application.Agencies.Queries.GetAgencyBySlug;
using RealEstate.Application.Agencies.Queries.GetAgencyListings;
using RealEstate.Application.Listings.Dtos;
using RealEstate.Application.Agencies.Commands.UpdateAgency;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/agencies")]
public sealed class AgenciesController : ControllerBase
{
    private readonly CreateAgencyHandler _createAgencyHandler;
    private readonly GetAgencyByIdHandler _getAgencyByIdHandler;
    private readonly GetMyAgenciesHandler _getMyAgenciesHandler;
    private readonly GetAgencyMembersHandler _getAgencyMembersHandler;
    private readonly GetAgencyBySlugHandler _getAgencyBySlugHandler;
    private readonly GetAgencyListingsHandler _getAgencyListingsHandler;
    private readonly UpdateAgencyHandler _updateAgencyHandler;

    public AgenciesController(CreateAgencyHandler createAgencyHandler, GetAgencyByIdHandler getAgencyByIdHandler, GetMyAgenciesHandler getMyAgenciesHandler, GetAgencyMembersHandler getAgencyMembersHandler, GetAgencyBySlugHandler getAgencyBySlugHandler, GetAgencyListingsHandler getAgencyListingsHandler, UpdateAgencyHandler updateAgencyHandler)
    {
        _createAgencyHandler = createAgencyHandler;
        _getAgencyByIdHandler = getAgencyByIdHandler;
        _getMyAgenciesHandler = getMyAgenciesHandler;
        _getAgencyMembersHandler = getAgencyMembersHandler;
        _getAgencyBySlugHandler = getAgencyBySlugHandler;
        _getAgencyListingsHandler = getAgencyListingsHandler;
        _updateAgencyHandler = updateAgencyHandler;
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

    [HttpGet("by-slug/{slug}")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgencyResponse>> GetAgencyBySlug(
    string slug,
    CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse> result =
            await _getAgencyBySlugHandler.HandleAsync(slug, cancellationToken);

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(result.Error);
        }

        return Ok(result.Value);
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

    [Authorize]
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<AgencyMemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AgencyMemberResponse>>> GetAgencyMembers(
    Guid id,
    CancellationToken cancellationToken)
    {
        ServiceResult<IReadOnlyList<AgencyMemberResponse>> result =
            await _getAgencyMembersHandler.HandleAsync(id, cancellationToken);

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(result.Error);
        }

        if (result.Status == ServiceResultStatus.Forbidden)
        {
            return Forbid();
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/listings")]
    [ProducesResponseType(typeof(PagedResult<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ListingResponse>>> GetAgencyListings(
    Guid id,
    [FromQuery] string? lang,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var query = new GetAgencyListingsQuery
        {
            AgencyId = id,
            LanguageCode = lang,
            Page = page,
            PageSize = pageSize
        };

        ServiceResult<PagedResult<ListingResponse>> result =
            await _getAgencyListingsHandler.HandleAsync(query, cancellationToken);

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(result.Error);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgencyResponse>> UpdateAgency(
    Guid id,
    [FromBody] UpdateAgencyRequest request,
    CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse> result =
            await _updateAgencyHandler.HandleAsync(id, request, cancellationToken);

        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return BadRequest(result.Error);
        }

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(result.Error);
        }

        if (result.Status == ServiceResultStatus.Forbidden)
        {
            return Forbid();
        }

        return Ok(result.Value);
    }
}
