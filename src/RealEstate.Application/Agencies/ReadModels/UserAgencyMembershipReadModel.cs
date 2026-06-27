using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.ReadModels;

public sealed class UserAgencyMembershipReadModel
{
    public Guid AgencyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public string? City { get; set; }

    public string? Municipality { get; set; }

    public AgencyStatus AgencyStatus { get; set; }

    public AgencyMemberRole MemberRole { get; set; }

    public AgencyMemberStatus MemberStatus { get; set; }
}
