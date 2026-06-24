using RealEstate.Domain.Common;
using RealEstate.Domain.Enums;

namespace RealEstate.Domain.Entities;

public sealed class Agency : IAuditableEntity
{
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

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public string? Description { get; private set; }

    public string? LogoUrl { get; private set; }

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
