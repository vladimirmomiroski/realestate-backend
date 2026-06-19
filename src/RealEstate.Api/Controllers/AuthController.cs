using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Auth.Commands.RegisterUser;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly RegisterUserHandler _registerUserHandler;

    public AuthController(RegisterUserHandler registerUserHandler)
    {
        _registerUserHandler = registerUserHandler;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        RegisterUserResult result = await _registerUserHandler.HandleAsync(
            request,
            cancellationToken);

        return result.Type switch
        {
            RegisterUserResultType.Success => Created(
                $"/api/users/{result.Response!.User.Id}",
                result.Response),

            RegisterUserResultType.EmailAlreadyExists => Conflict(new
            {
                message = result.Error
            }),

            RegisterUserResultType.ValidationFailed => BadRequest(new
            {
                message = result.Error
            }),

            _ => BadRequest()
        };
    }
}