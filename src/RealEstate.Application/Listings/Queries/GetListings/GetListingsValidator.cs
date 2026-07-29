namespace RealEstate.Application.Listings.Queries.GetListings;

public sealed class GetListingsValidator
{
    public sealed record ValidationFailure(string Key, string Error);
    public const string InvalidSortError =
        "Sort must be one of: newest, priceAsc, priceDesc.";

    public const string CityTooLongError =
    "City cannot exceed 100 characters.";

    public const string MunicipalityTooLongError =
        "Municipality cannot exceed 100 characters.";

    public const string NeighborhoodTooLongError =
        "Neighborhood cannot exceed 100 characters.";

    public const string SearchTextTooShortError =
        "Search query must contain at least 2 characters.";

    public const string SearchTextTooLongError =
        "Search query cannot exceed 100 characters.";

    public string? Validate(GetListingsQuery query)
    {
        return ValidateWithKey(query)?.Error;
    }

    public ValidationFailure? ValidateWithKey(GetListingsQuery query)
    {
        if (!ListingSortOptionParser.TryParse(
                query.Sort,
                out ListingSortOption sortOption))
        {
            return Failure("sort", InvalidSortError);
        }

        if (query.Currency is not null &&
            !IsValidCurrency(query.Currency))
        {
            return Failure(
                "currency",
                "Currency must contain exactly three ASCII letters.");
        }

        if (query.MinPrice is <= 0)
        {
            return Failure("minPrice", "Minimum price must be greater than zero.");
        }

        if (query.MaxPrice is <= 0)
        {
            return Failure("maxPrice", "Maximum price must be greater than zero.");
        }

        if (query.MinPrice.HasValue &&
            query.MaxPrice.HasValue &&
            query.MinPrice.Value > query.MaxPrice.Value)
        {
            return Failure(
                "request",
                "Minimum price cannot be greater than maximum price.");
        }

        bool usesPriceSort =
            sortOption is ListingSortOption.PriceAsc
                or ListingSortOption.PriceDesc;

        bool usesPriceFilter =
            query.MinPrice.HasValue ||
            query.MaxPrice.HasValue;

        if ((usesPriceSort || usesPriceFilter) &&
            query.Currency is null)
        {
            return Failure(
                "request",
                "Currency is required when filtering or sorting by price.");
        }

        if (query.MinAreaSquareMeters is <= 0)
        {
            return Failure(
                "minAreaSquareMeters",
                "Minimum area must be greater than zero.");
        }

        if (query.MaxAreaSquareMeters is <= 0)
        {
            return Failure(
                "maxAreaSquareMeters",
                "Maximum area must be greater than zero.");
        }

        if (query.MinAreaSquareMeters.HasValue &&
            query.MaxAreaSquareMeters.HasValue &&
            query.MinAreaSquareMeters.Value >
            query.MaxAreaSquareMeters.Value)
        {
            return Failure(
                "request",
                "Minimum area cannot be greater than maximum area.");
        }

        if (query.MinRooms is < 0)
        {
            return Failure("minRooms", "Minimum rooms cannot be negative.");
        }

        if (query.MaxRooms is < 0)
        {
            return Failure("maxRooms", "Maximum rooms cannot be negative.");
        }

        if (query.MinRooms.HasValue &&
            query.MaxRooms.HasValue &&
            query.MinRooms.Value > query.MaxRooms.Value)
        {
            return Failure(
                "request",
                "Minimum rooms cannot be greater than maximum rooms.");
        }

        if (query.MinYardAreaSquareMeters is < 0)
        {
            return Failure(
                "minYardAreaSquareMeters",
                "Minimum yard area cannot be negative.");
        }

        if (query.MaxYardAreaSquareMeters is < 0)
        {
            return Failure(
                "maxYardAreaSquareMeters",
                "Maximum yard area cannot be negative.");
        }

        if (query.MinYardAreaSquareMeters.HasValue &&
            query.MaxYardAreaSquareMeters.HasValue &&
            query.MinYardAreaSquareMeters.Value >
            query.MaxYardAreaSquareMeters.Value)
        {
            return Failure(
                "request",
                "Minimum yard area cannot be greater than maximum yard area.");
        }

        if (query.City is { Length: > 100 })
        {
            return Failure("city", CityTooLongError);
        }

        if (query.Municipality is { Length: > 100 })
        {
            return Failure("municipality", MunicipalityTooLongError);
        }

        if (query.Neighborhood is { Length: > 100 })
        {
            return Failure("neighborhood", NeighborhoodTooLongError);
        }

        if (query.SearchText is { Length: < 2 })
        {
            return Failure("q", SearchTextTooShortError);
        }

        if (query.SearchText is { Length: > 100 })
        {
            return Failure("q", SearchTextTooLongError);
        }

        return null;
    }

    private static ValidationFailure Failure(string key, string error)
    {
        return new ValidationFailure(key, error);
    }

    private static bool IsValidCurrency(string currency)
    {
        return currency.Length == 3 &&
               currency.All(character =>
                   character is >= 'A' and <= 'Z');
    }
}
