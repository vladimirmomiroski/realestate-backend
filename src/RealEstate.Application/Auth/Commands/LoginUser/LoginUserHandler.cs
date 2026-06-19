using System.ComponentModel.DataAnnotations;
using RealEstate.Application.Auth.Dtos;
using RealEstate.Application.Common.Security;
using RealEstate.Application.Users.Repositories;

namespace RealEstate.Application.Auth.Commands.LoginUser;

public sealed class LoginUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public LoginUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginUserResult> HandleAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        string? validationError = Validate(request);

        if (validationError is not null)
        {
            return LoginUserResult.ValidationFailed(validationError);
        }

        string normalizedEmail = request.Email.Trim().ToUpperInvariant();

        var user = await _userRepository.GetByNormalizedEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            return LoginUserResult.InvalidCredentials();
        }

        bool passwordIsValid = _passwordHasher.VerifyPassword(
            user.PasswordHash,
            request.Password);

        if (!passwordIsValid)
        {
            return LoginUserResult.InvalidCredentials();
        }

        var response = new AuthResponse(
            new AuthUserResponse(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.Role.ToString(),
                user.Status.ToString()));

        return LoginUserResult.Success(response);
    }

    private static string? Validate(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return "Email is required.";
        }

        if (!new EmailAddressAttribute().IsValid(request.Email))
        {
            return "Email is invalid.";
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return "Password is required.";
        }

        return null;
    }
}
