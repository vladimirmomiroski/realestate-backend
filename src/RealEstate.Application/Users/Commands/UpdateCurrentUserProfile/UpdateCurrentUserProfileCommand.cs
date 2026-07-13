namespace RealEstate.Application.Users.Commands.UpdateCurrentUserProfile;

public sealed record UpdateCurrentUserProfileCommand(
    string? FirstName,
    string? LastName,
    string? PhoneNumber);