using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.ReadModels;

namespace RealEstate.Application.Agencies.Mappings;

public static class AgencyMembershipMappingExtensions
{
    public static MyAgencyResponse ToMyAgencyResponse(
        this UserAgencyMembershipReadModel membership)
    {
        return new MyAgencyResponse
        {
            AgencyId = membership.AgencyId,
            Name = membership.Name,
            Slug = membership.Slug,
            LogoUrl = membership.LogoUrl,
            City = membership.City,
            Municipality = membership.Municipality,
            AgencyStatus = membership.AgencyStatus,
            MemberRole = membership.MemberRole,
            MemberStatus = membership.MemberStatus
        };
    }
}