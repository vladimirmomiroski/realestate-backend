namespace RealEstate.Application.Auth.Dtos;

public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Role,
    string Status);