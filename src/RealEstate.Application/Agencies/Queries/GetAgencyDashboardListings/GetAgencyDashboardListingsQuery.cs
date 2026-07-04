using RealEstate.Domain.Enums;

namespace RealEstate.Application.Agencies.Queries.GetAgencyDashboardListings;

public sealed class GetAgencyDashboardListingsQuery
{
    public Guid AgencyId { get; set; }

    public string? LanguageCode { get; set; }

    public ListingStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
