using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Dtos;

public sealed class ChangeAgencyMemberRoleRequest
{
    public AgencyMemberRole Role { get; init; }
}
