namespace RealEstate.Application.Listings.Queries.GetListings;

public sealed class GetListingsValidator
{
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
        if (!ListingSortOptionParser.TryParse(
                query.Sort,
                out ListingSortOption sortOption))
        {
            return InvalidSortError;
        }

        if (query.Currency is not null &&
            !IsValidCurrency(query.Currency))
        {
            return "Currency must contain exactly three ASCII letters.";
        }

        if (query.MinPrice is <= 0)
        {
            return "Minimum price must be greater than zero.";
        }

        if (query.MaxPrice is <= 0)
        {
            return "Maximum price must be greater than zero.";
        }

        if (query.MinPrice.HasValue &&
            query.MaxPrice.HasValue &&
            query.MinPrice.Value > query.MaxPrice.Value)
        {
            return "Minimum price cannot be greater than maximum price.";
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
            return "Currency is required when filtering or sorting by price.";
        }

        if (query.MinAreaSquareMeters is <= 0)
        {
            return "Minimum area must be greater than zero.";
        }

        if (query.MaxAreaSquareMeters is <= 0)
        {
            return "Maximum area must be greater than zero.";
        }

        if (query.MinAreaSquareMeters.HasValue &&
            query.MaxAreaSquareMeters.HasValue &&
            query.MinAreaSquareMeters.Value >
            query.MaxAreaSquareMeters.Value)
        {
            return "Minimum area cannot be greater than maximum area.";
        }

        if (query.MinRooms is < 0)
        {
            return "Minimum rooms cannot be negative.";
        }

        if (query.MaxRooms is < 0)
        {
            return "Maximum rooms cannot be negative.";
        }

        if (query.MinRooms.HasValue &&
            query.MaxRooms.HasValue &&
            query.MinRooms.Value > query.MaxRooms.Value)
        {
            return "Minimum rooms cannot be greater than maximum rooms.";
        }

        if (query.MinYardAreaSquareMeters is < 0)
        {
            return "Minimum yard area cannot be negative.";
        }

        if (query.MaxYardAreaSquareMeters is < 0)
        {
            return "Maximum yard area cannot be negative.";
        }

        if (query.MinYardAreaSquareMeters.HasValue &&
            query.MaxYardAreaSquareMeters.HasValue &&
            query.MinYardAreaSquareMeters.Value >
            query.MaxYardAreaSquareMeters.Value)
        {
            return "Minimum yard area cannot be greater than maximum yard area.";
        }

        if (query.City is { Length: > 100 })
        {
            return CityTooLongError;
        }

        if (query.Municipality is { Length: > 100 })
        {
            return MunicipalityTooLongError;
        }

        if (query.Neighborhood is { Length: > 100 })
        {
            return NeighborhoodTooLongError;
        }

        if (query.SearchText is { Length: < 2 })
        {
            return SearchTextTooShortError;
        }

        if (query.SearchText is { Length: > 100 })
        {
            return SearchTextTooLongError;
        }

        return null;
    }

    private static bool IsValidCurrency(string currency)
    {
        return currency.Length == 3 &&
               currency.All(character =>
                   character is >= 'A' and <= 'Z');
    }
}
