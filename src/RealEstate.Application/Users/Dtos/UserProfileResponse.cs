using RealEstate.Domain.Enums;

namespace RealEstate.Application.Users.Dtos;

public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    UserRole Role,
    UserStatus Status,
    string? AvatarUrl,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);
