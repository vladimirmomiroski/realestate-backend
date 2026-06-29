namespace RealEstate.Application.Agencies.Queries.GetAgencyListings;

public sealed class GetAgencyListingsQuery
{
    public Guid AgencyId { get; set; }

    public string? LanguageCode { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}