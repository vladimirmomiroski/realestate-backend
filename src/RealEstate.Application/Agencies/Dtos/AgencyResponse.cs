using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Dtos;

public sealed class AgencyResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? LogoUrl { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? WebsiteUrl { get; set; }

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? Municipality { get; set; }

    public AgencyStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }
}