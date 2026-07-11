using RealEstate.Application.Agencies.Dtos;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Commands.ChangeAgencyMemberRole;

public sealed class ChangeAgencyMemberRoleValidator
{
    public string? Validate(ChangeAgencyMemberRoleRequest? request)
    {
        if (request is null)
        {
            return "Request is required.";
        }

        if (request.Role is not AgencyMemberRole.Owner
            and not AgencyMemberRole.Agent)
        {
            return "Agency member role must be Owner or Agent.";
        }

        return null;
    }
}
