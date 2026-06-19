using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Auth.Commands.LoginUser;
using RealEstate.Application.Auth.Commands.RegisterUser;
using RealEstate.Application.Auth.Dtos;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly RegisterUserHandler _registerUserHandler;
    private readonly LoginUserHandler _loginUserHandler;

    public AuthController(
        RegisterUserHandler registerUserHandler,
        LoginUserHandler loginUserHandler)
    {
        _registerUserHandler = registerUserHandler;
        _loginUserHandler = loginUserHandler;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        LoginUserResult result = await _loginUserHandler.HandleAsync(
            request,
            cancellationToken);

        return result.Type switch
        {
            LoginUserResultType.Success => Ok(result.Response),

            LoginUserResultType.InvalidCredentials => Unauthorized(new
            {
                message = result.Error
            }),

            LoginUserResultType.ValidationFailed => BadRequest(new
            {
                message = result.Error
            }),

            _ => BadRequest()
        };
    }
}