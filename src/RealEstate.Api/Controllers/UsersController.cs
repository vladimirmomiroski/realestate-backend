using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Common;
using RealEstate.Application.Users.Commands.UpdateCurrentUserProfile;
using RealEstate.Application.Users.Dtos;
using RealEstate.Application.Users.Queries.GetCurrentUser;
using RealEstate.Application.Common.Files;
using RealEstate.Application.Users.Commands.UploadCurrentUserAvatar;
using RealEstate.Application.Users.Commands.DeleteCurrentUserAvatar;
using RealEstate.Api.Errors;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly GetCurrentUserHandler _getCurrentUserHandler;
    private readonly UpdateCurrentUserProfileHandler _updateCurrentUserProfileHandler;
    private readonly UploadCurrentUserAvatarHandler _uploadCurrentUserAvatarHandler;
    private readonly DeleteCurrentUserAvatarHandler _deleteCurrentUserAvatarHandler;
    private readonly ApiFailureService _failureService;

    public UsersController(
        GetCurrentUserHandler getCurrentUserHandler,
        UpdateCurrentUserProfileHandler updateCurrentUserProfileHandler,      
        UploadCurrentUserAvatarHandler uploadCurrentUserAvatarHandler,     
        DeleteCurrentUserAvatarHandler deleteCurrentUserAvatarHandler,
        ApiFailureService failureService)
    {
        _getCurrentUserHandler = getCurrentUserHandler;
        _updateCurrentUserProfileHandler = updateCurrentUserProfileHandler;
        _uploadCurrentUserAvatarHandler = uploadCurrentUserAvatarHandler;
        _deleteCurrentUserAvatarHandler = deleteCurrentUserAvatarHandler;
        _failureService = failureService;
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(
        CancellationToken cancellationToken)
    {
        var result = await _getCurrentUserHandler.HandleAsync(
            new GetCurrentUserQuery(),
            cancellationToken);

        if (result.Status == ServiceResultStatus.Unauthorized)
        {
            return CreateFailureResult(result);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPut("me/profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateProfile(
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

            ServiceResultStatus.ValidationError => CreateFailureResult(result),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The profile update result was not mapped.")
        };
    }

    [Authorize]
    [HttpPut("me/avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadAvatar(
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

            ServiceResultStatus.ValidationError => CreateFailureResult(result),
            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The avatar upload result was not mapped.")
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

            ServiceResultStatus.Unauthorized => CreateFailureResult(result),
            ServiceResultStatus.Forbidden => CreateFailureResult(result),
            _ => throw new InvalidOperationException(
                "The avatar delete result was not mapped.")
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
