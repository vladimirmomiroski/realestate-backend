using RealEstate.Application.Agencies.Dtos;
using RealEstate.Domain.Entities;

namespace RealEstate.Application.Agencies.Mappings;

public static class AgencyInvitationMappingExtensions
{
    public static AgencyInvitationResponse ToResponse(this AgencyInvitation invitation)
    {
        return new AgencyInvitationResponse
        {
            Id = invitation.Id,
            AgencyId = invitation.AgencyId,
            Email = invitation.Email,
            Role = invitation.Role,
            Status = invitation.Status,
            Token = invitation.Token,
            Code = invitation.Code,
            InvitedByUserId = invitation.InvitedByUserId,
            AcceptedByUserId = invitation.AcceptedByUserId,
            ExpiresAtUtc = invitation.ExpiresAtUtc,
            CreatedAtUtc = invitation.CreatedAtUtc,
            AcceptedAtUtc = invitation.AcceptedAtUtc,
            CancelledAtUtc = invitation.CancelledAtUtc
        };
    }
}
