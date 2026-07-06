using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Common;
using RealEstate.Application.Users.Commands.UpdateCurrentUserProfile;
using RealEstate.Application.Users.Dtos;
using RealEstate.Application.Users.Queries.GetCurrentUser;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly GetCurrentUserHandler _getCurrentUserHandler;
    private readonly UpdateCurrentUserProfileHandler _updateCurrentUserProfileHandler;

    public UsersController(
        GetCurrentUserHandler getCurrentUserHandler,
        UpdateCurrentUserProfileHandler updateCurrentUserProfileHandler)
    {
        _getCurrentUserHandler = getCurrentUserHandler;
        _updateCurrentUserProfileHandler = updateCurrentUserProfileHandler;
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> GetMe(
        CancellationToken cancellationToken)
    {
        var result = await _getCurrentUserHandler.HandleAsync(
            new GetCurrentUserQuery(),
            cancellationToken);

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return Unauthorized(new
            {
                message = result.Error
            });
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPut("me/profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        [FromBody] UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateCurrentUserProfileHandler.HandleAsync(
            new UpdateCurrentUserProfileCommand(
                request.FirstName,
                request.LastName,
                request.PhoneNumber),
            cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success => Ok(result.Value),

            ServiceResultStatus.ValidationError => BadRequest(new
            {
                message = result.Error
            }),

            ServiceResultStatus.Unauthorized => Unauthorized(new
            {
                message = result.Error
            }),

            ServiceResultStatus.Forbidden => Forbid(),

            _ => BadRequest()
        };
    }
}
