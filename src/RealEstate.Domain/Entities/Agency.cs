using RealEstate.Domain.Common;
using RealEstate.Domain.Enums;

namespace RealEstate.Domain.Entities;

public sealed class Agency : IAuditableEntity
{

    private readonly List<AgencyMember> _members = new();

    public IReadOnlyCollection<AgencyMember> Members => _members.AsReadOnly();
    private Agency()
    {
    }

    public Agency(
        string name,
        string slug,
        string? description,
        string? phoneNumber,
        string? email,
        string? websiteUrl,
        string? addressLine,
        string? city,
        string? municipality)
    {
        Id = Guid.NewGuid();
        Name = name;
        Slug = slug;
        Description = description;
        PhoneNumber = phoneNumber;
        Email = email;
        WebsiteUrl = websiteUrl;
        AddressLine = addressLine;
        City = city;
        Municipality = municipality;
        Status = AgencyStatus.PendingVerification;
    }

    public AgencyMember AddMember(
    Guid userId,
    AgencyMemberRole role,
    AgencyMemberStatus status = AgencyMemberStatus.Active)
    {
        if (_members.Any(member => member.UserId == userId))
        {
            throw new InvalidOperationException("User is already a member of this agency.");
        }

        var member = new AgencyMember(Id, userId, role, status);

        _members.Add(member);

        return member;
    }

    public void UpdateProfile(
    string name,
    string? description,
    string? phoneNumber,
    string? email,
    string? websiteUrl,
    string? addressLine,
    string? city,
    string? municipality)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Agency name is required.", nameof(name));
        }

        Name = name.Trim();
        Description = CleanNullableText(description);
        PhoneNumber = CleanNullableText(phoneNumber);
        Email = CleanNullableText(email);
        WebsiteUrl = CleanNullableText(websiteUrl);
        AddressLine = CleanNullableText(addressLine);
        City = CleanNullableText(city);
        Municipality = CleanNullableText(municipality);
    }

    public void SetLogo(
    string logoUrl,
    string storedFileName,
    string contentType,
    long sizeBytes)
    {
        LogoUrl = logoUrl.Trim();
        LogoStoredFileName = storedFileName.Trim();
        LogoContentType = contentType.Trim();
        LogoSizeBytes = sizeBytes;
    }

    public void RemoveLogo()
    {
        LogoUrl = null;
        LogoStoredFileName = null;
        LogoContentType = null;
        LogoSizeBytes = null;
    }

    private static string? CleanNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public string? Description { get; private set; }

    public string? LogoUrl { get; private set; }

    public string? LogoStoredFileName { get; private set; }

    public string? LogoContentType { get; private set; }

    public long? LogoSizeBytes { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? Email { get; private set; }

    public string? WebsiteUrl { get; private set; }

    public string? AddressLine { get; private set; }

    public string? City { get; private set; }

    public string? Municipality { get; private set; }

    public AgencyStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }
}
