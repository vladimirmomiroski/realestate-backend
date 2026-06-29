using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.ReadModels;

public sealed class AgencyMemberAccessReadModel
{
    public AgencyMemberRole Role { get; set; }

    public AgencyMemberStatus Status { get; set; }
}
