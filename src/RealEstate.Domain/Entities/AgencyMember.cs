using RealEstate.Domain.Common;
using RealEstate.Domain.Enums;

namespace RealEstate.Domain.Entities;

public sealed class AgencyMember : IAuditableEntity
{
    private AgencyMember()
    {
    }

    public AgencyMember(
        Guid agencyId,
        Guid userId,
        AgencyMemberRole role,
        AgencyMemberStatus status = AgencyMemberStatus.Active)
    {
        if (agencyId == Guid.Empty)
        {
            throw new ArgumentException("Agency id cannot be empty.", nameof(agencyId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        AgencyId = agencyId;
        UserId = userId;
        Role = role;
        Status = status;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid AgencyId { get; private set; }

    public Guid UserId { get; private set; }

    public AgencyMemberRole Role { get; private set; }

    public AgencyMemberStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ModifiedAtUtc { get; set; }

    public Agency Agency { get; private set; } = null!;

    public User User { get; private set; } = null!;
}