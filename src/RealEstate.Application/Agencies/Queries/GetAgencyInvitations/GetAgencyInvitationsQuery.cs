using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Queries.GetAgencyInvitations;

public sealed class GetAgencyInvitationsQuery
{
    public Guid AgencyId { get; init; }

    public AgencyInvitationStatus? Status { get; init; }
}
