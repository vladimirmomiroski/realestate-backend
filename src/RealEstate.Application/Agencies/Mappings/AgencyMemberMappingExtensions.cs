using RealEstate.Application.Agencies.Dtos;
using RealEstate.Application.Agencies.ReadModels;

namespace RealEstate.Application.Agencies.Mappings;

public static class AgencyMemberMappingExtensions
{
    public static AgencyMemberResponse ToAgencyMemberResponse(
        this AgencyMemberReadModel member)
    {
        return new AgencyMemberResponse
        {
            MemberId = member.MemberId,
            UserId = member.UserId,
            Email = member.Email,
            FirstName = member.FirstName,
            LastName = member.LastName,
            UserStatus = member.UserStatus,
            MemberRole = member.MemberRole,
            MemberStatus = member.MemberStatus,
            JoinedAtUtc = member.JoinedAtUtc
        };
    }
}