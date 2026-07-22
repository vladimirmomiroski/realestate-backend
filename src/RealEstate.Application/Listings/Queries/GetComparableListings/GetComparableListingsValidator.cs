namespace RealEstate.Application.Listings.Queries.GetComparableListings;

public sealed class GetComparableListingsValidator
{
    public const string InvalidLimitError =
        "Limit must be between 1 and 12.";

    public string? Validate(
        GetComparableListingsQuery query)
    {
        return query.Limit is < 1 or > 12
            ? InvalidLimitError
            : null;
    }
}
