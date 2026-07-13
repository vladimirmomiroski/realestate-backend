namespace RealEstate.Application.Users.Dtos;

public sealed record UpdateUserProfileRequest(
    string? FirstName,
    string? LastName,
    string? PhoneNumber);
