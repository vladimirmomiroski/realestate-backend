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
using RealEstate.Application.Agencies.Queries.GetAgencyDashboardListings;
using RealEstate.Domain.Enums;
using RealEstate.Application.Agencies.Commands.AcceptAgencyInvitation;
using RealEstate.Application.Agencies.Commands.CancelAgencyInvitation;
using RealEstate.Application.Agencies.Commands.CreateAgencyInvitation;
using RealEstate.Application.Agencies.Queries.GetAgencyInvitations;
using RealEstate.Application.Agencies.Commands.DisableAgencyMember;
using RealEstate.Application.Agencies.Commands.ChangeAgencyMemberRole;
using RealEstate.Application.Agencies.Commands.UploadAgencyLogo;
using RealEstate.Application.Agencies.Commands.DeleteAgencyLogo;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Agencies.Queries.GetAgencyDashboardSummary;

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
    private readonly GetAgencyDashboardListingsHandler _getAgencyDashboardListingsHandler;
    private readonly CreateAgencyInvitationHandler _createAgencyInvitationHandler;
    private readonly AcceptAgencyInvitationHandler _acceptAgencyInvitationHandler;
    private readonly CancelAgencyInvitationHandler _cancelAgencyInvitationHandler;
    private readonly DisableAgencyMemberHandler _disableAgencyMemberHandler;
    private readonly ChangeAgencyMemberRoleHandler _changeAgencyMemberRoleHandler;
    private readonly GetAgencyInvitationsHandler _getAgencyInvitationsHandler;
    private readonly UploadAgencyLogoHandler _uploadAgencyLogoHandler;
    private readonly DeleteAgencyLogoHandler _deleteAgencyLogoHandler;
    private readonly GetAgencyDashboardSummaryHandler _getAgencyDashboardSummaryHandler;

    public AgenciesController(CreateAgencyHandler createAgencyHandler, GetAgencyByIdHandler getAgencyByIdHandler, GetMyAgenciesHandler getMyAgenciesHandler, GetAgencyMembersHandler getAgencyMembersHandler, GetAgencyBySlugHandler getAgencyBySlugHandler, GetAgencyListingsHandler getAgencyListingsHandler, UpdateAgencyHandler updateAgencyHandler, GetAgencyDashboardListingsHandler getAgencyDashboardListingsHandler, CreateAgencyInvitationHandler createAgencyInvitationHandler, AcceptAgencyInvitationHandler acceptAgencyInvitationHandler, CancelAgencyInvitationHandler cancelAgencyInvitationHandler, DisableAgencyMemberHandler disableAgencyMemberHandler, ChangeAgencyMemberRoleHandler changeAgencyMemberRoleHandler, GetAgencyInvitationsHandler getAgencyInvitationsHandler, UploadAgencyLogoHandler uploadAgencyLogoHandler, DeleteAgencyLogoHandler deleteAgencyLogoHandler, GetAgencyDashboardSummaryHandler getAgencyDashboardSummaryHandler)
    {
        _createAgencyHandler = createAgencyHandler;
        _getAgencyByIdHandler = getAgencyByIdHandler;
        _getMyAgenciesHandler = getMyAgenciesHandler;
        _getAgencyMembersHandler = getAgencyMembersHandler;
        _getAgencyBySlugHandler = getAgencyBySlugHandler;
        _getAgencyListingsHandler = getAgencyListingsHandler;
        _updateAgencyHandler = updateAgencyHandler;
        _getAgencyDashboardListingsHandler = getAgencyDashboardListingsHandler;
        _createAgencyInvitationHandler = createAgencyInvitationHandler;
        _acceptAgencyInvitationHandler = acceptAgencyInvitationHandler;
        _cancelAgencyInvitationHandler = cancelAgencyInvitationHandler;
        _disableAgencyMemberHandler = disableAgencyMemberHandler;
        _changeAgencyMemberRoleHandler = changeAgencyMemberRoleHandler;
        _getAgencyInvitationsHandler = getAgencyInvitationsHandler;
        _uploadAgencyLogoHandler = uploadAgencyLogoHandler;
        _deleteAgencyLogoHandler = deleteAgencyLogoHandler;
        _getAgencyDashboardSummaryHandler = getAgencyDashboardSummaryHandler;
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
        }

        if (result.Status == ServiceResultStatus.Forbidden)
        {
            return Forbid();
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

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
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
    [HttpGet("{id:guid}/dashboard/listings")]
    [ProducesResponseType(typeof(PagedResult<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ListingResponse>>> GetAgencyDashboardListings(
    Guid id,
    [FromQuery] string? lang,
    [FromQuery] ListingStatus? status,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var query = new GetAgencyDashboardListingsQuery
        {
            AgencyId = id,
            LanguageCode = lang,
            Status = status,
            Page = page,
            PageSize = pageSize
        };

        ServiceResult<PagedResult<ListingResponse>> result =
            await _getAgencyDashboardListingsHandler.HandleAsync(query, cancellationToken);

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

    [Authorize]
    [HttpGet("{agencyId:guid}/dashboard/summary")]
    [ProducesResponseType(
    typeof(AgencyDashboardSummaryResponse),
    StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgencyDashboardSummaryResponse>>
    GetAgencyDashboardSummary(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyDashboardSummaryResponse> result =
            await _getAgencyDashboardSummaryHandler.HandleAsync(
                agencyId,
                cancellationToken);

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

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
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

    [Authorize]
    [HttpPost("{id:guid}/invitations")]
    [ProducesResponseType(typeof(AgencyInvitationCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgencyInvitationCreatedResponse>> CreateAgencyInvitation(
    Guid id,
    [FromBody] CreateAgencyInvitationRequest request,
    CancellationToken cancellationToken)
    {
        ServiceResult<AgencyInvitationCreatedResponse> result =
            await _createAgencyInvitationHandler.HandleAsync(
                id,
                request,
                cancellationToken);

        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return BadRequest(result.Error);
        }

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
        }

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(result.Error);
        }

        if (result.Status == ServiceResultStatus.Forbidden)
        {
            return Forbid();
        }

        return Created(
            $"/api/agencies/{id}/invitations/{result.Value!.Id}",
            result.Value);
    }

    [Authorize]
    [HttpGet("{id:guid}/invitations")]
    [ProducesResponseType(typeof(IReadOnlyList<AgencyInvitationListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AgencyInvitationListItemResponse>>> GetAgencyInvitations(
    Guid id,
    [FromQuery] AgencyInvitationStatus? status,
    CancellationToken cancellationToken)
    {
        var query = new GetAgencyInvitationsQuery
        {
            AgencyId = id,
            Status = status
        };

        ServiceResult<IReadOnlyList<AgencyInvitationListItemResponse>> result =
            await _getAgencyInvitationsHandler.HandleAsync(
                query,
                cancellationToken);

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
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

    [Authorize]
    [HttpPut("invitations/accept")]
    [ProducesResponseType(typeof(AgencyInvitationListItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgencyInvitationListItemResponse>> AcceptAgencyInvitation(
        [FromBody] AcceptAgencyInvitationRequest request,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyInvitationListItemResponse> result =
            await _acceptAgencyInvitationHandler.HandleAsync(
                request,
                cancellationToken);

        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return BadRequest(result.Error);
        }

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
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

    [Authorize]
    [HttpPut("{agencyId:guid}/invitations/{invitationId:guid}/cancel")]
    [ProducesResponseType(typeof(AgencyInvitationListItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgencyInvitationListItemResponse>> CancelAgencyInvitation(
        Guid agencyId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyInvitationListItemResponse> result =
            await _cancelAgencyInvitationHandler.HandleAsync(
                agencyId,
                invitationId,
                cancellationToken);

        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return BadRequest(result.Error);
        }

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
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

    [Authorize]
    [HttpPut("{agencyId:guid}/members/{memberId:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableAgencyMember(
    Guid agencyId,
    Guid memberId,
    CancellationToken cancellationToken)
    {
        ServiceResult<bool> result =
            await _disableAgencyMemberHandler.HandleAsync(
                agencyId,
                memberId,
                cancellationToken);

        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return BadRequest(result.Error);
        }

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
        }

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(result.Error);
        }

        if (result.Status == ServiceResultStatus.Forbidden)
        {
            return Forbid();
        }

        return NoContent();
    }

    [Authorize]
    [HttpPut("{agencyId:guid}/members/{memberId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeAgencyMemberRole(
    Guid agencyId,
    Guid memberId,
    [FromBody] ChangeAgencyMemberRoleRequest request,
    CancellationToken cancellationToken)
    {
        ServiceResult<bool> result =
            await _changeAgencyMemberRoleHandler.HandleAsync(
                agencyId,
                memberId,
                request,
                cancellationToken);

        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return BadRequest(result.Error);
        }

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
        }

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(result.Error);
        }

        if (result.Status == ServiceResultStatus.Forbidden)
        {
            return Forbid();
        }

        return NoContent();
    }

    [Authorize]
    [HttpPut("{agencyId:guid}/logo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgencyResponse>> UploadAgencyLogo(
    Guid agencyId,
    IFormFile? file,
    CancellationToken cancellationToken)
    {
        using var stream = file?.OpenReadStream();

        UploadedFile? uploadedFile = file is null
            ? null
            : new UploadedFile(
                stream!,
                file.FileName,
                file.ContentType,
                file.Length);

        ServiceResult<AgencyResponse> result =
            await _uploadAgencyLogoHandler.HandleAsync(
                new UploadAgencyLogoCommand(
                    agencyId,
                    uploadedFile),
                cancellationToken);

        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return BadRequest(result.Error);
        }

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
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

    [Authorize]
    [HttpDelete("{agencyId:guid}/logo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAgencyLogo(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        ServiceResult<bool> result =
            await _deleteAgencyLogoHandler.HandleAsync(
                new DeleteAgencyLogoCommand(agencyId),
                cancellationToken);

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(result.Error);
        }

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(result.Error);
        }

        if (result.Status == ServiceResultStatus.Forbidden)
        {
            return Forbid();
        }

        return NoContent();
    }
}
