namespace RealEstate.Application.Listings.Queries.GetComparableListings;

public sealed class GetComparableListingsQuery
{
    public Guid ListingId { get; set; }

    public string LanguageCode { get; set; } = "mk";

    public int Limit { get; set; } = 6;
}
