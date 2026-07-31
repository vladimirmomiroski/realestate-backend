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
using RealEstate.Api.Errors;

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
    private readonly ApiFailureService _failureService;

    public AgenciesController(CreateAgencyHandler createAgencyHandler, GetAgencyByIdHandler getAgencyByIdHandler, GetMyAgenciesHandler getMyAgenciesHandler, GetAgencyMembersHandler getAgencyMembersHandler, GetAgencyBySlugHandler getAgencyBySlugHandler, GetAgencyListingsHandler getAgencyListingsHandler, UpdateAgencyHandler updateAgencyHandler, GetAgencyDashboardListingsHandler getAgencyDashboardListingsHandler, CreateAgencyInvitationHandler createAgencyInvitationHandler, AcceptAgencyInvitationHandler acceptAgencyInvitationHandler, CancelAgencyInvitationHandler cancelAgencyInvitationHandler, DisableAgencyMemberHandler disableAgencyMemberHandler, ChangeAgencyMemberRoleHandler changeAgencyMemberRoleHandler, GetAgencyInvitationsHandler getAgencyInvitationsHandler, UploadAgencyLogoHandler uploadAgencyLogoHandler, DeleteAgencyLogoHandler deleteAgencyLogoHandler, GetAgencyDashboardSummaryHandler getAgencyDashboardSummaryHandler, ApiFailureService failureService)
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
        _failureService = failureService;
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAgency(
        [FromBody] CreateAgencyRequest request,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse> result =
            await _createAgencyHandler.HandleAsync(request, cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Created($"/api/agencies/{result.Value.Id}", result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful create-agency result must provide a value."),
            ServiceResultStatus.ValidationError => CreateFailureResult(result),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.Conflict => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The create-agency result was not mapped.")
        };
    }

    [Authorize]
    [HttpGet("my")]
    [ProducesResponseType(typeof(List<MyAgencyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyAgencies(
    CancellationToken cancellationToken)
    {
        ServiceResult<IReadOnlyList<MyAgencyResponse>> result =
            await _getMyAgenciesHandler.HandleAsync(cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful my-agencies result must provide a value."),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The my-agencies result was not mapped.")
        };
    }

    [HttpGet("by-slug/{slug}")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAgencyBySlug(
    string slug,
    CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse> result =
            await _getAgencyBySlugHandler.HandleAsync(slug, cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful agency-by-slug result must provide a value."),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The agency-by-slug result was not mapped.")
        };
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAgencyById(
        Guid id,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse> result =
            await _getAgencyByIdHandler.HandleAsync(id, cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful agency-by-id result must provide a value."),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The agency-by-id result was not mapped.")
        };
    }

    [Authorize]
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<AgencyMemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAgencyMembers(
    Guid id,
    CancellationToken cancellationToken)
    {
        ServiceResult<IReadOnlyList<AgencyMemberResponse>> result =
            await _getAgencyMembersHandler.HandleAsync(id, cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful agency-members result must provide a value."),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The agency-members result was not mapped.")
        };
    }

    [HttpGet("{id:guid}/listings")]
    [ProducesResponseType(
        typeof(PagedResponse<ListingResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAgencyListings(
        Guid id,
        [FromQuery] string? lang,
        [FromQuery] string sort = "newest",
        [FromQuery] string? currency = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAgencyListingsQuery
        {
            AgencyId = id,
            LanguageCode = lang,
            Sort = sort,
            Currency = currency,
            Page = page,
            PageSize = pageSize
        };

        ServiceResult<PagedResponse<ListingResponse>> result =
            await _getAgencyListingsHandler.HandleAsync(
                query,
                cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful public agency-listings result must provide a value."),
            ServiceResultStatus.ValidationError => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The public agency-listings result was not mapped.")
        };
    }

    [Authorize]
    [HttpGet("{id:guid}/dashboard/listings")]
    [ProducesResponseType(typeof(PagedResponse<ListingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAgencyDashboardListings(
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

        ServiceResult<PagedResponse<ListingResponse>> result =
            await _getAgencyDashboardListingsHandler.HandleAsync(query, cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful dashboard-listings result must provide a value."),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The dashboard-listings result was not mapped.")
        };
    }

    [Authorize]
    [HttpGet("{agencyId:guid}/dashboard/summary")]
    [ProducesResponseType(
    typeof(AgencyDashboardSummaryResponse),
    StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult>
    GetAgencyDashboardSummary(
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyDashboardSummaryResponse> result =
            await _getAgencyDashboardSummaryHandler.HandleAsync(
                agencyId,
                cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful dashboard-summary result must provide a value."),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The dashboard-summary result was not mapped.")
        };
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAgency(
    Guid id,
    [FromBody] UpdateAgencyRequest request,
    CancellationToken cancellationToken)
    {
        ServiceResult<AgencyResponse> result =
            await _updateAgencyHandler.HandleAsync(id, request, cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful update-agency result must provide a value."),
            ServiceResultStatus.ValidationError => CreateFailureResult(result),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The update-agency result was not mapped.")
        };
    }

    [Authorize]
    [HttpPost("{id:guid}/invitations")]
    [ProducesResponseType(typeof(AgencyInvitationCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAgencyInvitation(
    Guid id,
    [FromBody] CreateAgencyInvitationRequest request,
    CancellationToken cancellationToken)
    {
        ServiceResult<AgencyInvitationCreatedResponse> result =
            await _createAgencyInvitationHandler.HandleAsync(
                id,
                request,
                cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Created(
                    $"/api/agencies/{id}/invitations/{result.Value.Id}",
                    result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful create-invitation result must provide a value."),
            ServiceResultStatus.ValidationError => CreateFailureResult(result),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            ServiceResultStatus.Conflict => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The create-invitation result was not mapped.")
        };
    }

    [Authorize]
    [HttpGet("{id:guid}/invitations")]
    [ProducesResponseType(typeof(IReadOnlyList<AgencyInvitationListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAgencyInvitations(
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

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful invitation-list result must provide a value."),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The invitation-list result was not mapped.")
        };
    }

    [Authorize]
    [HttpPut("invitations/accept")]
    [ProducesResponseType(typeof(AgencyInvitationListItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptAgencyInvitation(
        [FromBody] AcceptAgencyInvitationRequest request,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyInvitationListItemResponse> result =
            await _acceptAgencyInvitationHandler.HandleAsync(
                request,
                cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful accept-invitation result must provide a value."),
            ServiceResultStatus.ValidationError => CreateFailureResult(result),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            ServiceResultStatus.Conflict => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The accept-invitation result was not mapped.")
        };
    }

    [Authorize]
    [HttpPut("{agencyId:guid}/invitations/{invitationId:guid}/cancel")]
    [ProducesResponseType(typeof(AgencyInvitationListItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAgencyInvitation(
        Guid agencyId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        ServiceResult<AgencyInvitationListItemResponse> result =
            await _cancelAgencyInvitationHandler.HandleAsync(
                agencyId,
                invitationId,
                cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful cancel-invitation result must provide a value."),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            ServiceResultStatus.Conflict => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The cancel-invitation result was not mapped.")
        };
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

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is true => NoContent(),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful disable-member result must be true."),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            ServiceResultStatus.Conflict => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The disable-member result was not mapped.")
        };
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

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is true => NoContent(),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful change-member-role result must be true."),
            ServiceResultStatus.ValidationError => CreateFailureResult(result),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            ServiceResultStatus.Conflict => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The change-member-role result was not mapped.")
        };
    }

    [Authorize]
    [HttpPut("{agencyId:guid}/logo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadAgencyLogo(
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

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is not null =>
                Ok(result.Value),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful upload-agency-logo result must provide a value."),
            ServiceResultStatus.ValidationError => CreateFailureResult(result),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The upload-agency-logo result was not mapped.")
        };
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

        return result.Status switch
        {
            ServiceResultStatus.Success when result.Value is true => NoContent(),
            ServiceResultStatus.Success => throw new InvalidOperationException(
                "A successful delete-agency-logo result must be true."),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            ServiceResultStatus.NotFound => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The delete-agency-logo result was not mapped.")
        };
    }

    private IActionResult CreateFailureResult<T>(ServiceResult<T> result)
    {
        if (result.Status == ServiceResultStatus.ValidationError)
        {
            return _failureService.CreateValidationResult(
                HttpContext,
                result.ValidationKey ?? throw new InvalidOperationException(
                    "A validation result must provide a validation key."),
                result.Error ?? throw new InvalidOperationException(
                    "A validation result must provide an error."),
                result.ErrorCode ?? throw new InvalidOperationException(
                    "A validation result must provide an error code."));
        }

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
