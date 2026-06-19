using System.ComponentModel.DataAnnotations;
using RealEstate.Application.Auth.Dtos;
using RealEstate.Application.Common.Security;
using RealEstate.Application.Users.Repositories;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Auth.Commands.RegisterUser;

public sealed class RegisterUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterUserResult> HandleAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        string? validationError = Validate(request);

        if (validationError is not null)
        {
            return RegisterUserResult.ValidationFailed(validationError);
        }

        string email = request.Email.Trim();
        string normalizedEmail = email.ToUpperInvariant();

        bool emailExists = await _userRepository.ExistsByNormalizedEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            return RegisterUserResult.EmailAlreadyExists();
        }

        string passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User(
            email,
            passwordHash,
            request.FirstName,
            request.LastName,
            request.PhoneNumber);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        var response = new AuthResponse(
            new AuthUserResponse(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.Role.ToString(),
                user.Status.ToString()));

        return RegisterUserResult.Success(response);
    }

    private static string? Validate(RegisterRequest request)
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

        if (request.Password.Length < 8)
        {
            return "Password must be at least 8 characters long.";
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return "First name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return "Last name is required.";
        }

        return null;
    }
}