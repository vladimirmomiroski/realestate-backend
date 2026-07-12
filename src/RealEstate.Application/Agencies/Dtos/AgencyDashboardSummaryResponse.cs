using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Dtos;

public sealed class AgencyDashboardSummaryResponse
{
    public Guid AgencyId { get; init; }

    public string AgencyName { get; init; } = null!;

    public AgencyStatus AgencyStatus { get; init; }

    public int TotalListings { get; init; }

    public int DraftListings { get; init; }

    public int ActiveListings { get; init; }

    public int ArchivedListings { get; init; }

    public int MembersCount { get; init; }

    public int ActiveMembersCount { get; init; }

    public int PendingInvitationsCount { get; init; }
}