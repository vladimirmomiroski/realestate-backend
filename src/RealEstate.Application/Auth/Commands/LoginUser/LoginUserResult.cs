using RealEstate.Application.Auth.Dtos;

namespace RealEstate.Application.Auth.Commands.LoginUser;

public enum LoginUserResultType
{
    Success,
    ValidationFailed,
    InvalidCredentials
}

public sealed record LoginUserResult(
    LoginUserResultType Type,
    LoginResponse? Response,
    string? Error)
{
    public static LoginUserResult Success(LoginResponse response)
        => new(LoginUserResultType.Success, response, null);

    public static LoginUserResult ValidationFailed(string error)
        => new(LoginUserResultType.ValidationFailed, null, error);

    public static LoginUserResult InvalidCredentials()
        => new(LoginUserResultType.InvalidCredentials, null, "Invalid email or password.");
}
