using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Dtos;

public sealed class CreateAgencyInvitationRequest
{
    public string Email { get; init; } = null!;

    public AgencyMemberRole Role { get; init; }
}
