using RealEstate.Domain.Common;
using RealEstate.Domain.Enums;

namespace RealEstate.Domain.Entities;

public sealed class User : IAuditableEntity
{
    private User()
    {
    }

    public User(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string? phoneNumber,
        UserRole role = UserRole.User,
        UserStatus status = UserStatus.PendingVerification)
    {
        Id = Guid.NewGuid();
        Email = email.Trim();
        NormalizedEmail = Email.ToUpperInvariant();
        PasswordHash = passwordHash;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        Role = role;
        Status = status;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string? PhoneNumber { get; private set; }

    public UserRole Role { get; private set; }

    public UserStatus Status { get; private set; }

    public string? AvatarUrl { get; private set; }

    public string? AvatarStoredFileName { get; private set; }

    public string? AvatarContentType { get; private set; }

    public long? AvatarSizeBytes { get; private set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }

    public void UpdateProfile(
        string firstName,
        string lastName,
        string? phoneNumber)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
    }

    public void SetAvatar(
        string avatarUrl,
        string storedFileName,
        string contentType,
        long sizeBytes)
    {
        AvatarUrl = avatarUrl.Trim();
        AvatarStoredFileName = storedFileName.Trim();
        AvatarContentType = contentType.Trim();
        AvatarSizeBytes = sizeBytes;
    }

    public void RemoveAvatar()
    {
        AvatarUrl = null;
        AvatarStoredFileName = null;
        AvatarContentType = null;
        AvatarSizeBytes = null;
    }
}