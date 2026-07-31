using RealEstate.Application.Agencies.Dtos;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Mappings;

public static class AgencyInvitationMappingExtensions
{
    public static AgencyInvitationCreatedResponse ToCreatedResponse(this AgencyInvitation invitation)
    {
        return new AgencyInvitationCreatedResponse
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

    public static AgencyInvitationListItemResponse ToListItemResponse(this AgencyInvitation invitation)
    {
        return ToListItemResponse(
            invitation,
            invitation.Status);
    }

    public static AgencyInvitationListItemResponse ToListItemResponse(
        this AgencyInvitation invitation,
        DateTime utcNow)
    {
        AgencyInvitationStatus effectiveStatus =
            invitation.Status ==
                AgencyInvitationStatus.Pending &&
            invitation.ExpiresAtUtc <= utcNow
                ? AgencyInvitationStatus.Expired
                : invitation.Status;

        return ToListItemResponse(
            invitation,
            effectiveStatus);
    }

    private static AgencyInvitationListItemResponse ToListItemResponse(
        AgencyInvitation invitation,
        AgencyInvitationStatus status)
    {
        return new AgencyInvitationListItemResponse
        {
            Id = invitation.Id,
            AgencyId = invitation.AgencyId,
            Email = invitation.Email,
            Role = invitation.Role,
            Status = status,
            InvitedByUserId = invitation.InvitedByUserId,
            AcceptedByUserId = invitation.AcceptedByUserId,
            ExpiresAtUtc = invitation.ExpiresAtUtc,
            CreatedAtUtc = invitation.CreatedAtUtc,
            AcceptedAtUtc = invitation.AcceptedAtUtc,
            CancelledAtUtc = invitation.CancelledAtUtc
        };
    }
}
