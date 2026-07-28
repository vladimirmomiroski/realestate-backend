using Microsoft.AspNetCore.Mvc;
using RealEstate.Application.Auth.Commands.LoginUser;
using RealEstate.Application.Auth.Commands.RegisterUser;
using RealEstate.Application.Auth.Dtos;
using RealEstate.Api.Errors;
using RealEstate.Application.Common;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly RegisterUserHandler _registerUserHandler;
    private readonly LoginUserHandler _loginUserHandler;
    private readonly ApiFailureService _failureService;

    public AuthController(
        RegisterUserHandler registerUserHandler,
        LoginUserHandler loginUserHandler,
        ApiFailureService failureService)
    {
        _registerUserHandler = registerUserHandler;
        _loginUserHandler = loginUserHandler;
        _failureService = failureService;
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

            RegisterUserResultType.EmailAlreadyExists =>
                _failureService.CreateResult(
                    HttpContext,
                    ApiFailureDescriptor.EmailAlreadyExists),

            RegisterUserResultType.ValidationFailed => CreateValidationResult(
                result.ValidationKey,
                result.Error),

            _ => throw new InvalidOperationException(
                "The registration result was not mapped.")
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

            LoginUserResultType.InvalidCredentials =>
                _failureService.CreateResult(
                    HttpContext,
                    ApiFailureDescriptor.AuthenticationInvalidCredentials),

            LoginUserResultType.ValidationFailed => CreateValidationResult(
                result.ValidationKey,
                result.Error),

            _ => throw new InvalidOperationException(
                "The login result was not mapped.")
        };
    }

    private IActionResult CreateValidationResult(
        string? validationKey,
        string? error)
    {
        return _failureService.CreateValidationResult(
            HttpContext,
            validationKey ?? throw new InvalidOperationException(
                "A validation result must provide a validation key."),
            error ?? throw new InvalidOperationException(
                "A validation result must provide an error."),
            ErrorCodes.ValidationFailed);
    }
}
