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
        ValidationFailure? validationFailure = Validate(request);

        if (validationFailure is not null)
        {
            return RegisterUserResult.ValidationFailed(
                validationFailure.Error,
                validationFailure.Key);
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

        UserRegistrationPersistenceResult persistenceResult =
            await _userRepository.PersistRegistrationAsync(
                user,
                cancellationToken);

        if (persistenceResult ==
            UserRegistrationPersistenceResult.NormalizedEmailAlreadyExists)
        {
            return RegisterUserResult.EmailAlreadyExists();
        }

        if (persistenceResult !=
            UserRegistrationPersistenceResult.Succeeded)
        {
            throw new InvalidOperationException(
                "The registration persistence result was not mapped.");
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

        return RegisterUserResult.Success(response);
    }

    private static ValidationFailure? Validate(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return new ValidationFailure("email", "Email is required.");
        }

        if (!new EmailAddressAttribute().IsValid(request.Email))
        {
            return new ValidationFailure("email", "Email is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return new ValidationFailure("password", "Password is required.");
        }

        if (request.Password.Length < 8)
        {
            return new ValidationFailure(
                "password",
                "Password must be at least 8 characters long.");
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return new ValidationFailure(
                "firstName",
                "First name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return new ValidationFailure(
                "lastName",
                "Last name is required.");
        }

        return null;
    }

    private sealed record ValidationFailure(string Key, string Error);
}
