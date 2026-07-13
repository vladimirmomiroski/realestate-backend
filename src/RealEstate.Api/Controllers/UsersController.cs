using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Common;
using RealEstate.Application.Users.Commands.UpdateCurrentUserProfile;
using RealEstate.Application.Users.Dtos;
using RealEstate.Application.Users.Queries.GetCurrentUser;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Users.Commands.UploadCurrentUserAvatar;
using RealEstate.Application.Users.Commands.DeleteCurrentUserAvatar;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly GetCurrentUserHandler _getCurrentUserHandler;
    private readonly UpdateCurrentUserProfileHandler _updateCurrentUserProfileHandler;
    private readonly UploadCurrentUserAvatarHandler _uploadCurrentUserAvatarHandler;
    private readonly DeleteCurrentUserAvatarHandler _deleteCurrentUserAvatarHandler;

    public UsersController(
        GetCurrentUserHandler getCurrentUserHandler,
        UpdateCurrentUserProfileHandler updateCurrentUserProfileHandler,      
        UploadCurrentUserAvatarHandler uploadCurrentUserAvatarHandler,     
        DeleteCurrentUserAvatarHandler deleteCurrentUserAvatarHandler)
    {
        _getCurrentUserHandler = getCurrentUserHandler;
        _updateCurrentUserProfileHandler = updateCurrentUserProfileHandler;
        _uploadCurrentUserAvatarHandler = uploadCurrentUserAvatarHandler;
        _deleteCurrentUserAvatarHandler = deleteCurrentUserAvatarHandler;
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

    [Authorize]
    [HttpPut("me/avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserProfileResponse>> UploadAvatar(
    IFormFile? file,
    CancellationToken cancellationToken)
    {
        using var stream = file?.OpenReadStream();

        var uploadedFile = file is null
            ? null
            : new UploadedFile(
                stream!,
                file.FileName,
                file.ContentType,
                file.Length);

        var result = await _uploadCurrentUserAvatarHandler.HandleAsync(
            new UploadCurrentUserAvatarCommand(uploadedFile),
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

    [Authorize]
    [HttpDelete("me/avatar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAvatar(
    CancellationToken cancellationToken)
    {
        var result = await _deleteCurrentUserAvatarHandler.HandleAsync(
            new DeleteCurrentUserAvatarCommand(),
            cancellationToken);

        return result.Status switch
        {
            ServiceResultStatus.Success => NoContent(),

            ServiceResultStatus.Unauthorized => Unauthorized(new
            {
                message = result.Error
            }),

            ServiceResultStatus.Forbidden => Forbid(),

            _ => BadRequest()
        };
    }
}
