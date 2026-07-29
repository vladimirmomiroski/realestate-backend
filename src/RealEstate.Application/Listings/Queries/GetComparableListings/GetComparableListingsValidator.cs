namespace RealEstate.Application.Listings.Queries.GetComparableListings;

public sealed class GetComparableListingsValidator
{
    public sealed record ValidationFailure(string Key, string Error);
    public const string InvalidLimitError =
        "Limit must be between 1 and 12.";

    public string? Validate(GetComparableListingsQuery query)
    {
        return ValidateWithKey(query)?.Error;
    }

    public ValidationFailure? ValidateWithKey(
        GetComparableListingsQuery query)
    {
        return query.Limit is < 1 or > 12
            ? new ValidationFailure("limit", InvalidLimitError)
            : null;
    }
}
