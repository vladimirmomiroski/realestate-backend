using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.ReadModels;

public sealed class AgencyMemberReadModel
{
    public Guid MemberId { get; set; }

    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public UserStatus UserStatus { get; set; }

    public AgencyMemberRole MemberRole { get; set; }

    public AgencyMemberStatus MemberStatus { get; set; }

    public DateTime JoinedAtUtc { get; set; }
}
