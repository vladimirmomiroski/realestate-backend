using RealEstate.Application.Auth.Dtos;

namespace RealEstate.Application.Auth.Commands.RegisterUser;

public enum RegisterUserResultType
{
    Success,
    ValidationFailed,
    EmailAlreadyExists
}

public sealed record RegisterUserResult(
    RegisterUserResultType Type,
    AuthResponse? Response,
    string? Error)
{
    public string? ValidationKey { get; private init; }

    public static RegisterUserResult Success(AuthResponse response)
        => new(RegisterUserResultType.Success, response, null);

    public static RegisterUserResult ValidationFailed(
        string error)
        => new(RegisterUserResultType.ValidationFailed, null, error);

    public static RegisterUserResult ValidationFailed(
        string error,
        string validationKey)
        => new(RegisterUserResultType.ValidationFailed, null, error)
        {
            ValidationKey = validationKey
        };

    public static RegisterUserResult EmailAlreadyExists()
        => new(RegisterUserResultType.EmailAlreadyExists, null, "Email already exists.");
}
