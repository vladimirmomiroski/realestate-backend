namespace RealEstate.Application.Auth.Commands.RegisterUser;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber);