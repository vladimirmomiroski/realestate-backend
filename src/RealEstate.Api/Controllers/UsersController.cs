using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Common;
using RealEstate.Application.Users.Dtos;
using RealEstate.Application.Users.Queries.GetCurrentUser;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly GetCurrentUserHandler _getCurrentUserHandler;

    public UsersController(GetCurrentUserHandler getCurrentUserHandler)
    {
        _getCurrentUserHandler = getCurrentUserHandler;
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
}
